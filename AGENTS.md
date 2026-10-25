# Code style

Codex must apply these rules to all new code it writes in this repository and to any existing code it materially edits:

- Put braces around every `if`, `else`, `for`, `foreach`, `while`, and other control-flow body, including one-line bodies.
- Separate nested class declarations from the preceding member with a blank line.
- Separate every method from the fields, properties, or methods before it with a blank line; keep any attributes or documentation comments attached to that method.
- Leave a blank line before each `if`, `for`, `foreach`, `while`, and `try` statement unless it is the first statement in a method.
- Leave a blank line before every `return` statement, except when it is the only statement in an `if` body.
- In object, anonymous-type, and collection initializers, put the opening `{` on its own line and put each property or item on its own line.
- Use normal spacing and line breaks to keep one logical step per line. Separate logical sections with blank lines, and wrap long parameter lists, calls, and initializers across lines when that improves scanning.
- Use descriptive loop counter names, such as `index`, instead of single-letter names like `i`.
- Use descriptive names for local variables and parameters; prefer names like `portfolio` or `holding` over single-letter or opaque names like `p` or `h`.
- Add concise comments where they explain why code is structured a certain way, a non-obvious constraint, or an important recovery/ordering guarantee. Do not add comments that only repeat what the code already says.

Follow `.editorconfig` for whitespace and brace enforcement. Apply every rule above to code you add or touch without reformatting unrelated code.

## Service-Repository pattern

- Repositories own database CRUD and persistence queries. Put repository implementations under `Services/Repositories`.
- Services own business rules, validation, calculations, composition, and workflow orchestration. Name business-logic classes `...Service` and have them call repositories for data access.
- Keep EF Core dependencies inside the persistence layer. Services and strategy workflows must not inject `DatabaseContext` or `IDbContextFactory<DatabaseContext>`, use `DbSet` or EF query extensions, or call `SaveChangesAsync` directly.
- Repositories own context lifetimes, tracking, database entity mapping, and persistence details such as document keys and payload serialization. Expose materialized results or explicit data contracts; do not expose `IQueryable`, EF tracking objects, or a context that callers must save.
- Services decide whether an operation is allowed and what changes it requires. Repositories load and persist those changes. Do not move an entire business workflow into a repository merely to remove EF imports from a service.
- Repository methods may encapsulate atomic conditional writes and replay-marker checks required for persistence correctness. Keep business policy in services while preserving concurrency checks and saving each document's changes together with its idempotency marker.
- Preserve commit boundaries, write ordering, and retry behavior when extracting persistence code. For strategy trades, preserve the existing cash -> transaction -> holding -> original-purchase sequence and recovery after each committed document; do not combine saves without verifying the storage guarantees.
- Reuse or extend existing services and repositories before adding parallel abstractions. Portfolio and holding operations belong in `PortfolioService` / `PortfolioRepository`; transaction operations belong in `TransactionService` / `TransactionRepository`. Shared strategy persistence must accept strategy identity as data rather than hard-code NasdaqBounce or another strategy.
- Apply the interface and contract placement rules below when introducing repository or service abstractions. Keep EF types out of shared contracts.
- Test business rules through service dependencies that can be substituted without EF. Verify repository persistence and recovery behavior with focused persistence tests, including retries after partial commits where applicable.
- Existing classes that mix these responsibilities are refactoring candidates, not examples to copy. Follow the separation used by `NasdaqBounceService` / `NasdaqBounceRepository` and the strategy trade methods in `TransactionService`, `TransactionRepository`, and `PortfolioRepository`.

## Shared interfaces and strategy abstractions

- Define each interface in its own file under the `Shared` project, in `Shared/Interfaces`; do not group multiple interfaces in a contracts file or define interfaces beside their implementations.
- Put contracts that are used across project boundaries in `Shared`, and keep service implementations in `Services`.
- Keep strategy-specific interfaces and data contracts in strategy-named namespaces under `Shared`; put behavior or contracts that apply to every strategy in strategy-neutral `Shared` namespaces and accept strategy identity as data rather than hard-coding a strategy name.
- Define enums in their own files under `Shared/Enums`; place NasdaqBounce `SupportAction` in `Shared/Enums/SupportAction.cs`.
