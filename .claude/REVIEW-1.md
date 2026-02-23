# Gyurma Codebase Review -- Consolidated Findings

## 1. Domain Design (domain-reviewer)

**Overall: Strong.** The structural mirroring pattern (`Setup` / `CallCounts` reflecting the original type's members) is the standout design choice -- highly discoverable and intuitive.

### Strengths
- Clean contract separation: `IMethodSetup<T>` (with `Returns` + `Throws`) vs `IVoidMethodSetup` (only `Throws`)
- Terminal API (`Returns`/`Throws` return `void`) prevents confusing chains
- `CallCounts` with standard assertions is a sound alternative to `Verify()`
- Naming is consistent and distinctive

### Missing Domain Concepts (ranked)

| Priority | Feature | Notes |
|----------|---------|-------|
| **High** | **Async support** (`ReturnsAsync`) | Users must write `Returns(Task.FromResult(x))` today |
| **High** | **Callback returns** (`Returns(Func<T>)`) | Enables dynamic/sequence returns; low-cost to add |
| **Medium** | Argument matchers (`It.IsAny<T>()`) | Deliberate omission, but should be documented as a design principle |
| **Medium** | `Throws<T>(T instance)` overload | Can't set exception messages currently |
| **Low** | Reset/Clear API, event support, protected members |

---

## 2. Documentation vs Implementation Alignment (discrepancy-reviewer)

**Overall: All documented features are implemented. All documented behaviors are correct.**

### Documentation Gaps (implemented but undocumented)
- Init-only properties (`{ get; init; }`)
- Interface inheritance (simple, multi-level, diamond, generic)
- Abstract class deep hierarchy support (virtual overrides, sealed, base constructor)
- Method overloading
- `GYURMA001` diagnostic for missing parameterless constructors
- Duplicate `[assembly: Gyurma]` deduplication

### Factual Inaccuracy
- CONCEPT.md says "reference types are compared by references" -- **actually uses `EqualityComparer<T>.Default`** which calls `Equals()`/`GetHashCode()`. Types like `string` get value equality, not reference equality.

---

## 3. Performance (perf-reviewer)

### High Impact

| # | Finding | Severity |
|---|---------|----------|
| 1 | **`Collect()` defeats incremental caching** -- all mocks regenerate when any `[assembly: Gyurma]` attribute changes | **HIGH** |

### Low Impact (easy fixes)

| # | Finding |
|---|---------|
| 2 | `DiagnosticDescriptor` should be `static readonly` field instead of allocated per error |
| 3 | Indexer call count code doesn't cache key in `var __key` (inconsistent with methods -- evaluates tuple twice) |

### Acceptable / By Design
- Double dictionary lookup for call count increment (test code, negligible impact)
- Generic method returns box value types through `Func<object?>` (test code)
- LINQ in generator is on cold paths (once per type at compile time)
- `Dictionary<>` is correct choice over `ConcurrentDictionary<>` for test mocks

---

## 4. Bugs & Test Coverage (test-bug-reviewer)

### Bugs

| # | Bug | Severity | Status |
|---|-----|----------|--------|
| 1 | **Null keys in Dictionary cause `ArgumentNullException`** | **HIGH** | **NEW** -- `Setup.Format(null)` crashes |
| 2 | `ref`/`out`/`in` parameter modifiers not emitted -> CS0535 | Medium | Known, confirmed still present |
| 3 | `params` modifier not emitted | Low | **NEW** |
| 4 | `new` with different return type unsupported | Low | Known limitation, confirmed |

### Test Coverage Gaps (13 total, top items)

| Priority | Gap |
|----------|-----|
| **High** | No tests for `ref`/`out`/`in`/`params` parameters |
| **Medium** | No tests for null argument values (would reveal Bug 1) |
| **Medium** | No tests for abstract class properties/indexers |
| **Medium** | No tests for call counts on abstract class members |
| **Medium** | No tests for overloaded method call counts |
| Low | No tests for multi-param indexers, nested generic types, empty interfaces, various generic constraints, 3+ level class hierarchies |

---

## Top Actionable Items

1. **Fix null key bug** -- use a null-safe wrapper or sentinel value for dictionary keys
2. **Refactor incremental pipeline** -- replace `Collect()` with per-item `RegisterSourceOutput`
3. **Emit `ref`/`out`/`in`/`params` modifiers** in generated parameter lists
4. **Update CONCEPT.md** -- document init properties, inheritance, overloads, diagnostics; fix the reference equality claim
5. **Add `ReturnsAsync` / `Returns(Func<T>)` overloads** for ergonomic async and dynamic setup
6. **Add tests for null args, abstract class properties/indexers, and overload call counts**
