using Xunit;

// The no-write test redirects TMPDIR so it can watch the relay's temp directory, and TMPDIR is
// process-wide. Running one test at a time makes that safe; the suite is a couple of seconds, so
// the cost is nothing and the alternative is a watcher that sees other tests' files and has to
// filter them out — and filtering is how a no-write check quietly stops being one.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
