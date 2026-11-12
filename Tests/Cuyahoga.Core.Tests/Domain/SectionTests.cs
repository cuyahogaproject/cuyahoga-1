using System.Security.Principal;
using Cuyahoga.Core.Domain;
using Xunit;

namespace Cuyahoga.Core.Tests.Domain
{
	public class SectionTests
	{
		[Fact]
		public void Positions_are_per_placeholder()
		{
			var node = Build.Node(1, "Home");
			var main1 = Build.Section(1, node, "main");
			var side1 = Build.Section(2, node, "side");
			var main2 = Build.Section(3, node, "main");

			Assert.Equal(0, main1.Position);
			Assert.Equal(0, side1.Position);
			Assert.Equal(1, main2.Position);
		}

		[Fact]
		public void Section_without_node_gets_position_0()
		{
			var section = new Section();

			section.CalculateNewPosition();

			Assert.Equal(0, section.Position);
		}

		[Fact]
		public void CanMoveUp_and_CanMoveDown_look_within_the_placeholder()
		{
			var node = Build.Node(1, "Home");
			var first = Build.Section(1, node, "main");
			var second = Build.Section(2, node, "main");
			var alone = Build.Section(3, node, "side");

			Assert.False(first.CanMoveUp());
			Assert.True(first.CanMoveDown());
			Assert.True(second.CanMoveUp());
			Assert.False(second.CanMoveDown());
			Assert.False(alone.CanMoveUp());
			Assert.False(alone.CanMoveDown());
		}

		[Fact]
		public void MoveUp_and_MoveDown_swap_positions()
		{
			var node = Build.Node(1, "Home");
			var first = Build.Section(1, node, "main");
			var second = Build.Section(2, node, "main");

			second.MoveUp();
			Assert.Equal((0, 1), (second.Position, first.Position));

			second.MoveDown();
			Assert.Equal((1, 0), (second.Position, first.Position));
		}

		[Fact]
		public void Changing_the_placeholder_closes_the_gap_in_the_old_placeholder()
		{
			var node = Build.Node(1, "Home");
			var first = Build.Section(1, node, "main");
			var moved = Build.Section(2, node, "main");
			var third = Build.Section(3, node, "main");

			moved.PlaceholderId = "side";
			moved.ChangeAndUpdatePositionsAfterPlaceholderChange("main", 1, false);

			Assert.Equal(0, first.Position);
			Assert.Equal(1, third.Position);
		}

		[Fact]
		public void View_permissions()
		{
			var anonymous = Build.Anonymous();
			var editor = Build.Editor();
			var node = Build.Node(1, "Home");
			var publicSection = Build.Section(1, node, "main").Allow(anonymous);
			var editOnlyForAnonymous = Build.Section(2, node, "main").Allow(anonymous, view: false, edit: true);
			var editorsOnly = Build.Section(3, node, "main").Allow(editor, view: true, edit: true);

			Assert.True(publicSection.ViewAllowed((IIdentity)null));
			Assert.False(editOnlyForAnonymous.ViewAllowed((IIdentity)null));
			Assert.False(editorsOnly.ViewAllowed(new GenericIdentity("visitor")));
			Assert.True(editorsOnly.ViewAllowed(Build.User(editor)));
			Assert.True(editorsOnly.EditAllowed(editor));
			Assert.False(publicSection.EditAllowed(anonymous));
		}
	}
}
