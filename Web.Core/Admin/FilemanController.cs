using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Admin
{
	/// <summary>
	/// The server side of the file manager of CKEditor (RoxyFileman in ~/Support/fileman; asp_net/main.ashx of the
	/// WebForms site): browse, upload, copy, move, rename and delete the files under FILES_ROOT (conf.json), for
	/// administrators and editors. Without thumbnails (GENERATETHUMB is off, the file manager shows file type icons) and
	/// without resizing uploaded images.
	/// </summary>
	public class FilemanController : Controller
	{
		private const string FilemanDir = "~/Support/fileman/";
		private static readonly string[] AllowedRoles = { "Administrator", "Editor" };

		private Dictionary<string, string> _settings;
		private Dictionary<string, string> _texts;

		[HttpGet]
		[HttpPost]
		[Route("Support/fileman/asp_net/main.ashx")]
		public async Task<IActionResult> Index(string a)
		{
			await HttpContext.AuthenticateAsync(LegacyAuthenticationHandler.SchemeName);
			User user = LegacyAuthenticationHandler.GetUser(HttpContext);
			if (user == null || !AllowedRoles.Any(user.IsInRole))
			{
				return StatusCode(StatusCodes.Status403Forbidden);
			}

			string action = (a ?? "DIRLIST").ToUpperInvariant();
			try
			{
				if (String.IsNullOrEmpty(GetSetting(action)))
				{
					throw new InvalidOperationException(Text("E_ActionDisabled"));
				}
				return action switch
				{
					"DIRLIST" => Html(ListDirTree(Param("type"))),
					"FILESLIST" => Html(ListFiles(Param("d"), Param("type"))),
					"COPYDIR" => Html(CopyDir(Param("d"), Param("n"))),
					"COPYFILE" => Html(CopyFile(Param("f"), Param("n"))),
					"CREATEDIR" => Html(CreateDir(Param("d"), Param("n"))),
					"DELETEDIR" => Html(DeleteDir(Param("d"))),
					"DELETEFILE" => Html(DeleteFile(Param("f"))),
					"DOWNLOAD" => Download(Param("f")),
					"MOVEDIR" => Html(MoveDir(Param("d"), Param("n"))),
					"MOVEFILE" => Html(MoveFile(Param("f"), Param("n"))),
					"RENAMEDIR" => Html(RenameDir(Param("d"), Param("n"))),
					"RENAMEFILE" => Html(RenameFile(Param("f"), Param("n"))),
					"UPLOAD" => Html(Upload(Param("d"))),
					_ => Html(ErrorResult("This action is not implemented."))
				};
			}
			catch (Exception ex)
			{
				return action == "UPLOAD"
					? Html("<script>parent.fileUploaded(" + ErrorResult(Text("E_UploadNoFiles") + " " + ex.Message) + ");</script>")
					: Html(ErrorResult(ex.Message));
			}
		}

		private string Param(string name)
		{
			string value = Request.Query[name];
			if (value == null && Request.HasFormContentType)
			{
				value = Request.Form[name];
			}
			return value;
		}

		private ContentResult Html(string content) => Content(content, "text/html; charset=utf-8");

		#region Settings, texts and paths

		private string GetSetting(string name)
		{
			this._settings ??= ReadJson("conf.json");
			return this._settings.TryGetValue(name, out string value) ? value : String.Empty;
		}

		private string Text(string name)
		{
			this._texts ??= ReadJson("lang/en.json");
			return this._texts.TryGetValue(name, out string value) ? value : name;
		}

		private static Dictionary<string, string> ReadJson(string file)
		{
			try
			{
				string json = System.IO.File.ReadAllText(ApplicationPaths.MapPath(FilemanDir + file), Encoding.UTF8);
				return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
			}
			catch (Exception)
			{
				return new Dictionary<string, string>();
			}
		}

		/// <summary>
		/// The physical path of a path of the site (/UserFiles/images).
		/// </summary>
		private static string MapPath(string path)
		{
			return Path.GetFullPath(ApplicationPaths.MapPath("~/" + (path ?? String.Empty).TrimStart('~', '/')));
		}

		private string FilesRoot => MapPath(GetSetting("FILES_ROOT"));

		/// <summary>
		/// The physical path of a path in the files root; anything outside it is denied.
		/// </summary>
		private string CheckPath(string path)
		{
			if (String.IsNullOrEmpty(path))
			{
				throw new InvalidOperationException("Access denied");
			}
			string fullPath = MapPath(path).TrimEnd(Path.DirectorySeparatorChar);
			string root = FilesRoot.TrimEnd(Path.DirectorySeparatorChar);
			// The root itself or below it (with the separator: UserFilesX isn't below UserFiles).
			if (!fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
				&& !fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Access to " + path + " is denied");
			}
			return fullPath;
		}

		/// <summary>
		/// New file and directory names are plain names without path parts.
		/// </summary>
		private static void CheckName(string name)
		{
			if (String.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name.Trim() == "." || name.Trim() == ".."
				|| name.IndexOfAny(Path.GetInvalidFileNameChars()) > -1)
			{
				throw new InvalidOperationException("Invalid name: " + name);
			}
		}

		private bool CanHandleFile(string fileName)
		{
			string extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
			bool allowed = false;
			string forbidden = GetSetting("FORBIDDEN_UPLOADS").Trim().ToLowerInvariant();
			if (forbidden != String.Empty)
			{
				allowed = !Regex.Split(forbidden, "\\s+").Contains(extension);
			}
			string allowedExtensions = GetSetting("ALLOWED_UPLOADS").Trim().ToLowerInvariant();
			if (allowedExtensions != String.Empty && !Regex.Split(allowedExtensions, "\\s+").Contains(extension))
			{
				allowed = false;
			}
			return allowed;
		}

		private static string FileType(string extension)
		{
			return extension.ToLowerInvariant() switch
			{
				".jpg" or ".jpeg" or ".png" or ".gif" => "image",
				".swf" or ".flv" => "flash",
				_ => "file"
			};
		}

		private static string Result(string type, string message)
		{
			return "{\"res\":\"" + type + "\",\"msg\":\"" + message.Replace("\\", "\\\\").Replace("\"", "\\\"")
				.Replace("\r", "").Replace("\n", " ") + "\"}";
		}

		private static string SuccessResult(string message = "") => Result("ok", message);

		private static string ErrorResult(string message) => Result("error", message);

		#endregion

		#region Directories

		private string ListDirTree(string type)
		{
			var root = new DirectoryInfo(FilesRoot);
			if (!root.Exists)
			{
				// The files root isn't part of the source tree: it is made on first use.
				try
				{
					root.Create();
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException(
						"Invalid files root directory. Check your configuration and permissions: " + ex.Message);
				}
			}
			var directories = new List<string> { root.FullName };
			directories.AddRange(Directory.GetDirectories(root.FullName, "*", SearchOption.AllDirectories)
				.OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase));
			string siteRoot = MapPath(String.Empty);
			return "[" + String.Join(",", directories.Select(directory =>
				"{\"p\":\"/" + Path.GetRelativePath(siteRoot, directory).Replace('\\', '/') + "\",\"f\":\""
				+ GetFiles(directory, type).Count.ToString(CultureInfo.InvariantCulture) + "\",\"d\":\""
				+ Directory.GetDirectories(directory).Length.ToString(CultureInfo.InvariantCulture) + "\"}")) + "]";
		}

		private string CreateDir(string path, string name)
		{
			string directory = CheckPath(path);
			CheckName(name);
			if (!Directory.Exists(directory))
			{
				throw new InvalidOperationException(Text("E_CreateDirInvalidPath"));
			}
			try
			{
				Directory.CreateDirectory(Path.Combine(directory, name));
			}
			catch (Exception)
			{
				throw new InvalidOperationException(Text("E_CreateDirFailed"));
			}
			return SuccessResult();
		}

		private string DeleteDir(string path)
		{
			string directory = CheckPath(path);
			if (!Directory.Exists(directory))
			{
				throw new InvalidOperationException(Text("E_DeleteDirInvalidPath"));
			}
			if (directory.Equals(FilesRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(Text("E_CannotDeleteRoot"));
			}
			if (Directory.EnumerateFileSystemEntries(directory).Any())
			{
				throw new InvalidOperationException(Text("E_DeleteNonEmpty"));
			}
			try
			{
				Directory.Delete(directory);
			}
			catch (Exception)
			{
				throw new InvalidOperationException(Text("E_CannotDeleteDir"));
			}
			return SuccessResult();
		}

		private string CopyDir(string path, string newPath)
		{
			var source = new DirectoryInfo(CheckPath(path));
			var destination = new DirectoryInfo(Path.Combine(CheckPath(newPath), source.Name));
			if (!source.Exists)
			{
				throw new InvalidOperationException(Text("E_CopyDirInvalidPath"));
			}
			if (destination.Exists)
			{
				throw new InvalidOperationException(Text("E_DirAlreadyExists"));
			}
			CopyDirectory(source.FullName, destination.FullName);
			return SuccessResult();
		}

		private static void CopyDirectory(string source, string destination)
		{
			Directory.CreateDirectory(destination);
			foreach (string file in Directory.GetFiles(source))
			{
				string target = Path.Combine(destination, Path.GetFileName(file));
				if (!System.IO.File.Exists(target))
				{
					System.IO.File.Copy(file, target);
				}
			}
			foreach (string directory in Directory.GetDirectories(source))
			{
				CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
			}
		}

		private string MoveDir(string path, string newPath)
		{
			var source = new DirectoryInfo(CheckPath(path));
			var destination = new DirectoryInfo(Path.Combine(CheckPath(newPath), source.Name));
			if (destination.FullName.StartsWith(source.FullName, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(Text("E_CannotMoveDirToChild"));
			}
			if (!source.Exists)
			{
				throw new InvalidOperationException(Text("E_MoveDirInvalisPath"));
			}
			if (destination.Exists)
			{
				throw new InvalidOperationException(Text("E_DirAlreadyExists"));
			}
			try
			{
				source.MoveTo(destination.FullName);
			}
			catch (Exception)
			{
				throw new InvalidOperationException(Text("E_MoveDir") + " \"" + path + "\"");
			}
			return SuccessResult();
		}

		private string RenameDir(string path, string name)
		{
			var source = new DirectoryInfo(CheckPath(path));
			CheckName(name);
			if (source.FullName.Equals(FilesRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(Text("E_CannotRenameRoot"));
			}
			if (!source.Exists)
			{
				throw new InvalidOperationException(Text("E_RenameDirInvalidPath"));
			}
			var destination = new DirectoryInfo(Path.Combine(source.Parent!.FullName, name));
			if (destination.Exists)
			{
				throw new InvalidOperationException(Text("E_DirAlreadyExists"));
			}
			try
			{
				source.MoveTo(destination.FullName);
			}
			catch (Exception)
			{
				throw new InvalidOperationException(Text("E_RenameDir") + " \"" + path + "\"");
			}
			return SuccessResult();
		}

		#endregion

		#region Files

		private static List<string> GetFiles(string directory, string type)
		{
			if (type == "#")
			{
				type = String.Empty;
			}
			return Directory.GetFiles(directory)
				.Where(file => String.IsNullOrEmpty(type) || FileType(Path.GetExtension(file)) == type)
				.ToList();
		}

		private string ListFiles(string path, string type)
		{
			string directory = CheckPath(path);
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0).ToLocalTime();
			// Without thumbnails, the width and height of images aren't read (0).
			return "[" + String.Join(",", GetFiles(directory, type).Select(file =>
			{
				var info = new FileInfo(file);
				double timestamp = Math.Ceiling((info.LastWriteTime.ToLocalTime() - epoch).TotalSeconds);
				return "{\"p\":\"" + path + "/" + info.Name + "\",\"t\":\""
					+ timestamp.ToString(CultureInfo.InvariantCulture) + "\",\"s\":\""
					+ info.Length.ToString(CultureInfo.InvariantCulture) + "\",\"w\":\"0\",\"h\":\"0\"}";
			})) + "]";
		}

		private static string MakeUniqueFileName(string directory, string fileName)
		{
			string result = fileName;
			for (int i = 1; System.IO.File.Exists(Path.Combine(directory, result)); i++)
			{
				result = Path.GetFileNameWithoutExtension(fileName) + " - Copy " + i.ToString(CultureInfo.InvariantCulture)
					+ Path.GetExtension(fileName);
			}
			return result;
		}

		private string CopyFile(string path, string newPath)
		{
			var file = new FileInfo(CheckPath(path));
			string destination = CheckPath(newPath);
			if (!file.Exists)
			{
				throw new InvalidOperationException(Text("E_CopyFileInvalisPath"));
			}
			try
			{
				System.IO.File.Copy(file.FullName, Path.Combine(destination, MakeUniqueFileName(destination, file.Name)));
			}
			catch (Exception)
			{
				throw new InvalidOperationException(Text("E_CopyFile"));
			}
			return SuccessResult();
		}

		private string DeleteFile(string path)
		{
			string file = CheckPath(path);
			if (!System.IO.File.Exists(file))
			{
				throw new InvalidOperationException(Text("E_DeleteFileInvalidPath"));
			}
			try
			{
				System.IO.File.Delete(file);
			}
			catch (Exception)
			{
				throw new InvalidOperationException(Text("E_DeletеFile"));
			}
			return SuccessResult();
		}

		private IActionResult Download(string path)
		{
			var file = new FileInfo(CheckPath(path));
			if (!file.Exists)
			{
				return NotFound();
			}
			return PhysicalFile(file.FullName, "application/force-download", file.Name);
		}

		private string MoveFile(string path, string newPath)
		{
			var source = new FileInfo(CheckPath(path));
			var destination = new FileInfo(CheckPath(newPath));
			if (!source.Exists)
			{
				throw new InvalidOperationException(Text("E_MoveFileInvalisPath"));
			}
			if (!CanHandleFile(destination.Name))
			{
				throw new InvalidOperationException(Text("E_FileExtensionForbidden"));
			}
			if (destination.Exists)
			{
				throw new InvalidOperationException(Text("E_MoveFileAlreadyExists"));
			}
			try
			{
				source.MoveTo(destination.FullName);
			}
			catch (Exception)
			{
				throw new InvalidOperationException(Text("E_MoveFile") + " \"" + path + "\"");
			}
			return SuccessResult();
		}

		private string RenameFile(string path, string name)
		{
			var source = new FileInfo(CheckPath(path));
			CheckName(name);
			if (!source.Exists)
			{
				throw new InvalidOperationException(Text("E_RenameFileInvalidPath"));
			}
			if (!CanHandleFile(name))
			{
				throw new InvalidOperationException(Text("E_FileExtensionForbidden"));
			}
			try
			{
				source.MoveTo(Path.Combine(source.DirectoryName!, name));
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException(ex.Message + "; " + Text("E_RenameFile") + " \"" + path + "\"");
			}
			return SuccessResult();
		}

		/// <summary>
		/// Saves the uploaded files (answered with a script for the iframe of the upload form).
		/// </summary>
		private string Upload(string path)
		{
			string directory = CheckPath(path);
			string result = SuccessResult();
			try
			{
				foreach (IFormFile upload in Request.Form.Files)
				{
					string fileName = Path.GetFileName(upload.FileName);
					if (fileName != String.Empty && CanHandleFile(fileName))
					{
						using FileStream stream = System.IO.File.Create(Path.Combine(directory, MakeUniqueFileName(directory, fileName)));
						upload.CopyTo(stream);
					}
					else
					{
						result = SuccessResult(Text("E_UploadNotAll"));
					}
				}
			}
			catch (Exception ex)
			{
				result = ErrorResult(ex.Message);
			}
			return "<script>parent.fileUploaded(" + result + ");</script>";
		}

		#endregion
	}
}
