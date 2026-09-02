#!/usr/bin/env bash
#
# Fails when a tracked text file references a repository path that does not exist.
#
# Five consecutive pull requests (#189, #192, #193, #194, #195) deleted text describing code
# the repository no longer had, and each was verified by a hand-run grep that leaked. Three of
# them leaked the same way: the grep named the extensions to search, and an extension was
# forgotten every time -- first .ps1 and .txt, then .dgml, .bat, .cmd, .http, .sql and .json.
#
# So this script names no extension anywhere. The file list is `git ls-files`, and a file is
# skipped only because of what is inside it, never because of what it is called -- and every
# skip, by content or by path, is counted in the output so nothing goes unchecked quietly. If
# you are about to add an extension list to this file, or a second filter that narrows the file
# set, you are re-introducing the bug it exists to prevent (issue #196).
#
# Usage:  scripts/check-doc-paths.sh [--self-test]
#         --self-test exercises the parser against fixtures and checks nothing else.
# Exit:   0 every reference resolves, 1 at least one does not, 2 the script could not run.

set -uo pipefail

readonly SCRIPT_PATH='scripts/check-doc-paths.sh'

repo_root=$(git rev-parse --show-toplevel 2>/dev/null) || {
    printf 'check-doc-paths: not inside a git repository\n' >&2
    exit 2
}
cd "$repo_root" || exit 2

# Archives: historical records that are deliberately never edited, so they will always name
# things that have since moved. Whole paths are excluded rather than individual lines marked,
# because "this file is frozen" is a property of the file, and an inline opt-out marker could
# only be added by editing the very files the rule says not to edit.
#
# AUDIT_* and METHODOLOGY_v* carry no extension on purpose: docs/audit/AUDIT_2026-07.html is
# the same archived report as AUDIT_2026-07.md, and matching only .md would have let it through.
# Note this does not cover docs/audit/README.md, which is a living index and is checked.
EXCLUDED_GLOBS=(
    'docs/audit/AUDIT_*'
    'docs/audit/METHODOLOGY_v*'
    'docs/pull-requests/*'
    'governance/*'
)

is_excluded() {
    local path=$1 glob
    for glob in "${EXCLUDED_GLOBS[@]}"; do
        # shellcheck disable=SC2053 -- the right-hand side is a glob on purpose
        [[ $path == $glob ]] && return 0
    done
    return 1
}

# ---------------------------------------------------------------------------------------------
# Parsing helpers. These assign to a global instead of printing: a command substitution forks a
# subshell, and these run once per line and once per link across the whole repository.
# Everything below is covered by --self-test.
# ---------------------------------------------------------------------------------------------

# Collapses "." and ".." so docs/process/../audit/README.md is recognised as docs/audit/README.md.
# A path climbing above the repository root keeps its leading "..", which no tracked path has, so
# it is reported rather than quietly accepted.
_normalized=""
normalize_path() {
    local part restore_glob=0
    local -a out=() parts=()

    # Splitting on "/" needs word splitting but not globbing. Without this a destination
    # containing * or [...] is expanded against the repository root before it is resolved,
    # which both mangles the error message and can make a bogus path match a real one.
    [[ $- == *f* ]] || { set -f; restore_glob=1; }
    local IFS=/
    parts=($1)
    ((restore_glob)) && set +f

    for part in "${parts[@]}"; do
        case $part in
            '' | .) ;;
            ..)
                if ((${#out[@]} > 0)) && [[ ${out[-1]} != .. ]]; then
                    unset 'out[-1]'
                else
                    out+=("$part")
                fi
                ;;
            *) out+=("$part") ;;
        esac
    done
    _normalized="${out[*]}"
}

# Turns a link destination into a repository path. Destinations are relative to the file that
# contains them; a leading "/" means the repository root, which is how GitHub renders it.
resolve_target() {
    local base_dir=$1 target=$2
    if [[ $target == /* ]]; then
        normalize_path "${target#/}"
    elif [[ -n $base_dir ]]; then
        normalize_path "$base_dir/$target"
    else
        normalize_path "$target"
    fi
}

# Strips the parts of a Markdown destination that are not the path, and rejects destinations
# that do not name one. Returns 1 when there is nothing to check.
_target=""
clean_target() {
    local target=$1 escaped

    # <angle brackets> around a destination, and an optional "Title" following it.
    if [[ $target =~ ^[[:space:]]*\<([^\>]*)\> ]]; then
        target=${BASH_REMATCH[1]}
    elif [[ $target =~ ^[[:space:]]*([^[:space:]]+) ]]; then
        target=${BASH_REMATCH[1]}
    else
        return 1
    fi

    # A fragment or a query says which part of the target to show, not which file it is.
    target=${target%%#*}
    target=${target%%\?*}
    [[ -n $target ]] || return 1

    # http:, https:, mailto:, tel: and protocol-relative URLs point outside the repository.
    [[ $target == //* ]] && return 1
    [[ $target =~ ^[A-Za-z][A-Za-z0-9+.-]*: ]] && return 1

    # Template placeholders resolve where they are rendered, not here.
    [[ $target == *'{{'* || $target == *'${'* ]] && return 1

    # %20 and friends belong to the link syntax, not to the name on disk. Decoded only when
    # every % starts a valid escape, so a literal % is left alone instead of mangled.
    # Backslashes are doubled first, because printf %b would otherwise interpret escapes that
    # were already in the target: \c in particular means "stop output here", which would
    # truncate a broken destination into a shorter one that exists and let it pass.
    if [[ $target == *%* && $target != *%00* && $target =~ ^([^%]|%[0-9A-Fa-f]{2})*$ ]]; then
        escaped=${target//\\/\\\\}
        target=$(printf '%b' "${escaped//%/\\x}")
        [[ -n $target ]] || return 1
    fi

    _target=$target
    return 0
}

# Removes inline code spans. A destination inside backticks renders as literal text, not as a
# link, so leaving them in reports examples as if they were references. A link whose *label* is
# code -- the backticked-label style used throughout this repository's docs -- keeps its
# destination, because only the label sits inside the backticks.
_stripped=""
strip_code_spans() {
    local s=$1 out=""
    while [[ $s =~ ^([^\`]*)\`[^\`]*\`(.*)$ ]]; do
        out+=${BASH_REMATCH[1]}
        s=${BASH_REMATCH[2]}
    done
    _stripped="$out$s"
}

# Fenced code blocks hold examples and transcripts, and a fence turns inline markup back into
# literal text, so nothing inside one is a live reference.
#
# The closing fence must use the same character as the opening one and be at least as long
# (CommonMark). Tracking only "am I inside a fence" instead desynchronises on a nested example
# -- a ```` block quoting a ``` block closes early, and every link in the outer block is then
# reported -- and an odd number of delimiters would silently disable the check for the rest of
# the file. An unterminated fence running to end of file is correct, not a bug.
FENCE_OPEN_RE='^[[:space:]]{0,3}(```+|~~~+)'
FENCE_CLOSE_RE='^[[:space:]]{0,3}(```+|~~~+)[[:space:]]*$'
fence_char=""
fence_len=0

# Returns 0 when the line is a fence delimiter (state updated), 1 when it is an ordinary line.
fence_update() {
    local line=$1 marker
    if [[ -z $fence_char ]]; then
        [[ $line =~ $FENCE_OPEN_RE ]] || return 1
        marker=${BASH_REMATCH[1]}
        fence_char=${marker:0:1}
        fence_len=${#marker}
        return 0
    fi
    if [[ $line =~ $FENCE_CLOSE_RE ]]; then
        marker=${BASH_REMATCH[1]}
        if [[ ${marker:0:1} == "$fence_char" ]] && ((${#marker} >= fence_len)); then
            fence_char=""
            fence_len=0
        fi
    fi
    return 0
}

# ---------------------------------------------------------------------------------------------
# Self-test
# ---------------------------------------------------------------------------------------------

_st_pass=0
_st_fail=0

_assert() {
    if [[ $2 == "$3" ]]; then
        ((_st_pass++))
    else
        printf 'self-test FAIL: %s\n  expected: [%s]\n  actual:   [%s]\n' "$1" "$2" "$3" >&2
        ((_st_fail++))
    fi
}

_clean() {
    _target=""
    clean_target "$1" || _target='<rejected>'
    printf '%s' "$_target"
}

self_test() {
    normalize_path 'docs/process/../audit/README.md'
    _assert 'normalize: ..' 'docs/audit/README.md' "$_normalized"
    normalize_path 'a/./b//c'
    _assert 'normalize: . and empty' 'a/b/c' "$_normalized"
    normalize_path '../outside.md'
    _assert 'normalize: above root keeps ..' '../outside.md' "$_normalized"
    normalize_path 'docs/'
    _assert 'normalize: trailing slash' 'docs' "$_normalized"
    # Regression: an unquoted expansion here globbed the destination against the repo root.
    normalize_path 'docs/*'
    _assert 'normalize: glob is not expanded' 'docs/*' "$_normalized"
    normalize_path 'docs/[abc].md'
    _assert 'normalize: bracket is not expanded' 'docs/[abc].md' "$_normalized"

    _assert 'clean: plain' 'a/b.md' "$(_clean 'a/b.md')"
    _assert 'clean: fragment' 'a/b.md' "$(_clean 'a/b.md#section')"
    _assert 'clean: anchor only' '<rejected>' "$(_clean '#section')"
    _assert 'clean: query' 'a/b.md' "$(_clean 'a/b.md?raw=1')"
    _assert 'clean: title' 'a/b.md' "$(_clean 'a/b.md "The Title"')"
    _assert 'clean: angle brackets' 'a b.md' "$(_clean '<a b.md>')"
    _assert 'clean: https' '<rejected>' "$(_clean 'https://example.com/x.md')"
    _assert 'clean: mailto' '<rejected>' "$(_clean 'mailto:someone@example.com')"
    _assert 'clean: protocol relative' '<rejected>' "$(_clean '//example.com/x.md')"
    _assert 'clean: template' '<rejected>' "$(_clean '{{ site.url }}/x.md')"
    _assert 'clean: percent escape' 'a b.md' "$(_clean 'a%20b.md')"
    _assert 'clean: lone percent kept' '100%done.md' "$(_clean '100%done.md')"
    # Regression: printf %b interpreted backslashes already present in the destination, and \c
    # truncated it to a prefix that exists -- turning a broken link into a passing one.
    _assert 'clean: backslash survives decode' 'docs\c x.md' "$(_clean 'docs\c%20x.md')"
    _assert 'clean: backslash without decode' 'docs\cx.md' "$(_clean 'docs\cx.md')"

    # Fixtures spell a link destination as ']' '(' in two adjacent literals, so this file does
    # not itself contain a Markdown link. The check scans its own source like any other tracked
    # text file, and on the first run it reported these fixtures -- correctly.
    strip_code_spans 'see `[not a link](docs/X.md)` here'
    _assert 'strip: code span removed' 'see  here' "$_stripped"
    strip_code_spans '[`STANDARDS.md`]''(docs/STANDARDS.md)'
    _assert 'strip: code label keeps link' '[]''(docs/STANDARDS.md)' "$_stripped"
    strip_code_spans 'one ` unmatched [x]''(y.md)'
    _assert 'strip: unmatched backtick is inert' 'one ` unmatched [x]''(y.md)' "$_stripped"

    # Regression: a single in/out toggle closed the outer fence on the inner delimiter, so links
    # in the outer block were checked; and an odd delimiter count disabled the file entirely.
    fence_char=""
    fence_len=0
    fence_update '````markdown'
    _assert 'fence: outer open' '`' "$fence_char"
    fence_update '```'
    _assert 'fence: inner does not close outer' '`' "$fence_char"
    fence_update '````'
    _assert 'fence: matching length closes' '' "$fence_char"
    fence_update '~~~'
    _assert 'fence: tilde opens' '~' "$fence_char"
    fence_update '```'
    _assert 'fence: wrong char does not close' '~' "$fence_char"
    fence_update '~~~'
    _assert 'fence: tilde closes' '' "$fence_char"
    fence_update '    ```'
    _assert 'fence: four spaces is not a fence' '' "$fence_char"
    fence_char=""
    fence_len=0

    if ((_st_fail > 0)); then
        printf 'check-doc-paths --self-test: %d passed, %d FAILED\n' "$_st_pass" "$_st_fail" >&2
        return 1
    fi
    printf 'check-doc-paths --self-test: %d assertions passed.\n' "$_st_pass"
    return 0
}

case ${1:-} in
    --self-test)
        self_test
        exit $?
        ;;
    '') ;;
    *)
        printf 'check-doc-paths: unknown argument %s\nUsage: %s [--self-test]\n' \
            "$1" "$SCRIPT_PATH" >&2
        exit 2
        ;;
esac

# ---------------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------------

# Files holding a C0 control byte that text does not use. That is a property of the bytes, so a
# new binary format needs no change here, and one `git grep` classifies the whole repository.
# Tab, newline, carriage return and form feed are excluded from the set as legitimate in text.
declare -A IS_BINARY=()
binary_list=$(git grep --files-with-matches --perl-regexp '[\x00-\x08\x0B\x0E-\x1F]' -- .)
binary_status=$?
# 0 = matches found, 1 = none found. Anything else (notably 128, a git built without PCRE) would
# leave every binary file classified as text, so fail loudly rather than scan a PDF as prose.
if ((binary_status > 1)); then
    printf 'check-doc-paths: git grep failed (status %d); cannot classify binary files.\n' \
        "$binary_status" >&2
    exit 2
fi
while IFS= read -r path; do
    [[ -n $path ]] && IS_BINARY[$path]=1
done <<<"$binary_list"

# Every tracked file plus every directory that contains one, so a link to a folder resolves too.
# Built from git rather than from the filesystem so the answer is the same on a developer
# machine and on a runner: an untracked local file must not make a broken reference look fine.
declare -A KNOWN_PATHS=()
while IFS= read -r -d '' tracked; do
    KNOWN_PATHS[$tracked]=1
    dir=$tracked
    while [[ $dir == */* ]]; do
        dir=${dir%/*}
        KNOWN_PATHS[$dir]=1
    done
done < <(git ls-files -z)

if ((${#KNOWN_PATHS[@]} == 0)); then
    printf 'check-doc-paths: git ls-files returned nothing\n' >&2
    exit 2
fi

failures=0
scanned=0
excluded=0
skipped=()

# Reads lines from stdin so the caller can hand it either the file or a decoded stream.
scan_stream() {
    local file=$1 base_dir=$2
    local line lineno=0 rest raw

    fence_char=""
    fence_len=0

    while IFS= read -r line || [[ -n $line ]]; do
        ((lineno++))
        fence_update "$line" && continue
        [[ -n $fence_char ]] && continue

        strip_code_spans "$line"
        rest=$_stripped

        # Markdown links -- a bracketed label immediately followed by a parenthesised
        # destination. The destination may not contain a parenthesis, which no path in this
        # repository does. (Spelling the form out rather than showing it, because showing it
        # would make this comment a reference the loop below then tries to resolve.)
        while [[ $rest =~ \]\(([^\(\)]*)\) ]]; do
            raw=${BASH_REMATCH[1]}
            rest=${rest#*"${BASH_REMATCH[0]}"}

            clean_target "$raw" || continue
            resolve_target "$base_dir" "$_target"
            [[ -n $_normalized ]] || continue
            [[ -n ${KNOWN_PATHS[$_normalized]+set} ]] && continue

            printf '%s:%s: %s -> %s (no such path in the repository)\n' \
                "$file" "$lineno" "$_target" "$_normalized" >&2
            ((failures++))
        done
    done
}

# A UTF-16 file is text that Windows tooling wrote -- PowerShell 5.1 writes UTF-16LE by default,
# which is how a .cs file in this repository ended up that way. Its NUL bytes make every
# byte-based heuristic call it binary, so decode it rather than skip it, or a document saved
# from a PowerShell prompt would have all of its links quietly ignored.
has_utf16_bom() {
    local bom
    bom=$(head -c 2 -- "$1" | od -An -tx1 | tr -d '[:space:]')
    [[ $bom == fffe || $bom == feff ]]
}

have_iconv=1
command -v iconv >/dev/null 2>&1 || have_iconv=0

while IFS= read -r -d '' file; do
    if is_excluded "$file"; then
        ((excluded++))
        continue
    fi

    if [[ ! -r $file ]]; then
        printf 'check-doc-paths: %s is tracked but not readable; refusing to report a pass.\n' \
            "$file" >&2
        exit 2
    fi

    decode=0
    if [[ -n ${IS_BINARY[$file]+set} ]]; then
        if ((have_iconv)) && has_utf16_bom "$file"; then
            decode=1
        else
            skipped+=("$file")
            continue
        fi
    fi
    ((scanned++))

    base_dir=""
    [[ $file == */* ]] && base_dir=${file%/*}

    if ((decode)); then
        scan_stream "$file" "$base_dir" < <(iconv -f UTF-16 -t UTF-8 -- "$file" 2>/dev/null)
    else
        scan_stream "$file" "$base_dir" <"$file"
    fi
done < <(git ls-files -z)

if ((${#skipped[@]} > 0)); then
    printf 'check-doc-paths: skipped %d file(s) whose bytes are not text:\n' "${#skipped[@]}"
    printf '  %s\n' "${skipped[@]}"
fi
if ((excluded > 0)); then
    printf 'check-doc-paths: excluded %d archived file(s) by path (%s).\n' \
        "$excluded" "${EXCLUDED_GLOBS[*]}"
fi

if ((failures > 0)); then
    printf '\ncheck-doc-paths: %d broken reference(s) across %d tracked text file(s).\n' \
        "$failures" "$scanned" >&2
    printf 'Fix the reference, or -- if the file is an archive that is never edited -- add its\n' >&2
    printf 'path to EXCLUDED_GLOBS in %s.\n' "$SCRIPT_PATH" >&2
    exit 1
fi

printf 'check-doc-paths: %d tracked text file(s) checked, every reference resolves.\n' "$scanned"
