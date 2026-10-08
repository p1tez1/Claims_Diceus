# AI interaction log

This directory is reserved for the human-authored evidence required by D2 sections 4.5 and 4.6. Do not reconstruct or invent missing conversations from code.

Before submission, add:

1. The prompts actually used, preferably one Markdown/text export per numbered task or phase. Preserve prompt wording, relevant follow-ups, and dates.
2. The AI tool and model/tier for each prompt when known.
3. At least three representative prompts, with a short note describing their purpose and the resulting code/docs/tests.
4. At least two concrete mistakes or suboptimal outputs: what the AI produced, how the issue was detected, what the human changed, and the test/review evidence for the correction.
5. A short honest assessment of where AI helped and where it required the most supervision.
6. A link to the exported Codex conversation history, or the sanitized export itself (text, Markdown, PDF, or screenshots).

A useful per-prompt file shape is:

```text
# Prompt <number>: <title>

- Date:
- Tool:
- Model/tier:
- Purpose:
- Related files or commit:

## Prompt
<verbatim prompt>

## Follow-ups and response evidence
<export or link>

## Human review and corrections
<what was accepted, changed, or rejected and how it was verified>
```

Do not commit API keys, passwords, connection strings, tokens, publish profiles, private customer data, or unrelated personal information. If a full export contains secrets, create a sanitized copy and state that it was redacted rather than silently altering the technical sequence.

After adding evidence, replace every `<<HUMAN TO FILL: ...>>` marker in the root `AI-WORKFLOW.md` with truthful content and working links.
