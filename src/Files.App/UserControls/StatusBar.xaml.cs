// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.UserControls
{
	public sealed partial class StatusBar : UserControl
	{
		// Extra width a section must gain back before it expands again, so the bar
		// doesn't oscillate between states at the boundary
		private const double ExpandHysteresisWidth = 16d;

		// Gap kept between the info section and the actions section
		private const double SectionGapWidth = 8d;

		// State ranks: 0 = everything inline, 1 = actions collapsed, 2 = info and actions collapsed
		private int _currentStateRank;

		// Natural widths from the last measure pass each element was visible in; used to
		// decide when to expand while an element is collapsed and reports zero size.
		// The compact buttons start with estimates until they are shown once.
		private double _lastInfoNaturalWidth;
		private double _lastActionsNaturalWidth;
		private double _lastStatusButtonWidth = 90d;
		private double _lastGitButtonWidth = 80d;

		public ICommandManager Commands { get; } = Ioc.Default.GetRequiredService<ICommandManager>();

		public StatusBarViewModel? StatusBarViewModel
		{
			get => (StatusBarViewModel)GetValue(StatusBarViewModelProperty);
			set => SetValue(StatusBarViewModelProperty, value);
		}

		// Using a DependencyProperty as the backing store for StatusBarViewModel.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty StatusBarViewModelProperty =
			DependencyProperty.Register(nameof(StatusBarViewModel), typeof(StatusBarViewModel), typeof(StatusBar), new PropertyMetadata(null));

		public SelectedItemsPropertiesViewModel? SelectedItemsPropertiesViewModel
		{
			get => (SelectedItemsPropertiesViewModel)GetValue(SelectedItemsPropertiesViewModelProperty);
			set => SetValue(SelectedItemsPropertiesViewModelProperty, value);
		}

		public static readonly DependencyProperty SelectedItemsPropertiesViewModelProperty =
			DependencyProperty.Register(nameof(SelectedItemsPropertiesViewModel), typeof(SelectedItemsPropertiesViewModel), typeof(StatusBar), new PropertyMetadata(null));

		public bool ShowInfoText
		{
			get => (bool)GetValue(ShowInfoTextProperty);
			set => SetValue(ShowInfoTextProperty, value);
		}

		// Using a DependencyProperty as the backing store for HideInfoText.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty ShowInfoTextProperty =
			DependencyProperty.Register(nameof(ShowInfoText), typeof(bool), typeof(StatusBar), new PropertyMetadata(false));

		public StatusBar()
		{
			InitializeComponent();
		}

		public Visibility ToVisibility(bool value)
		{
			return value ? Visibility.Visible : Visibility.Collapsed;
		}

		public Visibility GetOpenRepoInIDEItemVisibility(bool showOpenInIDEButton, string? gitBranchDisplayName)
		{
			return showOpenInIDEButton && gitBranchDisplayName is not null ? Visibility.Visible : Visibility.Collapsed;
		}

		public Visibility GetActionsAreaVisibility(bool showOpenInIDEButton, string? gitBranchDisplayName)
		{
			return showOpenInIDEButton || gitBranchDisplayName is not null ? Visibility.Visible : Visibility.Collapsed;
		}

		private void StatusBar_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			UpdateResponsiveState();
		}

		private void Content_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			UpdateResponsiveState();
		}

		private void UpdateResponsiveState()
		{
			// InfoTextPanel sits in a star column so its own DesiredSize is clamped to the
			// column width; its children are measured without a horizontal constraint, so
			// summing them yields the natural width of the localized text
			if (InfoTextPanel.Visibility == Visibility.Visible)
			{
				var infoTextWidth = 0d;
				var visibleChildren = 0;
				foreach (var child in InfoTextPanel.Children)
				{
					if (child.Visibility == Visibility.Visible)
					{
						infoTextWidth += child.DesiredSize.Width;
						visibleChildren++;
					}
				}
				_lastInfoNaturalWidth = infoTextWidth + InfoTextPanel.Spacing * Math.Max(0, visibleChildren - 1);
			}

			if (StandardActions.Visibility == Visibility.Visible)
				_lastActionsNaturalWidth = StandardActions.DesiredSize.Width;
			if (StatusButton.Visibility == Visibility.Visible && StatusButton.DesiredSize.Width > 0)
				_lastStatusButtonWidth = StatusButton.DesiredSize.Width;
			if (GitOverflowButton.Visibility == Visibility.Visible && GitOverflowButton.DesiredSize.Width > 0)
				_lastGitButtonWidth = GitOverflowButton.DesiredSize.Width;

			var showsInfo = ShowInfoText;
			var hasActions =
				StatusBarViewModel is not null &&
				(StatusBarViewModel.ShowOpenInIDEButton || StatusBarViewModel.GitBranchDisplayName is not null);

			var expandedInfoWidth = showsInfo ? _lastInfoNaturalWidth : 0d;
			var expandedActionsWidth = hasActions ? _lastActionsNaturalWidth : 0d;
			var collapsedActionsWidth = hasActions ? _lastGitButtonWidth : 0d;

			var availableWidth = ActualWidth - RootGrid.Padding.Left - RootGrid.Padding.Right;

			// Collapse one section at a time, actions first; expanding requires the extra
			// hysteresis width so the bar doesn't oscillate at the boundary
			var targetRank =
				expandedInfoWidth + SectionGapWidth + expandedActionsWidth + GetExpandMargin(0) <= availableWidth ? 0 :
				expandedInfoWidth + SectionGapWidth + collapsedActionsWidth + GetExpandMargin(1) <= availableWidth ? 1 : 2;

			_currentStateRank = targetRank;

			VisualStateManager.GoToState(
				this,
				targetRank switch
				{
					0 => "NormalState",
					1 => "CompactActionsState",
					_ => "CompactState",
				},
				false);

			double GetExpandMargin(int rank)
				=> rank < _currentStateRank ? ExpandHysteresisWidth : 0d;
		}

		private void GitBranchCompact_Click(object sender, RoutedEventArgs e)
		{
			ActionsOverflowFlyout.Hide();
			BranchesFlyout.ShowAt(GitOverflowButton);
		}

		private async void BranchesFlyout_Opening(object _, object e)
		{
			if (StatusBarViewModel is null)
				return;

			StatusBarViewModel.IsBranchesFlyoutExpanded = true;
			StatusBarViewModel.ShowLocals = true;
			await StatusBarViewModel.LoadBranches();
			StatusBarViewModel.SelectedBranchIndex = StatusBarViewModel.ACTIVE_BRANCH_INDEX;
		}

		private void BranchesList_ItemClick(object sender, ItemClickEventArgs e)
		{
			BranchesFlyout.Hide();
		}

		private void BranchesFlyout_Closing(object _, object e)
		{
			if (StatusBarViewModel is null)
				return;

			StatusBarViewModel.IsBranchesFlyoutExpanded = false;
		}

		private async void DeleteBranch_Click(object sender, RoutedEventArgs e)
		{
			if (StatusBarViewModel is null)
				return;

			BranchesFlyout.Hide();
			await StatusBarViewModel.ExecuteDeleteBranch(((BranchItem)((Button)sender).DataContext).Name);
		}
	}
}
