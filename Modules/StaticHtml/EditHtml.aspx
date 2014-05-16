<%@ Page language="c#" Codebehind="EditHtml.aspx.cs" AutoEventWireup="True" Inherits="Cuyahoga.Modules.StaticHtml.EditHtml" ValidateRequest="false" %>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN" >
<html>
	<head>
		<title>EditHtml</title>
		<meta content="Microsoft Visual Studio .NET 7.1" name="GENERATOR">
		<meta content="C#" name="CODE_LANGUAGE">
		<meta content="JavaScript" name="vs_defaultClientScript">
		<meta content="http://schemas.microsoft.com/intellisense/ie5" name="vs_targetSchema">
	</head>
	<body ms_positioning="FlowLayout">
		<form id="Form1" method="post" runat="server">
			<div id="moduleadminpane">
				<h1>Edit static content</h1>
				<asp:TextBox runat="server" id="txtEditor" TextMode="MultiLine"></asp:TextBox>
				<br/>
				<br/>
				<asp:button id="btnSave" runat="server" text="Save" onclick="btnSave_Click"></asp:button>
			</div>
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
		</form>
	</body>
</html>
