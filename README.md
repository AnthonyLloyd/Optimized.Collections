# Optimized.Collections
[![build](https://github.com/AnthonyLloyd/Optimized.Collections/workflows/CI/badge.svg?branch=master)](https://github.com/AnthonyLloyd/Optimized.Collections/actions) [![nuget](https://buildstats.info/nuget/Optimized.Collections?includePreReleases=true)](https://www.nuget.org/packages/Optimized.Collections)

Optimized.Collections aims to provide drop in replacements for System.Collections with improved speed, memory or features in specific use cases.

# Vec\<T>, Set\<T> and Map\<K, V>

By removing the ability to Remove or Clear collections the following advantages are possible:

- Improved performance and reduced memory.
- An improved threading model where reads are lock free.
- Set\<T> and Map\<K, V> are also IReadOnlyList\<T> i.e. can be accessed by an index and passed as a list.

These can be particularly important in collection based logic and caches.

TODO
- Add struct Keys and Values to Map
- Expose VecLink\<T>.
- SetSync\<T> and MapSync\<T>.
- Vec1\<T>, Set1\<T>, Map1\<K, V> make a lot of sense and optimal for Set and Map as no Holder.Initial branch.
- Are SetLink\<T> or MapLink\<K, V> useful?
- Come back to Memo as a class

# Memo

A set of high performance single and multi-threaded memoize functions for Func\<T, R>, Func<Set\<T>, R[]>, Func<IReadOnlyCollection\<T>, R[]>, Func<Set\<T>, Task<R[]>> and Func<IReadOnlyCollection\<T>, Task<R[]>>.
