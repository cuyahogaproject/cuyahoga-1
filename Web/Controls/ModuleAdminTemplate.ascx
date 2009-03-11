<%@ Control Language="c#" AutoEventWireup="false" Codebehind="ModuleAdminTemplate.ascx.cs" Inherits="Cuyahoga.Web.Controls.ModuleAdminTemplate"%>
<%@ Register TagPrefix="uc1" TagName="header" Src="../Controls/ModuleAdminHeader.ascx" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
	<head>
		<title><asp:literal id="PageTitle" runat="server"></asp:literal></title>
		<link id="CssStyleSheet" rel="stylesheet" type="text/css" runat="server" />
		<asp:PlaceHolder ID="AddedCssPlaceHolder" runat="server"></asp:PlaceHolder>
        <asp:PlaceHolder ID="AddedJavaScriptPlaceHolder" runat="server"></asp:PlaceHolder>
	</head>
	<body>
		<form id="Frm" method="post" enctype="multipart/form-data" runat="server">
			<uc1:header id="header" runat="server"></uc1:header>
			<div id="MessageBox" class="messagebox" runat="server" visible="false" enableviewstate="false"></div>
			<asp:placeholder id="PageContent" runat="server"></asp:placeholder>
		</form>
	</body>
</html>