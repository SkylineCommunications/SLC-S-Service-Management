namespace SLC_SM_IAS_ManageRelationships.Controller
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_ManageRelationships.Model;
	using SLC_SM_IAS_ManageRelationships.View;
	using SLCSMIASManageRelationships;

	public class ManageConnectionsController
	{
		private readonly IEngine _engine;
		private readonly InteractiveController _controller;
		private readonly ManageConnectionsModel _model;
		private readonly ScriptData _data;
		private readonly List<ServiceItemLinkMap> _linkMap;
		private readonly IssueCollector _issueCollector;

		private ManageConnectionDialog _dialog;
		private IServiceItem _serviceInstance;

		private int _nextPairIndex = 0;

		public ManageConnectionsController(IEngine engine, ScriptData data)
		{
			_engine = engine;
			_data = data;
			_model = new ManageConnectionsModel(engine);
			_controller = new InteractiveController(engine) { ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways };
			_linkMap = new List<ServiceItemLinkMap>();
			_issueCollector = new IssueCollector();
		}

		public EventHandler<EventArgs> OnCancel => (sender, args) => _engine.ExitSuccess(String.Empty);

		public EventHandler<EventArgs> OnValidate => (sender, args) => Validate_Pressed();

		public EventHandler<EventArgs> OnPrevious => (sender, args) => Previous_Pressed();

		public void HandleNext()
		{
			while (HasMorePairs())
			{
				var currentPair = GetNextValidPair();
				if (currentPair == null)
					break;

				if (currentPair.IsOneToOne)
				{
					HandleOneToOnePair(currentPair);
					continue;
				}

				ShowDialogForPair(currentPair);
				return;
			}

			Save();
		}

		public void BuildLinkMap()
		{
			_serviceInstance = _model.GetInstance(_data.DomId);

			var serviceItemNodes = _model.GetServiceItems(_serviceInstance, _data.ServiceIds);
			var sequentialPairs = _model.ToSequentialPairs(serviceItemNodes);

			var connections = sequentialPairs.Select(pair => CreateLinkMapFromPair(pair));

			foreach (var connection in connections)
				_linkMap.Add(connection);
		}

		public void CreateServiceItem()
		{
			_data.ServiceIds.Add(_model.CreateServiceItem(_data.DomId, _data.DefinitionReference, _data.Type));
		}

		private bool HasMorePairs()
		{
			return _nextPairIndex < _linkMap.Count;
		}

		private ServiceItemLinkMap GetNextValidPair()
		{
			while (_nextPairIndex < _linkMap.Count)
			{
				var pair = _linkMap[_nextPairIndex++];

				if (!pair.HasSources)
				{
					_issueCollector.Add($"{pair.SourceNode.Label} has no outputs available.");
					continue;
				}

				if (!pair.HasDestinations)
				{
					_issueCollector.Add($"{pair.DestinationNode.Label} has no inputs left.");
					continue;
				}

				return pair;
			}

			return null;
		}

		private void HandleOneToOnePair(ServiceItemLinkMap pair)
		{
			CreateOneToOneLink(pair);

			if (IsLast())
				Save();
		}

		private void ShowDialogForPair(ServiceItemLinkMap pair)
		{
			ConfigureDialog(pair);
			_controller.ShowDialog(_dialog);
		}

		private void CreateOneToOneLink(ServiceItemLinkMap currentPair)
		{
			var sourceInterface = currentPair.AvailableSources.Single();
			var destinationInterface = currentPair.AvailableDestinations.Single();

			if (!currentPair.HasLink(sourceInterface, destinationInterface))
				currentPair.AddLink(sourceInterface.NodeID, destinationInterface.NodeID);
		}

		private void ConfigureDialog(ServiceItemLinkMap currentPair)
		{
			var canShowNext = CanShowNext();
			var canShowPrevious = CanShowPrevious();

			_dialog = new ManageConnectionDialog(_engine, new ServiceItemLinkMapContext()
			{
				Pair = currentPair,
				ShowNext = canShowNext,
				ShowPrevious = canShowPrevious,
			});
			_dialog.ButtonValidate.Pressed += OnValidate;
			_dialog.ButtonCancel.Pressed += OnCancel;
			_dialog.ButtonPrevious.Pressed += OnPrevious;
		}

		private bool CanShowNext() => HasNavigablePair(i => i < _linkMap.Count, i => i + 1, _nextPairIndex);

		private bool CanShowPrevious() => HasNavigablePair(i => i >= 0, i => i - 1, _nextPairIndex - 2);

		private bool IsLast() => _nextPairIndex == _linkMap.Count;

		private bool HasNavigablePair(Func<int, bool> condition, Func<int, int> increment, int startIndex)
		{
			for (int i = startIndex; condition(i); i = increment(i))
			{
				var pair = _linkMap[i];
				if (pair.HasSources && !pair.IsOneToOne)
					return true;
			}

			return false;
		}

		private ServiceItemLinkMap CreateLinkMapFromPair((Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceItem, Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceItem) pair)
		{
			var links = _model.FindRelationshipsBetweenPair(_serviceInstance, pair);

			var source = _model.ResolveDefinitionReference(_serviceInstance, pair.Item1);
			var destination = _model.ResolveDefinitionReference(_serviceInstance, pair.Item2);

			return new ServiceItemLinkMap
			{
				SourceNode = pair.Item1,
				DestinationNode = pair.Item2,
				AvailableSources = source.GetAvailableOutputs(),
				AvailableDestinations = destination.GetAvailableInputs(),
				Links = links,
			};
		}

		private void Previous_Pressed()
		{
			_dialog.UpdatePairFromDialog();
			_nextPairIndex -= 2;
			HandleNext();
		}

		private void Validate_Pressed()
		{
			_dialog.UpdatePairFromDialog();
			HandleNext();
		}

		private void Save()
		{
			_model.Update(_linkMap, _serviceInstance);

			if (_issueCollector.HasIssues)
				ReportIssues();

			_engine.ExitSuccess(String.Empty);
		}

		private void ReportIssues()
		{
			_engine.ShowPopupDialog(_controller, "Attention!", _issueCollector.PrintReport(), "OK");
		}
	}
}
