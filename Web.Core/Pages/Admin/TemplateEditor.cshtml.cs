using Cuyahoga.Core.Util;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Edit a template stylesheet (.css) in the template folder (wwwroot/Templates)
	/// (TemplateEditor.aspx?file=Templates/AnotherRed/css/red.css). The templates themselves are Razor layouts of the
	/// ASP.NET Core site (Templates/*.cshtml), part of the code of the site, so they can't be edited here.
	/// </summary>
	public class TemplateEditorModel : AdminPageModel
	{
		private readonly CuyahogaAppSettings _settings;

		public TemplateEditorModel(CuyahogaAppSettings settings)
		{
			this._settings = settings;
		}

		[BindProperty] public string Contents { get; set; }

		/// <summary>
		/// False when the file is not a stylesheet in the template folder.
		/// </summary>
		public bool CanEdit { get; private set; }

		public IActionResult OnGet()
		{
			string fileName = Load();
			if (fileName != null)
			{
				try
				{
					Contents = System.IO.File.ReadAllText(fileName);
				}
				catch (Exception)
				{
					ShowError("Error reading file: '" + fileName + "'");
				}
			}
			return Page();
		}

		public IActionResult OnPost()
		{
			if (Request.Form.ContainsKey("btnBack"))
			{
				return Redirect("Templates.aspx");
			}
			string fileName = Load();
			if (fileName != null)
			{
				try
				{
					using (var writer = new StreamWriter(fileName, false))
					{
						writer.Write(Contents);
					}
					ShowMessage("File saved.");
				}
				catch (Exception ex)
				{
					ShowError("Error occured: " + ex.Message);
				}
			}
			return Page();
		}

		/// <summary>
		/// The physical path of the file in the url, or null (with an error) when it may not be edited.
		/// </summary>
		private string Load()
		{
			Title = "Edit file";
			string fileName = GetFileName();
			CanEdit = fileName != null;
			if (!CanEdit)
			{
				ShowError("Only stylesheets (.css) in the template directory can be edited.");
			}
			return fileName;
		}

		private string GetFileName()
		{
			string file = Request.Query["file"];
			if (String.IsNullOrEmpty(file))
			{
				return null;
			}
			try
			{
				string physicalPath = Path.GetFullPath(ApplicationPaths.MapPath("~/" + file.TrimStart('/')));
				// Only stylesheets inside the template folder.
				string templateDir = Path.GetFullPath(ApplicationPaths.MapPath(this._settings.TemplateDir))
					.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
				if (physicalPath.StartsWith(templateDir, StringComparison.OrdinalIgnoreCase)
					&& String.Equals(Path.GetExtension(physicalPath), ".css", StringComparison.OrdinalIgnoreCase))
				{
					return physicalPath;
				}
			}
			catch (Exception)
			{
				// An invalid path can't be edited.
			}
			return null;
		}
	}
}
