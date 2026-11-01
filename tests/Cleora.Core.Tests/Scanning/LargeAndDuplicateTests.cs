using Cleora.Core.Scanning;

namespace Cleora.Core.Tests.Scanning
{
    public class LargeAndDuplicateTests
    {
        private static void Write(string path, byte[] content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }

        private static byte[] Bytes(int length, byte fill)
        {
            var data = new byte[length];
            for (var i = 0; i < length; i++)
            {
                data[i] = (byte)(fill + i % 7);
            }

            return data;
        }

        [Fact]
        public void LargeFiles_KeepsTheBiggestAndSumsFirstLevelFolders()
        {
            using var s = new Scratch();
            s.File("D/Games/a.pak", 9000);
            s.File("D/Games/sub/b.pak", 3000);
            s.File("D/Videos/c.mp4", 7000);
            s.File("D/readme.txt", 10);

            var scan = LargeFileFinder.Find(Path.Combine(s.Root, "D"), top: 2, minBytes: 0);

            Assert.Equal(new[] { "a.pak", "c.mp4" }, scan.Files.Select(f => Path.GetFileName(f.Path)));
            Assert.Equal(19010, scan.TotalBytes);
            Assert.Equal(4, scan.FilesSeen);
            Assert.Equal("Games", Path.GetFileName(scan.Folders[0].Path));
            Assert.Equal(12000, scan.Folders[0].Bytes);
            Assert.Equal(2, scan.Folders[0].Files);
        }

        [Fact]
        public void Duplicates_SameContentGrouped_SameSizeDifferentContentNot()
        {
            using var s = new Scratch();
            var a = Path.Combine(s.Root, "Photos/a.jpg");
            var b = Path.Combine(s.Root, "Backup/a (1).jpg");
            var c = Path.Combine(s.Root, "Photos/c.jpg");
            Write(a, Bytes(5000, 1));
            Write(b, Bytes(5000, 1));
            Write(c, Bytes(5000, 2));

            var files = new FileWalker().Walk(s.Root, Array.Empty<string>(), true).ToList();
            var groups = DuplicateFinder.Find(files, minBytes: 1);

            var group = Assert.Single(groups);
            Assert.Equal(2, group.Files.Count);
            Assert.Equal(5000, group.Wasted);
            Assert.DoesNotContain(group.Files, f => f.Path == c);
        }

        [Fact]
        public void Duplicates_BigFilesDifferingOnlyInTheMiddle_AreNotGrouped()
        {
            using var s = new Scratch();
            var first = Bytes(400_000, 3);
            var second = (byte[])first.Clone();
            second[200_000] ^= 0xFF;
            Write(Path.Combine(s.Root, "x/one.bin"), first);
            Write(Path.Combine(s.Root, "x/two.bin"), second);
            Write(Path.Combine(s.Root, "y/three.bin"), first);

            var groups = DuplicateFinder.Find(new FileWalker().Walk(s.Root, Array.Empty<string>(), true), minBytes: 1);

            var group = Assert.Single(groups);
            Assert.Equal(new[] { "one.bin", "three.bin" }, group.Files.Select(f => Path.GetFileName(f.Path)).OrderBy(n => n));
        }

        [Fact]
        public void Duplicates_BelowMinimumSize_Ignored()
        {
            using var s = new Scratch();
            Write(Path.Combine(s.Root, "a.txt"), Bytes(100, 1));
            Write(Path.Combine(s.Root, "b.txt"), Bytes(100, 1));

            Assert.Empty(DuplicateFinder.Find(new FileWalker().Walk(s.Root, Array.Empty<string>(), true), minBytes: 1024));
        }

        [Fact]
        public void Deletable_EveryCopyTicked_StillKeepsOne()
        {
            var group = new DuplicateGroup(10, "h", new[]
            {
                new FoundFile(@"D:.jpg", 10, new DateTime(2020, 1, 1)),
                new FoundFile(@"D:.jpg", 10, new DateTime(2025, 1, 1)),
            });

            var all = DuplicateFinder.Deletable(group, new HashSet<string> { @"D:.jpg", @"D:.jpg" }, KeepRule.Newest);
            var one = DuplicateFinder.Deletable(group, new HashSet<string> { @"D:.jpg" }, KeepRule.Newest);

            Assert.Equal(new[] { @"D:.jpg" }, all.Select(f => f.Path));
            Assert.Equal(new[] { @"D:.jpg" }, one.Select(f => f.Path));
        }

        [Theory]
        [InlineData(KeepRule.Newest, @"D:\new\photo.jpg")]
        [InlineData(KeepRule.Oldest, @"D:\old\a\deep\photo.jpg")]
        [InlineData(KeepRule.ShortestPath, @"D:\new\photo.jpg")]
        public void Keep_PicksByRule(KeepRule rule, string expected)
        {
            var group = new DuplicateGroup(10, "h", new[]
            {
                new FoundFile(@"D:\old\a\deep\photo.jpg", 10, new DateTime(2020, 1, 1)),
                new FoundFile(@"D:\new\photo.jpg", 10, new DateTime(2025, 1, 1)),
                new FoundFile(@"D:\mid\x\photo.jpg", 10, new DateTime(2022, 1, 1)),
            });

            Assert.Equal(expected, DuplicateFinder.Keep(group, rule).Path);
        }
    }
}
