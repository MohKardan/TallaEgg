# TallaEgg — Code Audit Prompt (v1)

**Methodology Version:** 1 (assigned retrospectively — it carried no version when it was used)
**Used for:** the 2026-07-08 audit — [`AUDIT_2026-07.md`](AUDIT_2026-07.md) and
[`AUDIT_2026-07.html`](AUDIT_2026-07.html)
**Status:** historical. Current methodology is [`METHODOLOGY_v8.md`](METHODOLOGY_v8.md).

Archived so the trend table in [`README.md`](README.md) means something: a score is only
comparable to another score if you can see what was asked for. This is what was asked for in
July, and it explains most of what that audit looks like — fifty-odd dimensions weighted
equally, a score for every one of them, and a Persian HTML deliverable, which is why
`AUDIT_2026-07.html` exists and why every methodology since has said "no HTML".

The prompt text below is unaltered. It was originally written as three chat messages, and the
three lines that marked where one message ended and the next began have been removed — they
were delivery artifacts, not part of the method, and one of them fell in the middle of the
Performance list, which is now whole again. Nothing else was corrected, reordered or tidied.

---

# Role

You are a Principal Software Architect, Senior ASP.NET Core Engineer, and Software Quality Auditor.

Your task is to perform a COMPLETE architecture and code review of my ASP.NET Core project.

Do NOT just look for bugs.
Review the project exactly like an experienced software architect performing a professional code audit before releasing a production system.

---

# Objective

Analyze the entire solution and produce a comprehensive report including:

* Architecture
* Design Quality
* Maintainability
* Scalability
* Extensibility
* Testability
* Debuggability
* Readability
* Consistency
* Performance
* Security
* Code Smells
* SOLID
* Clean Architecture principles
* Domain Driven Design (if applicable)
* Dependency Injection usage
* Error Handling
* Logging
* Configuration
* API Design
* Validation
* Folder Structure
* Naming
* Async usage
* Database layer
* EF Core usage
* Caching
* Middleware
* Background Services
* HTTP Clients
* DTO Mapping
* Separation of Concerns
* Code Duplication
* Dead Code
* Technical Debt

---

# Review Style

Think like a senior reviewer at Microsoft.

Be brutally honest.

Do NOT try to be nice.

Every criticism must explain:

* Why it is a problem
* Possible risks
* Long-term impact
* Recommended solution
* Priority

---

# Scoring

Score each section from 0 to 10.

Example:

Architecture ............ 8.5/10

Testability ............. 6/10

Maintainability ......... 9/10

Debuggability ........... 5/10

Performance ............. 8/10

Consistency ............. 7/10

Security ................ 8/10

Scalability ............. 8/10

Readability ............. 9/10

Extensibility ........... 7/10

SOLID ................... 8/10

Dependency Injection .... 9/10

Logging ................. 4/10

Validation .............. 7/10

Error Handling .......... 5/10

Configuration ........... 8/10

Overall Project ......... 8.1/10

---

# Things to Inspect

## Solution Structure

Review whether the solution structure is appropriate.

Could folders be organized better?

Should projects be separated?

Should class libraries exist?

Should interfaces move elsewhere?

---

## Architecture

Determine which architecture is currently used.

Examples:

* Layered
* Clean Architecture
* Onion
* Vertical Slice
* Modular Monolith
* Feature Based
* N-Tier

Evaluate whether the architecture is appropriate.

Explain improvements.

---

## Dependency Injection

Inspect every registration.

Look for:

* Singleton misuse
* Scoped misuse
* Transient misuse
* Missing interfaces
* Service Locator
* Circular dependencies

---

## SOLID

Evaluate every principle separately.

Single Responsibility

Open Closed

Liskov

Interface Segregation

Dependency Inversion

Provide examples from the project.

---

## DRY

Find duplicated code.

Suggest refactoring.

---

## KISS

Identify unnecessary complexity.

---

## YAGNI

Find code that probably should not exist.

---

## Naming

Review names of:

Classes

Methods

Properties

Variables

Namespaces

Folders

Projects

Controllers

Services

Repositories

DTOs

Commands

Queries

Enums

Interfaces

Explain inconsistent naming.

---

## Folder Structure

Review whether folders are consistent.

Suggest improvements.

---

## API Design

Review:

Controllers

Minimal APIs

Routing

Response types

HTTP status codes

REST principles

Versioning

DTO usage

ProblemDetails

Pagination

Filtering

Sorting

---

## Exception Handling

Inspect:

try/catch usage

global exception middleware

custom exceptions

logging

error responses

---

## Logging

Evaluate:

ILogger usage

structured logging

missing logs

log levels

sensitive information

---

## Validation

Review:

FluentValidation

DataAnnotations

manual validation

duplicate validation

---

## EF Core

Inspect:

DbContext

Migrations

Tracking

AsNoTracking

Lazy loading

N+1 queries

Indexes

Transactions

Unit of Work

Repositories

Performance

---

## Async

Review every async method.

Find:

missing async

blocking calls

.Result

.Wait()

ConfigureAwait issues

---

## Performance

Look for:

allocations
LINQ inefficiencies

multiple enumerations

boxing

reflection

large object allocations

caching opportunities

memory usage

---

## Security

Review:

Authentication

Authorization

Secrets

Connection strings

CORS

Cookies

CSRF

XSS

SQL Injection

File Uploads

Input validation

Rate limiting

Headers

Sensitive logging

HTTPS

---

## Testability

Review:

Dependency injection

Interfaces

Mockability

Static classes

Extension methods

Pure functions

Coupling

Suggest where unit tests are difficult.

---

## Debuggability

Review:

Logging

Exception messages

Method sizes

Magic values

Nested conditions

Readability

Diagnostic information

Breakpoints friendliness

Stack traces

---

## Maintainability

Evaluate:

Code organization

Class size

Method size

Cyclomatic complexity

Long parameter lists

Large constructors

God classes

Feature coupling

---

## Extensibility

Can new features be added easily?

What parts are tightly coupled?

Which abstractions are missing?

---

## Consistency

Inspect consistency of:

Naming

Formatting

Dependency Injection

DTOs

Async

Controllers

Repositories

Services

Error handling

Logging

Patterns

Coding style

Folder structure

---

## Code Smells

Find:

God Object

Long Method

Large Class

Shotgun Surgery

Feature Envy

Primitive Obsession

Magic Numbers

Temporary Fields

Duplicate Code

Data Clumps

Middle Man

Message Chains

Inappropriate Intimacy

Dead Code

---

## Atomicity

Review whether methods are atomic.

Identify methods doing multiple unrelated responsibilities.

Suggest decomposition.

---

## Readability

Evaluate:

Comments

Method names

Expression clarity

Variable names

Indentation

Control flow

Nested ifs

Early returns

Pattern matching opportunities

---

## Best Practices

Compare code against modern ASP.NET Core and C# best practices.

Recommend modern language features where beneficial.

Examples:

Primary Constructors

Collection Expressions

Required Members

File Scoped Namespaces

Records

Pattern Matching

Minimal APIs

Keyed Services

IOptions

Typed HttpClient

ProblemDetails

CancellationToken

---

# Severity Levels

Categorize every finding:

🔴 Critical

🟠 High

🟡 Medium

🟢 Low

---

# Positive Findings

Don't only criticize.

Mention good architectural decisions.

Explain why they are good.

---

# Technical Debt

Estimate overall technical debt.

Explain:

Current impact

Future impact

Estimated refactoring effort

---

# Refactoring Roadmap

Produce a prioritized roadmap.

Phase 1

Phase 2

Phase 3

Quick Wins

Long-term Improvements

---

# Final Summary

Include:

Overall score

Top 10 strengths

Top 10 weaknesses

Top 20 improvements

Risk assessment

Production readiness (percentage)

Maintainability score

Architecture maturity level

Estimated project maturity from 1 to 10.

---

# Output Format

Generate the report as BOTH:

1. A beautiful standalone HTML file.

Requirements:

* Modern responsive layout
* Dark mode
* Sticky sidebar with table of contents
* Collapsible sections
* Colored score cards
* Progress bars
* Severity badges
* Syntax-highlighted code snippets (if any)
* Professional typography
* Print-friendly
* No external CSS or JavaScript dependencies (everything embedded)

2. A Markdown (.md) version with the same content.

At the end, provide both files ready to save.
Language Requirement
Generate the entire report in Persian (Farsi).
Requirements:
Write all explanations, analyses, recommendations, strengths, weaknesses, and conclusions in fluent and professional Persian.
Use technical terms commonly used by Persian-speaking .NET developers. When necessary, include the original English term in parentheses (for example: تزریق وابستگی (Dependency Injection)).
Do not translate source code, class names, method names, namespaces, file names, or framework/library names.
All tables, headings, scores, summaries, and roadmap sections must also be written in Persian.
The generated HTML report must use RTL (right-to-left) layout with dir="rtl" and lang="fa".
Use a modern Persian font stack (such as Vazirmatn, IRANSans, or system fallbacks) and ensure proper spacing and typography for Persian text.
Numbers inside the report should be Persian digits unless they are part of source code or identifiers.
The final deliverables must be:
A standalone RTL HTML report.
A Markdown (.md) report in Persian.