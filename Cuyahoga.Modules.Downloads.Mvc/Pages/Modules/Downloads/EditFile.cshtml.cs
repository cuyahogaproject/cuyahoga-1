using System.Globalization;
using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NHibernate;
using File = Cuyahoga.Modules.Downloads.Domain.File;

namespace Cuyahoga.Modules.Downloads.Mvc.Pages.Modules.Downloads
{
	/// <summary>
	/// Upload a file and edit its properties and the roles that may download it (EditFile.aspx in the WebForms site).
	/// Like there, an upload saves the file right away; Cancel removes a new file again.
	/// </summary>
	public class EditFileModel : ModuleAdminPageModel<DownloadsModule>
	{
		private File _file;

		/// <summary>
		/// The new file that was uploaded but not saved yet, or null.
		/// </summary>
		[BindProperty]
		public int? TempFileId { get; set; }

		[BindProperty(Name = "txtTitle")]
		public string FileTitle { get; set; }

		[BindProperty(Name = "calDatePublished")]
		public string DatePublished { get; set; }

		/// <summary>
		/// The ids of the checked roles.
		/// </summary>
		[BindProperty(Name = "Roles")]
		public List<int> AllowedRoleIds { get; set; } = new List<int>();

		public string FilePath => this._file.FilePath;

		public bool IsExistingFile { get; private set; }

		public bool FileMissing { get; private set; }

		public bool DateMissing { get; private set; }

		public IReadOnlyList<RoleOption> Roles { get; private set; }

		public record RoleOption(int Id, string Name, bool Allowed);

		public IActionResult OnGet()
		{
			if (!LoadFile())
			{
				return NotFound();
			}
			if (IsExistingFile)
			{
				FileTitle = this._file.Title;
				DatePublished = JsCalendar.Format(this._file.DatePublished, displayTime: true);
			}
			else
			{
				DatePublished = JsCalendar.Format(DateTime.Now, displayTime: true);
			}
			BindRoles(role => this._file.IsDownloadAllowed(role));
			return Page();
		}

		public IActionResult OnPost(IFormFile filUpload)
		{
			if (!LoadFile())
			{
				return NotFound();
			}
			if (Request.Form.ContainsKey("btnUpload"))
			{
				Upload(filUpload);
			}
			else if (Request.Form.ContainsKey("btnDelete") && IsExistingFile)
			{
				try
				{
					Module.DeleteFile(this._file);
					return BackToList();
				}
				catch (Exception ex)
				{
					ShowError("Error deleting file: " + ex.Message);
				}
			}
			else if (Request.Form.ContainsKey("btnCancel"))
			{
				// A new file that was uploaded is removed again.
				if (!IsExistingFile && TempFileId != null)
				{
					Module.DeleteFile(this._file);
				}
				return BackToList();
			}
			else if (Save())
			{
				return BackToList();
			}
			BindRoles(role => AllowedRoleIds.Contains(role.Id));
			return Page();
		}

		/// <summary>
		/// The file of the url (FileId), the new file that was uploaded, or a new file that the roles of the section may
		/// download. False when the file doesn't exist in this section.
		/// </summary>
		private bool LoadFile()
		{
			if (!Int32.TryParse(Request.Query["FileId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fileId))
			{
				return false;
			}
			IsExistingFile = fileId > 0;
			int? id = IsExistingFile ? fileId : TempFileId;
			if (id != null)
			{
				this._file = Module.GetFileById(id.Value);
				try
				{
					// GetFileById returns a proxy that is loaded on first use.
					NHibernateUtil.Initialize(this._file);
				}
				catch (ObjectNotFoundException)
				{
					return false;
				}
				// Only the files of this section (the WebForms page took any file).
				return this._file.Section?.Id == Section.Id;
			}
			this._file = new File();
			foreach (Permission permission in Section.SectionPermissions)
			{
				this._file.AllowedRoles.Add(permission.Role);
			}
			return true;
		}

		private void Upload(IFormFile upload)
		{
			if (upload == null || upload.Length == 0)
			{
				return;
			}
			this._file.Title = FileTitle;
			this._file.DatePublished = JsCalendar.Parse(DatePublished) ?? DateTime.MinValue;
			this._file.FilePath = Path.GetFileName(upload.FileName);
			this._file.ContentType = upload.ContentType;
			this._file.Size = (int)upload.Length;
			this._file.Publisher = CurrentUser;
			this._file.Section = Section;
			try
			{
				using (Stream contents = upload.OpenReadStream())
				{
					Module.SaveFile(this._file, contents);
				}
				if (!IsExistingFile && this._file.Id > 0)
				{
					TempFileId = this._file.Id;
				}
				FileTitle = this._file.Title;
				DatePublished = JsCalendar.Format(this._file.DatePublished, displayTime: true);
			}
			catch (Exception ex)
			{
				ShowError("Error saving the file: " + Path.Combine(Module.FileDir, this._file.FilePath) + " " + ex.Message);
			}
		}

		private bool Save()
		{
			FileMissing = String.IsNullOrEmpty(this._file.FilePath);
			DateMissing = String.IsNullOrWhiteSpace(DatePublished);
			if (FileMissing || DateMissing)
			{
				return false;
			}
			this._file.Publisher = CurrentUser;
			this._file.Title = FileTitle;
			this._file.DatePublished = JsCalendar.Parse(DatePublished) ?? DateTime.MinValue;
			this._file.AllowedRoles.Clear();
			foreach (Permission permission in Section.SectionPermissions)
			{
				if (AllowedRoleIds.Contains(permission.Role.Id))
				{
					this._file.AllowedRoles.Add(permission.Role);
				}
			}
			try
			{
				Module.SaveFileInfo(this._file);
				return true;
			}
			catch (Exception ex)
			{
				ShowError("Error saving file: " + ex.Message);
				return false;
			}
		}

		private void BindRoles(Func<Role, bool> isAllowed)
		{
			Roles = Section.SectionPermissions
				.Select(permission => new RoleOption(permission.Role.Id, permission.Role.Name, isAllowed(permission.Role)))
				.ToList();
		}

		private IActionResult BackToList()
		{
			return Redirect("EditDownloads.aspx" + BaseQueryString);
		}
	}
}
