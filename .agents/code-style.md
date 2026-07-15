# Code Style

This file applies to the entire repository and supplements the coding guidelines in the root `AGENTS.md`.

## Accessibility

- Keep the `public` accessibility modifier explicit on interface members.
- If a class or method needs to be accessed directly by tests, make it `public`.
- Do not modify project files or add an `AssemblyInfo` file solely to expose code to tests.
