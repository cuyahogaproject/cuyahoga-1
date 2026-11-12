using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using Cuyahoga.Core.Domain;
using Xunit;

namespace Cuyahoga.Core.Tests.Domain
{
	public class NodeTests
	{
		private static string[] Titles(IEnumerable<Node> nodes) => nodes.Select(n => n.Title).ToArray();

		[Fact]
		public void Level_trail_and_path_come_from_the_parents()
		{
			var home = Build.Node(1, "Home");
			var about = Build.Node(2, "About", home);
			var team = Build.Node(3, "Team", about);

			Assert.Equal(0, home.Level);
			Assert.Equal(2, team.Level);
			Assert.Equal(new[] { 1, 2, 3 }, team.Trail);
			Assert.Equal(new[] { home, about, team }, team.NodePath);
		}

		[Fact]
		public void CalculateNewPosition_puts_a_node_at_the_end()
		{
			var home = Build.Node(1, "Home");
			Build.Node(2, "A", home);
			Build.Node(3, "B", home);
			var child = new Node { ParentNode = home };
			var root = new Node();

			child.CalculateNewPosition(null);
			root.CalculateNewPosition(new List<Node> { home });

			Assert.Equal(2, child.Position);
			Assert.Equal(1, root.Position);
		}

		[Fact]
		public void ReOrderNodePositions_closes_a_gap()
		{
			var nodes = new List<Node> { new Node { Position = 0 }, new Node { Position = 2 }, new Node { Position = 3 } };

			new Node().ReOrderNodePositions(nodes, 1);

			Assert.Equal(new[] { 0, 1, 2 }, nodes.Select(n => n.Position));
		}

		[Fact]
		public void Move_up_and_down_swap_with_the_sibling()
		{
			var home = Build.Node(1, "Home");
			var a = Build.Node(2, "A", home);
			var b = Build.Node(3, "B", home);
			var c = Build.Node(4, "C", home);

			b.Move(null, NodePositionMovement.Up);
			Assert.Equal(new[] { "B", "A", "C" }, Titles(home.ChildNodes));
			Assert.Equal(new[] { 0, 1, 2 }, new[] { b.Position, a.Position, c.Position });

			b.Move(null, NodePositionMovement.Down);
			b.Move(null, NodePositionMovement.Down);
			Assert.Equal(new[] { "A", "C", "B" }, Titles(home.ChildNodes));
			Assert.Equal(new[] { 0, 1, 2 }, new[] { a.Position, c.Position, b.Position });
		}

		[Fact]
		public void Move_right_makes_the_node_a_child_of_the_previous_sibling()
		{
			var home = Build.Node(1, "Home");
			var a = Build.Node(2, "A", home);
			var b = Build.Node(3, "B", home);
			var c = Build.Node(4, "C", home);

			b.Move(null, NodePositionMovement.Right);

			Assert.Same(a, b.ParentNode);
			Assert.Equal(new[] { "B" }, Titles(a.ChildNodes));
			Assert.Equal(0, b.Position);
			Assert.Equal(new[] { "A", "C" }, Titles(home.ChildNodes));
			Assert.Equal(1, c.Position);
		}

		[Fact]
		public void Move_left_puts_the_node_after_its_parent()
		{
			var site = Build.Node(1, "Root");
			var home = Build.Node(2, "Home", site);
			var a = Build.Node(3, "A", home);
			var other = Build.Node(4, "Other", site);

			a.Move(null, NodePositionMovement.Left);

			Assert.Same(site, a.ParentNode);
			Assert.Equal(home.Position + 1, a.Position);
			Assert.Equal(2, other.Position);
			Assert.Empty(home.ChildNodes);
		}

		[Fact(Skip = "Known bug: MoveDown reads ParentNode.ChildNodes before checking for a parent, so moving a root node down throws a NullReferenceException.")]
		public void Move_down_works_for_root_nodes()
		{
			var first = Build.Node(1, "English");
			var second = Build.Node(2, "Nederlands");
			second.Position = 1;
			var rootNodes = new List<Node> { first, second };
			first.Position = 0;

			first.Move(rootNodes, NodePositionMovement.Down);

			Assert.Equal(1, first.Position);
			Assert.Equal(0, second.Position);
		}

		[Theory]
		[InlineData("About Us", "home/about-us")]
		[InlineData("  Q&A: news! ", "home/qa-news")]
		[InlineData("Version 2.0", "home/version-2.0")]
		public void CreateShortDescription_makes_a_url_friendly_path(string title, string expected)
		{
			var home = new Node { ShortDescription = "home" };
			var node = new Node { Title = title, ParentNode = home };

			node.CreateShortDescription();

			Assert.Equal(expected, node.ShortDescription);
		}

		[Fact]
		public void Root_node_short_description_has_no_prefix()
		{
			var node = new Node { Title = "Home" };

			node.CreateShortDescription();

			Assert.Equal("home", node.ShortDescription);
		}

		[Fact]
		public void View_permissions()
		{
			var anonymous = Build.Anonymous();
			var editor = Build.Editor();
			var publicNode = Build.Node(1, "Public").Allow(anonymous).Allow(editor, edit: true);
			var editorsOnly = Build.Node(2, "Editors").Allow(editor, edit: true);
			IIdentity visitor = new GenericIdentity("visitor");

			Assert.True(publicNode.ViewAllowed((IIdentity)null));
			Assert.True(publicNode.ViewAllowed(visitor));
			Assert.False(editorsOnly.ViewAllowed((IIdentity)null));
			Assert.False(editorsOnly.ViewAllowed(visitor));
			Assert.True(editorsOnly.ViewAllowed(Build.User(editor)));
			Assert.True(editorsOnly.ViewAllowed(editor));
			Assert.True(editorsOnly.EditAllowed(editor));
			Assert.False(editorsOnly.EditAllowed(anonymous));
		}

		[Fact(Skip = "Known bug: Node.AnonymousViewAllowed ignores ViewAllowed (Section checks it), so a node where anonymous users may only edit is visible to everyone.")]
		public void Anonymous_role_without_view_permission_cant_view()
		{
			var node = Build.Node(1, "Page").Allow(Build.Anonymous(), view: false, edit: true);

			Assert.False(node.ViewAllowed((IIdentity)null));
		}

		[Fact]
		public void CopyRolesFromParent_copies_the_permissions()
		{
			var editor = Build.Editor();
			var home = Build.Node(1, "Home").Allow(editor, view: true, edit: true);
			var child = Build.Node(2, "Child", home);

			child.CopyRolesFromParent();

			var permission = Assert.Single(child.NodePermissions);
			Assert.Same(child, permission.Node);
			Assert.Same(editor, permission.Role);
			Assert.True(permission.ViewAllowed && permission.EditAllowed);
		}

		[Fact]
		public void Validate_rejects_a_second_root_node_with_the_same_culture()
		{
			var site = new Site { RootNodes = new List<Node>() };
			var english = new Node { Id = 1, Culture = "en-US", Site = site };
			site.RootNodes.Add(english);

			var anotherEnglish = new Node { Id = 2, Culture = "en-US", Site = site };
			var dutch = new Node { Id = 3, Culture = "nl-NL", Site = site };

			Assert.Throws<Exception>(() => anotherEnglish.Validate());
			dutch.Validate();
			english.Validate();
		}
	}
}
