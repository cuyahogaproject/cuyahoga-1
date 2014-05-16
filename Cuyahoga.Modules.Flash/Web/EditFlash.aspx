<%@ Page language="c#" Codebehind="EditFlash.aspx.cs" AutoEventWireup="false" Inherits="Cuyahoga.Modules.Flash.Web.EditFlash" ValidateRequest="false" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
	<head>
		<title>Edit Alternate static content</title>
	</head>
	<body>
		<form id="Form1" method="post" runat="server">
			<div id="moduleadminpane">
				<h1>Edit&nbsp;alternate static&nbsp;content</h1>
				<asp:TextBox runat="server" id="txtEditor" TextMode="MultiLine"></asp:TextBox><br/>
				<br>
				<asp:button id="btnSave" runat="server" text="Save"></asp:button><asp:label id="lblMessage" runat="server" EnableViewState="False">Label</asp:label>
				<asp:PlaceHolder runat="server">
					<script type="text/javascript" src="<%= ResolveUrl("~/Support/ckeditor/ckeditor.js") %>"></script>
					<script type="text/javascript">
						var fileManUrl = '<%= ResolveUrl("~/Support/fileman/index.html") %>';
						CKEDITOR.replace('<%= this.txtEditor.ClientID %>', {
							uiColor: '#6699cc',
							height: 400,
							filebrowserBrowseUrl: fileManUrl,
							filebrowserUploadUrl: fileManUrl,
							filebrowserImageBrowseUrl: fileManUrl + '?type=image',
							filebrowserImageUploadUrl: fileManUrl + '?type=image'
						});
					</script>
				</asp:PlaceHolder>
			</div>
		</form>
	</body>
</html>
