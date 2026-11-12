using System;
using System.IO;
using System.Text;
using Cuyahoga.Core.Service.Files;
using Xunit;

namespace Cuyahoga.Core.Tests.Service
{
	public sealed class FileServiceTests : IDisposable
	{
		private readonly TempDirectory _files = new TempDirectory();
		private readonly TempDirectory _temp = new TempDirectory();

		public void Dispose()
		{
			_files.Dispose();
			_temp.Dispose();
		}

		private static Stream Contents(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

		[Fact]
		public void FileWriter_writes_the_file_on_commit()
		{
			var writer = new FileWriter(_temp.Path);
			string target = _files.File("upload.txt");

			writer.CreateFromStream(target, Contents("hello"));
			Assert.False(File.Exists(target));
			Assert.True(File.Exists(_temp.File("upload.txt")));

			writer.Commit();
			Assert.Equal("hello", File.ReadAllText(target));
			Assert.False(File.Exists(_temp.File("upload.txt")));
		}

		[Fact]
		public void FileWriter_removes_the_temp_file_on_rollback()
		{
			var writer = new FileWriter(_temp.Path);
			string target = _files.File("upload.txt");

			writer.CreateFromStream(target, Contents("hello"));
			writer.Rollback();

			Assert.False(File.Exists(target));
			Assert.False(File.Exists(_temp.File("upload.txt")));
		}

		[Fact]
		public void FileWriter_deletes_on_commit_only()
		{
			string existing = _files.File("old.txt");
			File.WriteAllText(existing, "old");
			var writer = new FileWriter(_temp.Path);

			writer.DeleteFile(existing);
			Assert.True(File.Exists(existing));

			writer.Commit();
			Assert.False(File.Exists(existing));
		}

		[Fact]
		public void FileWriter_checks_the_files()
		{
			string existing = _files.File("exists.txt");
			File.WriteAllText(existing, "x");
			var writer = new FileWriter(_temp.Path);

			Assert.Throws<ArgumentException>(() => writer.CreateFromStream(existing, Contents("y")));
			Assert.Throws<DirectoryNotFoundException>(() => writer.CreateFromStream(Path.Combine(_files.Path, "missing", "a.txt"), Contents("y")));
			Assert.Throws<FileNotFoundException>(() => writer.DeleteFile(_files.File("missing.txt")));
		}

		[Fact]
		public void Without_a_transaction_the_file_service_writes_and_deletes_immediately()
		{
			var fileService = new TransactionalFileService { TempDir = _temp.Path };
			string target = _files.File("direct.txt");

			fileService.WriteFile(target, Contents("direct"));
			Assert.Equal("direct", File.ReadAllText(target));

			fileService.DeleteFile(target);
			Assert.False(File.Exists(target));
		}
	}
}
