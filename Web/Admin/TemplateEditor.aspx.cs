using System;
using System.IO;
using System.Web.UI.WebControls;
using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Admin.UI;

namespace Cuyahoga.Web.Admin
{
    /// <summary>
    /// Summary description for TemplateEditor.
    /// </summary>
    public class TemplateEditor : AdminBasePage
    {
        private Template _activeTemplate;

        protected Button btnBack;
        protected Button btnSave;
        protected TextBox txtName;

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        /// <value>The name of the file.</value>
        private string FileName
        {
            get
            {
                if (Context.Request.QueryString["file"] != null)
                {
                    try
                    {
                        string fileName = Context.Request.QueryString["file"].ToString();
                        if (!fileName.StartsWith("/")) fileName = "/" + fileName;
                        return Server.MapPath(fileName);
                    }
                    catch (Exception)
                    {
                        return string.Empty;
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Handles the Load event of the Page control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void Page_Load(object sender, EventArgs e)
        {
            Title = "Edit file";

            btnSave.Attributes.Add("onclick", "return confirm('Are you sure?')");

            if (!IsPostBack)
            {
                try
                {
                    StreamReader reader = new StreamReader(FileName);

                    txtName.Text = reader.ReadToEnd();
                    reader.Close();
                }
                catch (Exception ex)
                {
                    ShowError("Error reading file: '" + FileName + "'");
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            if (IsValid)
            {
                try
                {
                    StreamWriter writer = new StreamWriter(FileName, false);
                    writer.Write(txtName.Text);
                    writer.Close();

                    ShowMessage("File saved.");
                }
                catch(Exception ex)
                {
                    ShowError("Error occured: " + ex.Message);
                }
            }
        }


        /// <summary>
        /// Handles the Click event of the btnCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            Context.Response.Redirect("Templates.aspx");
        }

        #region Web Form Designer generated code

        protected override void OnInit(EventArgs e)
        {
            //
            // CODEGEN: This call is required by the ASP.NET Web Form Designer.
            //
            InitializeComponent();
            base.OnInit(e);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            this.btnBack.Click += new System.EventHandler(this.btnCancel_Click);
            this.Load += new System.EventHandler(this.Page_Load);
        }

        #endregion
    }
}