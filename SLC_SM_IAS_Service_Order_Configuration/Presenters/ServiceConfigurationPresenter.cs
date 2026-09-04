namespace SLC_SM_IAS_Service_Order_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using DomHelpers.SlcConfigurations;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Service_Order_Configuration.Views;

	public class ServiceConfigurationPresenter
	{
		private readonly List<DataRecord> configurations = new List<DataRecord>();
		private readonly IEngine engine;
		private readonly InteractiveController controller;
		private readonly ServiceOrderItem instance;
		private readonly ServiceConfigurationView view;
		private ServiceManagementApiHelper repoService;

		public ServiceConfigurationPresenter(IEngine engine, InteractiveController controller, ServiceConfigurationView view, ServiceOrderItem instance)
		{
			this.engine = engine;
			this.controller = controller;
			this.view = view;
			this.instance = instance;

			view.BtnCancel.Pressed += OnCancelButtonPressed;
			view.BtnUpdate.Pressed += OnUpdateButtonPressed;
			view.BtnAddParameter.Pressed += (sender, args) =>
			{
				if (view.AddParameter?.Selected == null)
				{
					return;
				}

				AddConfigModel(view.AddParameter.Selected);
				BuildUI(!view.BtnShowValueDetails.IsCollapsed, !view.BtnShowLifeCycleDetails.IsCollapsed);
				view.AddParameter.Selected = null;
			};
		}

		private enum State
		{
			Update,
			Delete,
		}

		public void LoadFromModel()
		{
			repoService = new ServiceManagementApiHelper(engine.GetUserConnection(), "SLC_SM_IAS_Service_Order_Configuration");
			var configParams = ReadAll(repoService.ServiceCatalog.ConfigurationParameters);
			var configParamValues = ReadAll(repoService.ServiceCatalog.ConfigurationParameterValues).ToDictionary(x => x.Identifier);
			var numberOptions = ReadAll(repoService.ServiceCatalog.NumberParameterOptions).ToDictionary(x => x.Identifier);
			var discreteOptions = ReadAll(repoService.ServiceCatalog.DiscreteParameterOptions).ToDictionary(x => x.Identifier);
			var textOptions = ReadAll(repoService.ServiceCatalog.TextParameterOptions).ToDictionary(x => x.Identifier);
			var units = ReadAll(repoService.ServiceCatalog.ConfigurationUnits).ToDictionary(x => x.Identifier);
			var discreteValues = ReadAll(repoService.ServiceCatalog.DiscreteValues).ToDictionary(x => x.Identifier);

			var configurationReferences = instance.ServiceInfo?.Configurations;

			if (configurationReferences != null)
			{
				foreach (var currentConfigReference in configurationReferences)
				{
					var currentConfig = repoService.ServiceOrder.ServiceOrderItemConfigurationValues
						.Read(ServiceOrderItemConfigurationValueExposers.Identifier.Equal(currentConfigReference.Identifier))
						.FirstOrDefault();
					if (currentConfig == null)
					{
						continue;
					}

					var configurationParameterValueId = currentConfig.ConfigurationParameterValueId == null
						? String.Empty
						: currentConfig.ConfigurationParameterValueId.Identifier;

					if (String.IsNullOrEmpty(configurationParameterValueId) ||
						!configParamValues.TryGetValue(configurationParameterValueId, out var configurationParameterValue))
					{
						continue;
					}

					ConfigurationParameter configParam = null;
					var configurationParameterId = configurationParameterValue.ConfigurationParameterId.Identifier;
					if (!String.IsNullOrEmpty(configurationParameterId))
					{
						configParam = configParams.Find(x => x.Identifier == configurationParameterId);
					}

					if (configParam == null)
					{
						continue;
					}

					DataRecord dataRecord = BuildDataRecord(
						currentConfig,
						configurationParameterValue,
						configParam,
						numberOptions,
						discreteOptions,
						textOptions,
						units,
						discreteValues);

					configurations.Add(dataRecord);
				}
			}

			var parameterOptions = configParams.Select(x => new Option<ConfigurationParameter>(x.Name, x)).OrderBy(x => x.DisplayValue).ToList();
			parameterOptions.Insert(0, new Option<ConfigurationParameter>("- Parameter -", null));
			view.AddParameter.SetOptions(parameterOptions);

			BuildUI(false, false);
		}

		public void StoreModels()
		{
			foreach (var configuration in configurations)
			{
				if (configuration.State == State.Delete)
				{
					DeleteConfiguration(configuration);
					continue;
				}

				configuration.ConfigurationParamValue.ConfigurationParameterId =
					new SdmObjectReference<ConfigurationParameter>(
				configuration.ConfigurationParam.Identifier);

				configuration.ServiceConfig.ConfigurationParameterValueId =
					new SdmObjectReference<ConfigurationParameterValue>(
						configuration.ConfigurationParamValue.Identifier);

				CreateOrUpdateOptions(configuration);
				repoService.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { configuration.ConfigurationParamValue });
				UpsertServiceOrderItemConfigurationValue(configuration.ServiceConfig);
			}

			var updatedReferences = configurations
				.Where(c => c.State != State.Delete && c.ServiceConfig != null && !String.IsNullOrWhiteSpace(c.ServiceConfig.Identifier))
				.Select(c => new SdmObjectReference<ServiceOrderItemConfigurationValue>(c.ServiceConfig.Identifier))
				.GroupBy(reference => reference.Identifier, StringComparer.OrdinalIgnoreCase)
				.Select(group => group.First())
				.ToList();

			if (instance.ServiceInfo == null)
			{
				instance.ServiceInfo = new ServiceOrderItemServiceInfo();
			}

			instance.ServiceInfo.Configurations = updatedReferences;
			repoService.ServiceOrder.ServiceOrderItems.Update(instance);
		}

		private static void OnCancelButtonPressed(object sender, EventArgs e)
		{
			throw new ScriptAbortException("OK");
		}

		private void OnUpdateButtonPressed(object sender, EventArgs e)
		{
			StoreModels();
			throw new ScriptAbortException("OK");
		}

		private void UpsertServiceOrderItemConfigurationValue(ServiceOrderItemConfigurationValue value)
		{
			var existing = repoService.ServiceOrder.ServiceOrderItemConfigurationValues
				.Read(ServiceOrderItemConfigurationValueExposers.Identifier.Equal(value.Identifier))
				.FirstOrDefault();

			if (existing == null)
			{
				repoService.ServiceOrder.ServiceOrderItemConfigurationValues.Create(value);
				return;
			}

			repoService.ServiceOrder.ServiceOrderItemConfigurationValues.Update(value);
		}

		private void AddConfigModel(ConfigurationParameter selectedParameter)
		{
			var configurationParameterValue = new ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = String.Empty,
				Type = selectedParameter.Type,
				ConfigurationParameterId =
					new SdmObjectReference<ConfigurationParameter>(selectedParameter.Identifier),
			};

			var config = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = true,
				ConfigurationParameterValueId =
					new SdmObjectReference<ConfigurationParameterValue>(
						configurationParameterValue.Identifier),
			};

			if (instance.ServiceInfo == null)
			{
				instance.ServiceInfo = new ServiceOrderItemServiceInfo();
			}

			if (instance.ServiceInfo.Configurations == null)
			{
				instance.ServiceInfo.Configurations =
					new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
			}

			instance.ServiceInfo.Configurations.Add(
				new SdmObjectReference<ServiceOrderItemConfigurationValue>(config.Identifier));

			configurations.Add(BuildDataRecord(
				config,
				configurationParameterValue,
				selectedParameter,
				ReadAll(repoService.ServiceCatalog.NumberParameterOptions).ToDictionary(x => x.Identifier),
				ReadAll(repoService.ServiceCatalog.DiscreteParameterOptions).ToDictionary(x => x.Identifier),
				ReadAll(repoService.ServiceCatalog.TextParameterOptions).ToDictionary(x => x.Identifier),
				ReadAll(repoService.ServiceCatalog.ConfigurationUnits).ToDictionary(x => x.Identifier),
				ReadAll(repoService.ServiceCatalog.DiscreteValues).ToDictionary(x => x.Identifier)));
		}

		private DataRecord BuildDataRecord(
			ServiceOrderItemConfigurationValue currentConfig,
			ConfigurationParameterValue configurationParameterValue,
			ConfigurationParameter configParam,
			IReadOnlyDictionary<string, NumberParameterOptions> numberOptions,
			IReadOnlyDictionary<string, DiscreteParameterOptions> discreteOptions,
			IReadOnlyDictionary<string, TextParameterOptions> textOptions,
			IReadOnlyDictionary<string, ConfigurationUnit> units,
			IReadOnlyDictionary<string, DiscreteValue> discreteValues)
		{
			var numberOptionsId = FirstIdentifier(configurationParameterValue.NumberOptionsId, configParam.NumberOptionsId);
			var discreteOptionsId = FirstIdentifier(configurationParameterValue.DiscreteOptionsId, configParam.DiscreteOptionsId);
			var textOptionsId = FirstIdentifier(configurationParameterValue.TextOptionsId, configParam.TextOptionsId);
			var dataRecord = new DataRecord
			{
				State = State.Update,
				ServiceConfig = currentConfig,
				ConfigurationParamValue = configurationParameterValue,
				ConfigurationParam = configParam,
				NumberOptionsPersisted = !String.IsNullOrEmpty(configurationParameterValue.NumberOptionsId.Identifier),
				DiscreteOptionsPersisted = !String.IsNullOrEmpty(configurationParameterValue.DiscreteOptionsId.Identifier),
				TextOptionsPersisted = !String.IsNullOrEmpty(configurationParameterValue.TextOptionsId.Identifier),
				NumberOptions = GetValue(numberOptions, numberOptionsId),
				DiscreteOptions = GetValue(discreteOptions, discreteOptionsId),
				TextOptions = GetValue(textOptions, textOptionsId),
			};

			if (String.IsNullOrEmpty(configurationParameterValue.NumberOptionsId.Identifier) && dataRecord.NumberOptions != null)
			{
				dataRecord.NumberOptions = Clone(dataRecord.NumberOptions);
				configurationParameterValue.NumberOptionsId = new SdmObjectReference<NumberParameterOptions>(dataRecord.NumberOptions.Identifier);
			}

			if (String.IsNullOrEmpty(configurationParameterValue.DiscreteOptionsId.Identifier) && dataRecord.DiscreteOptions != null)
			{
				dataRecord.DiscreteOptions = Clone(dataRecord.DiscreteOptions);
				configurationParameterValue.DiscreteOptionsId = new SdmObjectReference<DiscreteParameterOptions>(dataRecord.DiscreteOptions.Identifier);
			}

			if (String.IsNullOrEmpty(configurationParameterValue.TextOptionsId.Identifier) && dataRecord.TextOptions != null)
			{
				dataRecord.TextOptions = Clone(dataRecord.TextOptions);
				configurationParameterValue.TextOptionsId = new SdmObjectReference<TextParameterOptions>(dataRecord.TextOptions.Identifier);
			}

			dataRecord.Units = Resolve(dataRecord.NumberOptions?.Units, units);
			dataRecord.DiscreteValues = Resolve(dataRecord.DiscreteOptions?.DiscreteValues, discreteValues);
			return dataRecord;
		}

		private void BuildHeaderRow(int row)
		{
			var lblLabel = new Label("Label") { Style = TextStyle.Heading };
			var lblParameter = new Label("Parameter") { Style = TextStyle.Heading };
			var lblLink = new Label("Link") { Style = TextStyle.Heading, MaxWidth = 50 };
			var lblNa = new Label("N/A") { Style = TextStyle.Heading, MaxWidth = 50 };
			var lblValue = new Label("Value") { Style = TextStyle.Heading };
			var lblUnit = new Label("Unit") { Style = TextStyle.Heading };
			var lblStart = new Label("Start") { Style = TextStyle.Heading };
			var lblEnd = new Label("End") { Style = TextStyle.Heading };
			var lblStop = new Label("Step Size") { Style = TextStyle.Heading };
			var lblDecimals = new Label("Decimals") { Style = TextStyle.Heading };
			var lblValues = new Label("Values") { Style = TextStyle.Heading };
			var lblDefault = new Label("Fixed") { Style = TextStyle.Heading };
			var lblMandatoryAtService = new Label("Mandatory") { Style = TextStyle.Heading };

			view.AddWidget(lblLabel, row, 0);
			view.AddWidget(lblParameter, row, 1);
			view.AddWidget(lblLink, row, 2);
			view.AddWidget(lblNa, row, 3);
			view.AddWidget(lblValue, row, 4);
			view.AddWidget(lblUnit, row, 5);

			view.AddWidget(lblStart, row, 6);
			view.AddWidget(lblEnd, row, 7);
			view.AddWidget(lblStop, row, 8);
			view.AddWidget(lblDecimals, row, 9);
			view.AddWidget(lblValues, row, 10);
			view.BtnShowValueDetails.LinkedWidgets.Add(lblStart);
			view.BtnShowValueDetails.LinkedWidgets.Add(lblEnd);
			view.BtnShowValueDetails.LinkedWidgets.Add(lblStop);
			view.BtnShowValueDetails.LinkedWidgets.Add(lblDecimals);
			view.BtnShowValueDetails.LinkedWidgets.Add(lblValues);

			view.AddWidget(lblDefault, row, 11);
			view.AddWidget(lblMandatoryAtService, row, 12);
			view.BtnShowLifeCycleDetails.LinkedWidgets.Add(lblDefault);
			view.BtnShowLifeCycleDetails.LinkedWidgets.Add(lblMandatoryAtService);
		}

		private void BuildUI(bool showDetails, bool showLifeCycleDetails)
		{
			view.Clear();
			view.BtnShowValueDetails.LinkedWidgets.Clear();
			view.BtnShowLifeCycleDetails.LinkedWidgets.Clear();

			int row = 0;
			view.AddWidget(view.TitleDetails, row, 0, 1, 2);
			view.AddWidget(new WhiteSpace(), ++row, 0);
			view.AddWidget(view.BtnShowValueDetails, ++row, 0);
			view.AddWidget(view.BtnShowLifeCycleDetails, row, 1);
			view.AddWidget(new WhiteSpace(), ++row, 0);

			BuildHeaderRow(++row);

			foreach (var configuration in configurations.Where(x => x.State != State.Delete).OrderBy(x => x.ConfigurationParam?.Name))
			{
				BuildUIRow(configuration, ++row);
			}

			if (showDetails)
			{
				view.BtnShowValueDetails.Expand();
			}
			else
			{
				view.BtnShowValueDetails.Collapse();
			}

			if (showLifeCycleDetails)
			{
				view.BtnShowLifeCycleDetails.Expand();
			}
			else
			{
				view.BtnShowLifeCycleDetails.Collapse();
			}

			view.AddWidget(new WhiteSpace(), ++row, 0);
			var parameterToAddLabel = new Label("Add Parameter:") { Style = TextStyle.Heading };
			view.AddWidget(parameterToAddLabel, ++row, 0, horizontalAlignment: HorizontalAlignment.Right);
			view.AddWidget(view.AddParameter, row, 1);
			view.AddWidget(view.BtnAddParameter, row, 2, 1, 2);

			view.AddWidget(new WhiteSpace(), ++row, 0);
			view.AddWidget(view.BtnUpdate, ++row, 0);
			view.AddWidget(view.BtnCancel, row, 1);
		}

		private void BuildUIRow(DataRecord record, int row)
		{
			// Init
			var label = new TextBox(record.ConfigurationParamValue.Label);
			var parameter = new DropDown<ConfigurationParameter>(
				new[] { new Option<ConfigurationParameter>(record.ConfigurationParam.Name, record.ConfigurationParam) })
			{
				IsEnabled = false,
			};
			var isFixed = new CheckBox { IsChecked = record.ConfigurationParamValue.ValueFixed, IsEnabled = false };
			var link = new CheckBox { IsChecked = record.ConfigurationParamValue.LinkedConfigurationReference != null, IsEnabled = !isFixed.IsChecked };
			var unit = new DropDown<ConfigurationUnit>(
				new[] { new Option<ConfigurationUnit>("-", null) })
			{ IsEnabled = false, MaxWidth = 80 };
			var start = new Numeric { IsEnabled = false, MaxWidth = 100 };
			var end = new Numeric { IsEnabled = false, MaxWidth = 100 };
			var step = new Numeric { IsEnabled = false, Minimum = 0, Maximum = 1, MaxWidth = 100 };
			var decimals = new Numeric { StepSize = 1, Minimum = 0, Maximum = 6, IsEnabled = false, MaxWidth = 80 };
			var values = new Button("...") { IsEnabled = false };
			var mandatoryAtService = new CheckBox { IsChecked = record.ServiceConfig.Mandatory, IsEnabled = false };
			var delete = new Button(Defaults.SymbolCross) { IsEnabled = !record.ServiceConfig.Mandatory };
			if (record.ServiceConfig.Mandatory)
			{
				delete.Tooltip = "This parameter is marked as mandatory on Service Specification level and cannot be deleted.";
			}

			label.Changed += (sender, args) => record.ConfigurationParamValue.Label = args.Value;
			delete.Pressed += (sender, args) =>
			{
				record.State = State.Delete;
				instance.Configurations.RemoveAll(reference => reference.Identifier == record.ServiceConfig.Identifier);
				BuildUI(!view.BtnShowValueDetails.IsCollapsed, !view.BtnShowLifeCycleDetails.IsCollapsed);
			};
			link.Changed += (sender, args) =>
			{
				record.ConfigurationParamValue.LinkedConfigurationReference = args.IsChecked ? "Dummy Link" : null;
				BuildUI(!view.BtnShowValueDetails.IsCollapsed, !view.BtnShowLifeCycleDetails.IsCollapsed);
			};

			if (record.ConfigurationParamValue.LinkedConfigurationReference != null)
			{
				view.AddWidget(new DropDown(), row, 3);
			}
			else
			{
				switch (parameter.Selected.Type)
				{
					case SlcConfigurationsIds.Enums.Type.Number:
						AddNumericParam(record, row, parameter, isFixed, unit, start, end, step, decimals);
						break;

					case SlcConfigurationsIds.Enums.Type.Discrete:
						AddDiscreteParam(record, row, parameter, isFixed, values);
						break;

					case SlcConfigurationsIds.Enums.Type.Text:
						AddTextParam(record, row, isFixed);
						break;

					default:
						return;
				}
			}

			// Populate row
			view.AddWidget(label, row, 0);
			view.AddWidget(parameter, row, 1);
			view.AddWidget(link, row, 2);
			//// columns 3/4 reserverd for N/A and Value
			view.AddWidget(unit, row, 5);

			view.AddWidget(start, row, 6);
			view.AddWidget(end, row, 7);
			view.AddWidget(step, row, 8);
			view.AddWidget(decimals, row, 9);
			view.AddWidget(values, row, 10);
			view.BtnShowValueDetails.LinkedWidgets.Add(start);
			view.BtnShowValueDetails.LinkedWidgets.Add(end);
			view.BtnShowValueDetails.LinkedWidgets.Add(step);
			view.BtnShowValueDetails.LinkedWidgets.Add(decimals);
			view.BtnShowValueDetails.LinkedWidgets.Add(values);

			view.AddWidget(isFixed, row, 11);
			view.AddWidget(mandatoryAtService, row, 12);
			view.BtnShowLifeCycleDetails.LinkedWidgets.Add(isFixed);
			view.BtnShowLifeCycleDetails.LinkedWidgets.Add(mandatoryAtService);

			view.AddWidget(delete, row, 13);
		}

		private void AddTextParam(DataRecord record, int row, CheckBox isFixed)
		{
			bool hasValue = !String.IsNullOrEmpty(record.ConfigurationParamValue.StringValue);
			var value = new TextBox(record.ConfigurationParamValue.StringValue ?? record.TextOptions?.Default ?? String.Empty)
			{
				Tooltip = record.TextOptions?.UserMessage ?? String.Empty,
				IsEnabled = (isFixed.IsChecked && !hasValue) || hasValue,
			};
			value.Changed += (sender, args) =>
			{
				if (record.TextOptions?.Regex != null && !Regex.IsMatch(args.Value, record.TextOptions.Regex))
				{
					value.ValidationState = UIValidationState.Invalid;
					value.ValidationText = $"Input did not match Regex '{record.TextOptions.Regex}' - reverted to previous value";
					value.Text = args.Previous;
					return;
				}

				value.ValidationState = UIValidationState.Valid;
				value.ValidationText = record.TextOptions?.UserMessage;
				record.ConfigurationParamValue.StringValue = args.Value;
			};

			var na = new CheckBox { IsChecked = !hasValue };
			na.Changed += (sender, args) =>
			{
				value.IsEnabled = !args.IsChecked;
				if (args.IsChecked)
				{
					record.ConfigurationParamValue.StringValue = null;
				}
			};
			view.AddWidget(na, row, 3);
			view.AddWidget(value, row, 4);
		}

		private void AddDiscreteParam(DataRecord record, int row, DropDown<ConfigurationParameter> parameter, CheckBox isFixed, Button values)
		{
			if (record.DiscreteOptions == null)
			{
				throw new InvalidOperationException($"DiscreteOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
			}

			var allDiscretes = record.DiscreteValues
				.Select(x => new Option<DiscreteValue>(x.Value, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();

			bool hasValue = record.ConfigurationParamValue.StringValue != null && allDiscretes.Any(x => x.DisplayValue == record.ConfigurationParamValue.StringValue);
			bool widgetEnabled = (isFixed.IsChecked && !hasValue) || hasValue;
			var value = new DropDown<DiscreteValue>(allDiscretes)
			{
				IsEnabled = widgetEnabled,
			};
			if (hasValue)
			{
				value.Selected = value.Options.First(x => x.DisplayValue == record.ConfigurationParamValue.StringValue).Value;
			}

			values.IsEnabled = widgetEnabled;
			if (record.ConfigurationParamValue.StringValue == null)
			{
				record.ConfigurationParamValue.StringValue = value.Selected?.Value;
			}

			value.Changed += (sender, args) => { record.ConfigurationParamValue.StringValue = args.SelectedOption.DisplayValue; };
			values.Pressed += (sender, args) =>
			{
				var optionsView = new DiscreteValuesView(engine);
				optionsView.Options.SetOptions(allDiscretes);
				foreach (var option in optionsView.Options.Values.ToList())
				{
					if (value.Options.Any(o => o.Value.Equals(option)))
					{
						optionsView.Options.Check(option); // check only the available items.
					}
				}

				optionsView.BtnApply.Pressed += (o, eventArgs) =>
				{
					value.SetOptions(optionsView.Options.CheckedOptions);
					record.ConfigurationParamValue.StringValue = value.Selected?.Value;
					record.DiscreteValues = optionsView.Options.Checked.ToList();
					record.DiscreteOptions.DiscreteValues = record.DiscreteValues
						.Select(x => new SdmObjectReference<DiscreteValue>(x.Identifier))
						.ToList();
					controller.ShowDialog(view);
				};
				optionsView.BtnCancel.Pressed += (o, eventArgs) => controller.ShowDialog(view);
				controller.ShowDialog(optionsView);
			};

			var na = new CheckBox { IsChecked = !hasValue };
			na.Changed += (sender, args) =>
			{
				value.IsEnabled = !args.IsChecked;
				if (args.IsChecked)
				{
					record.ConfigurationParamValue.StringValue = null;
				}
			};
			view.AddWidget(na, row, 3);
			view.AddWidget(value, row, 4);
		}

		private void AddNumericParam(DataRecord record, int row, DropDown<ConfigurationParameter> parameter, CheckBox isFixed, DropDown<ConfigurationUnit> unit, Numeric start, Numeric end, Numeric step, Numeric decimals)
		{
			if (record.NumberOptions == null)
			{
				throw new InvalidOperationException($"NumberOptions is null for parameter: {record.ConfigurationParam?.Name ?? "Unknown"}");
			}

			bool hasValue = record.ConfigurationParamValue.DoubleValue.HasValue || record.NumberOptions.DefaultValue.HasValue;
			double minimum = record.NumberOptions.MinRange ?? int.MinValue;
			double maximum = record.NumberOptions.MaxRange ?? int.MaxValue;
			int decimalVal = Convert.ToInt32(record.NumberOptions.Decimals);
			double stepSize = record.NumberOptions.StepSize ?? 1;
			bool widgetEnabled = (isFixed.IsChecked && !hasValue) || hasValue;
			Numeric value = new Numeric(record.ConfigurationParamValue.DoubleValue ?? record.NumberOptions.DefaultValue ?? 0)
			{
				Minimum = minimum,
				Maximum = maximum,
				StepSize = stepSize,
				Decimals = decimalVal,
				IsEnabled = widgetEnabled,
			};
			unit.SetOptions(GetUnits(record));
			unit.Selected = GetDefaultUnit(record);
			unit.IsEnabled = widgetEnabled;
			start.Value = minimum;
			start.IsEnabled = widgetEnabled;
			end.Value = maximum;
			end.IsEnabled = widgetEnabled;
			decimals.Value = decimalVal;
			decimals.IsEnabled = widgetEnabled;
			step.Value = stepSize;
			step.StepSize = 1 / Math.Pow(10, decimalVal);
			step.Decimals = decimalVal;
			step.IsEnabled = widgetEnabled;

			start.Changed += (sender, args) =>
			{
				value.Minimum = args.Value;
				record.NumberOptions.MinRange = args.Value;
			};
			end.Changed += (sender, args) =>
			{
				value.Maximum = args.Value;
				record.NumberOptions.MaxRange = args.Value;
			};
			decimals.Changed += (sender, args) =>
			{
				value.Decimals = Convert.ToInt32(args.Value);
				step.Decimals = Convert.ToInt32(args.Value);
				double newStepsize = 1 / Math.Pow(10, args.Value);
				value.StepSize = newStepsize;
				step.StepSize = newStepsize;
				record.NumberOptions.Decimals = Convert.ToInt32(args.Value);
			};
			step.Changed += (sender, args) =>
			{
				value.StepSize = args.Value;
				record.NumberOptions.StepSize = args.Value;
			};
			unit.Changed += (sender, args) =>
				record.NumberOptions.DefaultUnitId = args.Selected == null
					? default
					: new SdmObjectReference<ConfigurationUnit>(args.Selected.Identifier);
			value.Changed += (sender, args) => { record.ConfigurationParamValue.DoubleValue = args.Value; };

			var na = new CheckBox { IsChecked = !widgetEnabled };
			na.Changed += (sender, args) =>
			{
				value.IsEnabled = !args.IsChecked;
				if (args.IsChecked)
				{
					record.ConfigurationParamValue.DoubleValue = null;
				}
			};
			view.AddWidget(na, row, 3);
			view.AddWidget(value, row, 4);
		}

		private static ConfigurationUnit GetDefaultUnit(DataRecord record)
		{
			return record.Units.Find(x => x.Identifier == record.NumberOptions?.DefaultUnitId.Identifier);
		}

		private static List<Option<ConfigurationUnit>> GetUnits(DataRecord record)
		{
			var units = record.Units
				.Select(x => new Option<ConfigurationUnit>(x.Name, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();
			units.Insert(0, new Option<ConfigurationUnit>("-", null));
			return units;
		}

		private static List<T> ReadAll<T>(IBulkRepository<T> repository)
			where T : SdmObject<T>
		{
			return repository.Read(new TRUEFilterElement<T>()).ToList();
		}

		private static string FirstIdentifier<T>(SdmObjectReference<T> preferred, SdmObjectReference<T> fallback)
			where T : SdmObject<T>
		{
			return String.IsNullOrEmpty(preferred.Identifier) ? fallback.Identifier : preferred.Identifier;
		}

		private static T GetValue<T>(IReadOnlyDictionary<string, T> values, string identifier)
			where T : class
		{
			return !String.IsNullOrEmpty(identifier) && values.TryGetValue(identifier, out var value) ? value : null;
		}

		private static List<T> Resolve<T>(IEnumerable<SdmObjectReference<T>> references, IReadOnlyDictionary<string, T> values)
			where T : SdmObject<T>
		{
			return references?
				.Select(reference => GetValue(values, reference.Identifier))
				.Where(value => value != null)
				.ToList() ?? new List<T>();
		}

		private static NumberParameterOptions Clone(NumberParameterOptions source)
		{
			return new NumberParameterOptions
			{
				Identifier = Guid.NewGuid().ToString(),
				Units = source.Units.ToList(),
				DefaultUnitId = source.DefaultUnitId,
				MinRange = source.MinRange,
				MaxRange = source.MaxRange,
				Decimals = source.Decimals,
				StepSize = source.StepSize,
				DefaultValue = source.DefaultValue,
			};
		}

		private static DiscreteParameterOptions Clone(DiscreteParameterOptions source)
		{
			return new DiscreteParameterOptions
			{
				Identifier = Guid.NewGuid().ToString(),
				DiscreteValues = source.DiscreteValues.ToList(),
				DefaultDiscreteValueId = source.DefaultDiscreteValueId,
			};
		}

		private static TextParameterOptions Clone(TextParameterOptions source)
		{
			return new TextParameterOptions
			{
				Identifier = Guid.NewGuid().ToString(),
				Regex = source.Regex,
				UserMessage = source.UserMessage,
				Default = source.Default,
			};
		}

		private void CreateOrUpdateOptions(DataRecord record)
		{
			if (record.NumberOptions != null)
			{
				repoService.ServiceCatalog.NumberParameterOptions.CreateOrUpdate(new[] { record.NumberOptions });
			}

			if (record.DiscreteOptions != null)
			{
				repoService.ServiceCatalog.DiscreteParameterOptions.CreateOrUpdate(new[] { record.DiscreteOptions });
			}

			if (record.TextOptions != null)
			{
				repoService.ServiceCatalog.TextParameterOptions.CreateOrUpdate(new[] { record.TextOptions });
			}
		}

		private void DeleteOptions(DataRecord record)
		{
			if (record.NumberOptionsPersisted && record.NumberOptions != null)
			{
				repoService.ServiceCatalog.NumberParameterOptions.Delete(record.NumberOptions);
			}

			if (record.DiscreteOptionsPersisted && record.DiscreteOptions != null)
			{
				repoService.ServiceCatalog.DiscreteParameterOptions.Delete(record.DiscreteOptions);
			}

			if (record.TextOptionsPersisted && record.TextOptions != null)
			{
				repoService.ServiceCatalog.TextParameterOptions.Delete(record.TextOptions);
			}
		}

		private void DeleteConfiguration(DataRecord record)
		{
			string configurationParameterValueId = record.ConfigurationParamValue.Identifier;
			bool isShared = ReadAll(repoService.ServiceOrder.ServiceOrderItemConfigurationValues)
				.Any(configuration =>
					configuration.Identifier != record.ServiceConfig.Identifier &&
					configuration.ConfigurationParameterValueId.Identifier == configurationParameterValueId);

			repoService.ServiceOrder.ServiceOrderItemConfigurationValues.Delete(record.ServiceConfig);
			if (isShared)
			{
				return;
			}

			repoService.ServiceCatalog.ConfigurationParameterValues.Delete(record.ConfigurationParamValue);
			DeleteOptions(record);
		}

		private sealed class DataRecord
		{
			public State State { get; set; }

			public ServiceOrderItemConfigurationValue ServiceConfig { get; set; }

			public ConfigurationParameterValue ConfigurationParamValue { get; set; }

			public ConfigurationParameter ConfigurationParam { get; set; }

			public NumberParameterOptions NumberOptions { get; set; }

			public DiscreteParameterOptions DiscreteOptions { get; set; }

			public TextParameterOptions TextOptions { get; set; }

			public bool NumberOptionsPersisted { get; set; }

			public bool DiscreteOptionsPersisted { get; set; }

			public bool TextOptionsPersisted { get; set; }

			public List<ConfigurationUnit> Units { get; set; } = new List<ConfigurationUnit>();

			public List<DiscreteValue> DiscreteValues { get; set; } = new List<DiscreteValue>();
		}
	}
}