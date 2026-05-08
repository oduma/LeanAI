# Testing & TDD Workflow

## 1. The TDD Cycle (Red-Green-Refactor)
1. Write a failing unit test for a specific branch of logic.
2. Write the minimum code necessary to pass the test.
3. Refactor for quality while keeping the test green.

## 2. Coverage Requirements
- **100% Branch Coverage:** Non-negotiable for all business logic (Domain & Application). For every `if/else`, switch case, or null check, a test case MUST exist.
- **Logic Isolation:** Only test the code in the current layer. Mock dependencies using `Moq` or `NSubstitute`.

## 3. Assertions
- Use `FluentAssertions` for readable, descriptive test failures.
- Every test must follow the AAA pattern:
    - **Arrange:** Set up the context.
    - **Act:** Execute the method.
    - **Assert:** Verify the outcome.