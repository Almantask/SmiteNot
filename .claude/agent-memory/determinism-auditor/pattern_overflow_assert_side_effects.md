---
name: pattern-overflow-assert-side-effects
description: Recurring Fix/integer pitfalls to check first - division asserts that throw on long.MinValue / -1, guard ordering inside Debug.Assert, runtime-dependent MinValue/-1 behaviour (IL2CPP)
metadata:
  type: project
---

Check first in any Fix / integer-math change:
- `x / -1` with x == long.MinValue: .NET/Mono throw OverflowException; IL2CPP compiles to C++ where it is UB (SIGFPE on x86). Any division whose divisor can be -1 needs an explicit guard, including divisions *inside* Debug.Assert overflow checks (`product / a == b` style).
- In `A && B` assert conditions, the guard that prevents the throwing division must come first (found in Fix operator* on 2026-11-01: `product / a.Raw == b.Raw && !(a.Raw == -1 && ...)` evaluates the division first).
- Debug.Assert is [Conditional("DEBUG")], so any exception thrown by the assert expression is a Debug-vs-Release behaviour difference.
- Signed overflow and left shift of negatives are UB in C++ before C++20; IL2CPP output relies on compiler behaviour. Note as Low, not a blocker.
- New files in sim/src need their Unity .meta files committed (stable GUIDs); sim/src is also a Unity package.

**Why:** These came up in the first Fix.cs audit. Tests in the safe range don't hit them.
**How to apply:** grep new code for `/` and `%` on long/int and check the MinValue / -1 path and the operand order inside asserts.
