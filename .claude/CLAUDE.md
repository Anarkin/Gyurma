# CLAUDE.md

## important

- When asked to explore or understand the codebase, always read actual source files before providing any summary. Never guess or infer project purpose from surface-level information like file names or README alone.

- After making code changes, always build and run all tests before reporting completion. If tests fail, fix them before presenting results. Do not leave failing tests for the user to discover.

- When modifying contracts, interfaces, abstract classes, or source generator output: verify that no unused/redundant members remain after changes. Check that generated code matches the current interface/abstract class contracts.

## when I ask you to execute an order, do the following:

- "order 1": "please fully understand the existing repo, including the documentation, context, requirements, code base before proceeding"

- "order 66": "please thoroughly check that the documents are in line with the implementation"

- "order 67": "please thoroughly check that the implementation has no bugs"

- "order 68": "please thoroughly check for clean code improvement opportunities"

- "order 69": "please thoroughly check for performance improvement opportunities"

