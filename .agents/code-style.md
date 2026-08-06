# Code Style

This file applies to the entire repository and supplements the coding guidelines in the root `AGENTS.md`.

## File Encoding And Line Endings

- Use UTF-8 without a byte order mark (BOM) and LF line endings for all text files.

## Tabs, Indents, Alignment

- None

## Naming

- None

## Syntax Style

- Keep the `public` accessibility modifier explicit on interface members.
- Use conventional constructors with explicit backing fields for injected dependencies; do not convert these types to primary constructors.
- Keep short data-only contracts as positional records. Add `init` properties when optional collection values should remain available through object-initializer syntax.
- Use block bodies for methods, including methods whose implementation is a single statement. Reserve expression bodies for concise non-method members such as simple computed properties.
- Use an explicit local type when a value comes from a method call, property, deserialization, or other expression whose result type is not visible at the assignment. Use `var` when the initializer names the constructed type, for anonymous types, and for assertion results whose exact type is evident from the assertion.

## Braces Layout

- None

## Blank Lines

- Leave a blank line immediately before a `return` statement when other statements precede it in the same code block. A standalone `return` statement does not require a preceding blank line.
- Separate branching statements such as `if` and `switch` from surrounding statements with a blank line both before and after the complete branching construct. Keep connected clauses such as `else if` and `else` together with their preceding clause.
- Keep consecutive argument, dependency, and state checks together as one validation group, then leave a blank line before the method's main logic.
- Leave a blank line between consecutive statements that invoke methods, including calls used in assignments. Consecutive calls in a validation group are the exception.

## Line Breaks And Wrapping

- Apply the 120-character line limit only to source code, not to documentation, configuration, or data files.
- Limit source-code lines to 120 character, not document or other files.
- Keep a declaration or invocation parameter list on one line when the complete line is 120 characters or fewer.
- Wrap parameters or arguments only when the complete line would exceed 120 characters. Break at parameter or argument boundaries rather than wrapping short lists preemptively.

## Spaces

- None

## Null Checking

- None

## XML Documentation

- None

## File Layout

- Order file-level declarations as follows: `using` directives, the namespace declaration, simple records/enums/interfaces/structs that are not split into separate files, and then classes.
- Within a class, order declarations as follows: properties, public non-method members, private non-method members, constants, constructors, public methods, and private methods.
- Do not modify project files or add an `AssemblyInfo` file solely to expose code to tests.

## Tests

- Name tests `Member_WhenCondition_ExpectedBehavior`; omit the condition segment only when there is no meaningful precondition.
- Structure tests as arrange, act, and assert sections separated by blank lines. Do not add `Arrange`, `Act`, or `Assert` comments when the sections are already clear.
- Use xUnit's built-in `Assert` APIs and capture expected exceptions in a local before asserting on their messages.
