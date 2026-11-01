using Cleora.Core.Cleanup;

namespace Cleora.Core.Tests.Cleanup
{
    public class PathGuardTests
    {
        private static readonly string Base = Path.Combine(Path.GetTempPath(), "cleora-guard");

        [Fact]
        public void IsInside_FileBelowRoot_True()
        {
            Assert.True(PathGuard.IsInside(Path.Combine(Base, "Temp", "a", "b.tmp"), Path.Combine(Base, "Temp")));
        }

        [Theory]
        [InlineData("Temp2/b.tmp")]
        [InlineData("Temp")]
        [InlineData("Temp/../Documents/cv.docx")]
        [InlineData("Other/b.tmp")]
        public void IsInside_SiblingTheRootItselfOrEscape_False(string relative)
        {
            Assert.False(PathGuard.IsInside(Path.Combine(Base, relative), Path.Combine(Base, "Temp")));
        }

        [Fact]
        public void IsSafeRoot_ProtectedOrShallowFolderTakingEverything_False()
        {
            using var scratch = new Scratch();
            var f = scratch.Folders;

            Assert.False(PathGuard.IsSafeRoot(CleanupRoot.All(f.Windows), f));
            Assert.False(PathGuard.IsSafeRoot(CleanupRoot.All(f.UserProfile), f));
            Assert.False(PathGuard.IsSafeRoot(CleanupRoot.All(f.LocalAppData), f));
            Assert.False(PathGuard.IsSafeRoot(CleanupRoot.All(Path.Combine(f.Windows, "System32")), f));
            Assert.False(PathGuard.IsSafeRoot(CleanupRoot.All(Path.GetPathRoot(f.Windows)!), f));
            Assert.False(PathGuard.IsSafeRoot(CleanupRoot.Only(f.Windows, true, "*"), f));
        }

        [Fact]
        public void IsSafeRoot_CacheFolderOrNamedFilesInWindows_True()
        {
            using var scratch = new Scratch();
            var f = scratch.Folders;

            Assert.True(PathGuard.IsSafeRoot(CleanupRoot.All(f.Temp), f));
            Assert.True(PathGuard.IsSafeRoot(CleanupRoot.All(Path.Combine(f.Windows, "SoftwareDistribution", "Download")), f));
            Assert.True(PathGuard.IsSafeRoot(CleanupRoot.Only(f.Windows, true, "MEMORY.DMP"), f));
        }

        [Fact]
        public void WindowsOld_OnARealDriveRoot_IsNotASafeFileRoot()
        {
            using var scratch = new Scratch();
            var drive = Path.GetPathRoot(Path.GetTempPath())!;

            Assert.False(PathGuard.IsSafeRoot(CleanupRoot.All(Path.Combine(drive, "Windows.old")), scratch.Folders));
        }

        [Fact]
        public void Catalog_EveryRootIsSafe()
        {
            using var scratch = new Scratch();
            CatalogTests.FakeMachine(scratch);

            var roots = TargetCatalog.Build(scratch.Folders).Where(t => t.Kind == TargetKind.Files).SelectMany(t => t.Roots).ToList();

            Assert.NotEmpty(roots);
            Assert.All(roots, r => Assert.True(PathGuard.IsSafeRoot(r, scratch.Folders), r.Path));
        }
    }
}
