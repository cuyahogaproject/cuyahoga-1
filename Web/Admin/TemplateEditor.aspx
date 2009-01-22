<%@ Page language="c#" Codebehind="TemplateEditor.aspx.cs" AutoEventWireup="false" Inherits="Cuyahoga.Web.Admin.TemplateEditor" %>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN" >
<html>
	<head>
		<title>Template/CSS Editor</title>
		<meta name="GENERATOR" content="Microsoft Visual Studio .NET 7.1">
		<meta name="CODE_LANGUAGE" content="C#">
		<meta name="vs_defaultClientScript" content="JavaScript">
		<meta name="vs_targetSchema" content="http://schemas.microsoft.com/intellisense/ie5">
	</head>
	<body ms_positioning="FlowLayout">
		<form id="Form1" method="post" runat="server">
			<p>
				<em>Modifying this file could broke design or functionalities of the site.</em>
			</p>
			<div class="group">
				<h4>Edit file</h4>
				<table>
					<tr>
						<td><asp:textbox id="txtName" runat="server" width="600px" Height="500px" TextMode="MultiLine"></asp:textbox></td>
					</tr>
				</table>
			</div>
			<br/>
			<asp:button id="btnBack" runat="server" text="Back" causesvalidation="false"></asp:button>
			&nbsp;<asp:button id="btnSave" runat="server" text="Save"></asp:button>            
		</form>
	</body>
</html>
