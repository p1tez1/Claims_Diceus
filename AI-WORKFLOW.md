# AI-assisted workflow report

This is an evidence-based scaffold for D2 sections 4.5 and 4.6. Repository facts are filled in. Personal experience, exact prompts, model selections, corrections, and conversation links are deliberately left for the human author where no `docs/ai-log/` evidence existed at the time this file was created.

## Tools used

- OpenAI Codex was used for repository work. This is verifiable from the repository's `AGENTS.md` workflow and the Codex task context in which the implementation documentation was produced.
- `<<HUMAN TO FILL: List every other AI tool used, or write "None". Include editor/CLI/app names and versions when known.>>`
- `<<HUMAN TO FILL: Record the Codex model name/tier used for each major prompt or phase. No model-tier record was found in docs/ai-log/.>>`

## Workflow structure

The repository records a staged, specification-driven workflow rather than a single unconstrained generation pass:

1. Load `AGENTS.md` for precedence, architecture boundaries, package policy, verification, and reporting rules.
2. Read D1 (`docs/spec/spec-1-functional.md`) for functional behavior and D2 (`docs/spec/spec-2-assessment.md`) for stack/deliverables.
3. Read `docs/DESIGN-DECISIONS.md` and its addendum. Those files explicitly label user-supplied decisions, disagreements with the specifications, derived behavior, and resolved/open questions.
4. Inspect existing architecture, analogous code, tests, and dependencies before editing; trace each flow from API through Application/Domain to Persistence/Infrastructure.
5. Implement in bounded prompts/phases, add focused tests, run build/test verification, and write topic evidence under `docs/`.
6. Review the integrated backend and deployment/frontend artifacts, recording remaining limitations rather than describing missing work as complete.

The checked-in documentation references staged work including Prompt 4 (Domain), Prompt 5 (Persistence), Prompt 8 (claims core), Prompt 10 (background jobs), Prompt 11 (reserves), and Prompt 14 (backend checkpoint). These references verify sequencing labels, not the exact text of those prompts.

### Context strategy

The recurring context set was:

- Source precedence and coding constraints from `AGENTS.md`.
- Functional rules from relevant D1 sections.
- technology/deliverable requirements from relevant D2 sections.
- explicit DD-xx decisions before implementation.
- current code, migrations, tests, review notes, infrastructure, and workflows as the reality check.

The decision documents preserve conflicts instead of silently normalizing them. Examples visible in the repository include the signed-delta reserve ledger, gap-free transactional claim numbers, pre-commit audit events versus post-commit Hangfire enqueue, SLA timestamp rather than status, POST-only idempotency, shadow tenant/audit properties, and standalone Angular routes.

### Iteration and verification approach

Repository evidence shows tests split across Domain, Application, and SQL/Azurite-backed Integration projects, plus Angular unit/build and a typed-HTTP-boundary check. Topic documents record command lines and historical outcomes. The current CI repeats backend build/tests and frontend tests/build; the deployment workflow also generates and applies an idempotent migration script.

`<<HUMAN TO FILL: Describe how you personally reviewed AI output between prompts—examples might include diff review, debugger/Swagger checks, database inspection, or rewriting parts by hand. Include only actions you actually performed.>>`

## Representative prompts

D2 requires at least three representative prompt examples. No prompt log was present under `docs/ai-log/`, and prompt wording cannot be reconstructed honestly from filenames or implementation artifacts.

### Prompt example 1

`<<HUMAN TO FILL: Paste the exact or faithfully exported prompt. State its purpose and identify the resulting files/commit/task.>>`

### Prompt example 2

`<<HUMAN TO FILL: Paste the exact or faithfully exported prompt. State its purpose and identify the resulting files/commit/task.>>`

### Prompt example 3

`<<HUMAN TO FILL: Paste the exact or faithfully exported prompt. State its purpose and identify the resulting files/commit/task.>>`

Add further examples if they better demonstrate planning, implementation, verification, or review.

## AI-generated versus manually designed or refined

Verifiable repository facts:

- The DD-01 through DD-40 material is explicitly described as user-supplied/accepted decisions. It defines important behavior before implementation, including conflict resolutions and tradeoffs.
- `AGENTS.md` supplies human-controlled source precedence, dependency arrows, package constraints, verification commands, and the eight-item reporting contract.
- The implementation and tests are organized around those constraints, with prompt-specific evidence documents and a later integrated backend checkpoint.
- The repository alone does not prove line-by-line authorship or which edits were typed, accepted, rewritten, or rejected by a human.

`<<HUMAN TO FILL: Explain what AI generated (for example scaffolding, implementation drafts, tests, documentation, review) and what you manually designed or materially refined. Be precise and do not claim manual work that was not performed.>>`

## Corrections of incorrect or suboptimal AI output

D2 requires at least two specific examples. The repository contains final decisions and review findings, but without prompt/conversation logs it cannot prove that a particular defect came from AI or how the human corrected it.

### Correction 1

`<<HUMAN TO FILL: Describe a specific AI mistake/suboptimal proposal, how you detected it, the correction you made, and the verification that proved the correction. Link to the relevant prompt log and code/test.>>`

### Correction 2

`<<HUMAN TO FILL: Describe a different specific AI mistake/suboptimal proposal, how you detected it, the correction you made, and the verification that proved the correction. Link to the relevant prompt log and code/test.>>`

Optional additional corrections can be added when they show meaningful critical review.

## Honest assessment

`<<HUMAN TO FILL: Give your personal assessment of which parts benefited most from AI, where AI was least reliable, how much review/rework was needed, and what you would change in a future AI-assisted project. This must be written by the person who performed the work.>>`

## Interaction history

No exported conversation, share link, transcript, PDF, or screenshots were present under `docs/ai-log/` when this scaffold was created.

`<<HUMAN TO FILL: Add the repository-relative path or accessible share/export link for the substantive OpenAI Codex conversation history required by D2 section 4.6. Remove secrets and personal data before submission, but preserve enough context to show the real workflow.>>`

See `docs/ai-log/README.md` for the expected evidence format.
