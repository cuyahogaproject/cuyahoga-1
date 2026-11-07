<%@ Control Language="c#" AutoEventWireup="True" Codebehind="RemoteContent.ascx.cs" Inherits="Cuyahoga.Modules.RemoteContent.Web.RemoteContent" TargetSchema="http://schemas.microsoft.com/intellisense/ie5"%>
<asp:repeater id="rptFeedItems" runat="server" enableviewstate="False">
	<itemtemplate>
		<div class="genericitem">
			<h4><asp:hyperlink id="hplLink" runat="server" navigateurl='<%# FormatUrl(DataBinder.Eval(Container.DataItem, "Url")) %>'><%# FormatText(DataBinder.Eval(Container.DataItem, "Title")) %></asp:hyperlink></h4>
			<asp:panel id="pnlContents" runat="server" visible="False">
				<%# FormatContent(DataBinder.Eval(Container.DataItem, "Content")) %>
			</asp:panel>
			<div class="genericdetails">
				<asp:label id="lblPubdate" runat="server" visible="False"></asp:label>
				<asp:label id="lblAuthor" runat="server" visible="False"><%# FormatText(DataBinder.Eval(Container.DataItem, "Author")) %></asp:label>
				<asp:label id="lblSource" runat="server" visible="False"><%# FormatText(DataBinder.Eval(Container.DataItem, "Feed.Title")) %></asp:label>
			</div>
		</div>
	</itemtemplate>
</asp:repeater>