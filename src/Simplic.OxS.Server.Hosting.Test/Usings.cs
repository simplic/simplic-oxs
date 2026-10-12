global using Xunit;
global using FluentAssertions;

// MongoDB class maps and serializers are registered per process, and every test here starts a
// host that registers some: the tests run one after another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
