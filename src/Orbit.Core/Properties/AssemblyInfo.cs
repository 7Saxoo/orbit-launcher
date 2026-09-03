using System.Runtime.CompilerServices;

// The test project drives the internal ProcessLauncher constructor (its "OS seam")
// so launches can be verified without starting real processes.
[assembly: InternalsVisibleTo("Orbit.Tests")]
