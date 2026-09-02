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
# skipped only because of what is inside it, never because of what it is called -- and the
# skipped ones are printed, so nothing goes unchecked quietly. If you are about to add an
# extension list to this file, you are re-introducing the bug it exists to prevent (issue #196).
#
# Usage:  scripts/check-doc-paths.sh
# Exit:   0 every reference resolves, 1 at least one does not, 2 the script could not run.

set -uo pipefail

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

# Files holding a C0 control byte that text does not use. That is a property of the bytes, so a
# new binary format needs no change here, and one `git grep` classifies the whole repository.
# Tab, newline, carriage return and form feed are excluded from the set as legitimate in text.
declare -A IS_BINARY=()
while IFS= read -r path; do
    [[ -n $path ]] && IS_BINARY[$path]=1
done < <(git grep --files-with-matches --perl-regexp '[\x00-\x08\x0B\x0E-\x1F]' -- . 2>/dev/null)

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

# The helpers below assign to a global instead of printing, because a command substitution forks
# a subshell and this runs once per link across the whole repository.

# Collapses "." and ".." so docs/process/../audit/README.md is recognised as docs/audit/README.md.
# A path climbing above the repository root keeps its leading "..", which no tracked path has, so
# it is reported rather than quietly accepted.
_normalized=""
normalize_path() {
    local IFS=/ part
    local -a out=()
    for part in $1; do
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
    local target=$1

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
    if [[ $target == *%* && $target =~ ^([^%]|%[0-9A-Fa-f]{2})*$ ]]; then
        target=$(printf '%b' "${target//%/\\x}")
    fi

    _target=$target
    return 0
}

failures=0
scanned=0
skipped=()

while IFS= read -r -d '' file; do
    is_excluded "$file" && continue
    if [[ -n ${IS_BINARY[$file]+set} ]]; then
        skipped+=("$file")
        continue
    fi
    ((scanned++))

    base_dir=""
    [[ $file == */* ]] && base_dir=${file%/*}

    lineno=0
    in_fence=0
    while IFS= read -r line || [[ -n $line ]]; do
        ((lineno++))

        # A fenced code block holds examples and transcripts. A Markdown link is inline markup
        # that a fence turns back into literal text, so nothing inside one is a live reference.
        # Only a line that is itself a fence delimiter toggles this -- at most three spaces of
        # indent, per CommonMark -- so ``` used mid-sentence does not.
        if [[ $line =~ ^[[:space:]]{0,3}('```'|'~~~') ]]; then
            in_fence=$((1 - in_fence))
            continue
        fi
        ((in_fence)) && continue

        # Markdown links -- a bracketed label immediately followed by a parenthesised
        # destination. The destination may not contain a parenthesis, which no path in this
        # repository does. (Spelling the form out rather than showing it, because showing it
        # would make this comment a reference the loop below then tries to resolve.)
        rest=$line
        while [[ $rest =~ \]\(([^\(\)]*)\) ]]; do
            raw=${BASH_REMATCH[1]}
            rest=${rest#*"${BASH_REMATCH[0]}"}

            clean_target "$raw" || continue
            resolve_target "$base_dir" "$_target"
            [[ -n $_normalized ]] || continue
            [[ -n ${KNOWN_PATHS[$_normalized]+set} ]] && continue

            printf '%s:%s: %s -> %s (no such path in the repository)\n' \
                "$file" "$lineno" "$_target" "$_normalized"
            ((failures++))
        done
    done <"$file"
done < <(git ls-files -z)

if ((${#skipped[@]} > 0)); then
    printf 'check-doc-paths: skipped %d file(s) whose bytes are not text:\n' "${#skipped[@]}"
    printf '  %s\n' "${skipped[@]}"
fi

if ((failures > 0)); then
    printf '\ncheck-doc-paths: %d broken reference(s) across %d tracked text file(s).\n' \
        "$failures" "$scanned" >&2
    printf 'Fix the reference, or -- if the file is an archive that is never edited -- add its\n' >&2
    printf 'path to EXCLUDED_GLOBS in %s.\n' "$0" >&2
    exit 1
fi

printf 'check-doc-paths: %d tracked text file(s) checked, every reference resolves.\n' "$scanned"
