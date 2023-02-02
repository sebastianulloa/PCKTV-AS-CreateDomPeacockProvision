/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

02/02/2023	1.0.0.1		GBS, Skyline	Initial version
****************************************************************************
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Status;
using Skyline.DataMiner.Net.Apps.Sections.SectionDefinitions;
using Skyline.DataMiner.Net.Messages.SLDataGateway;
using Skyline.DataMiner.Net.Sections;

/// <summary>
/// DataMiner Script Class.
/// </summary>
public class Script
{
	private Engine engine;

	/// <summary>
	/// The Script entry point.
	/// </summary>
	/// <param name="engine">Link with SLAutomation process.</param>
	public void Run(Engine engine)
	{
		this.engine = engine;
		var domHelper = new DomHelper(engine.SendSLNetMessages, "test_process_automation");

		var peacockProvisionDomDefinition = CreateDomDefinition(domHelper);
		if (peacockProvisionDomDefinition != null)
		{
			var domDefinition = domHelper.DomDefinitions.Read(DomDefinitionExposers.Name.Equal(peacockProvisionDomDefinition.Name));
			if (domDefinition.Any())
			{
				peacockProvisionDomDefinition.ID = domDefinition.FirstOrDefault()?.ID;
				domHelper.DomDefinitions.Update(peacockProvisionDomDefinition);
			}
			else
			{
				domHelper.DomDefinitions.Create(peacockProvisionDomDefinition);
			}
		}
	}

	private DomDefinition CreateDomDefinition(DomHelper domHelper)
	{
		try
		{
			// Create SectionDefinitions
			var provisionInfoSectionDefinitions = SectionDefinitions.CreateProvisionInfoServiceDefinition(engine, domHelper);
			var domInstancesSectionDefinitions = SectionDefinitions.CreateDomInstancesServiceDefinition(engine, domHelper);

			// Create DomBehaviorDefinition
			var behavior = domHelper.DomBehaviorDefinitions.Read(DomBehaviorDefinitionExposers.Name.Equal("Peacock Provision"));
			if (!behavior.Any())
			{
				var domBehaviorDefinition = BehaviorDefinitions.CreateDomBehaviorDefinition(engine, provisionInfoSectionDefinitions.FirstOrDefault(), domInstancesSectionDefinitions.FirstOrDefault());
				domBehaviorDefinition = domHelper.DomBehaviorDefinitions.Create(domBehaviorDefinition);
				behavior = new List<DomBehaviorDefinition> { domBehaviorDefinition };
			}

			// create dom definition
			return new DomDefinition
			{
				Name = "Test Peacock Provision",
				SectionDefinitionLinks = new List<SectionDefinitionLink> { new SectionDefinitionLink(provisionInfoSectionDefinitions.FirstOrDefault()?.GetID()), new SectionDefinitionLink(domInstancesSectionDefinitions.FirstOrDefault()?.GetID()) },
				DomBehaviorDefinitionId = behavior.FirstOrDefault()?.ID,
			};
		}
		catch(Exception ex)
		{
			engine.Log($"error on CreateDomDefinition method with exception {ex}");
			return null;
		}
	}

	public class SectionDefinitions
	{
		public static List<SectionDefinition> CreateDomInstancesServiceDefinition(Engine engine, DomHelper domHelper)
		{
			var convivaFieldDescriptor = CreateDomInstanceFieldDescriptorObject<Guid>("Conviva", "Link to the DOM Instance that contains the information for Conviva provisioning.");
			var tagFieldDescriptor = CreateDomInstanceFieldDescriptorObject<Guid>("TAG", "Link to the DOM Instance that contains the information for TAG provisioning.");
			var touchstreamFieldDescriptor = CreateDomInstanceFieldDescriptorObject<Guid>("Touchstream", "Link to the DOM Instance that contains the information for TS provisioning.");

			var domInstancesSectionDefinition = new CustomSectionDefinition
			{
				Name = "DOM Instances",
			};
			domInstancesSectionDefinition.AddOrReplaceFieldDescriptor(convivaFieldDescriptor);
			domInstancesSectionDefinition.AddOrReplaceFieldDescriptor(tagFieldDescriptor);
			domInstancesSectionDefinition.AddOrReplaceFieldDescriptor(touchstreamFieldDescriptor);

			var domInstanceSection = domHelper.SectionDefinitions.Read(SectionDefinitionExposers.Name.Equal(domInstancesSectionDefinition.Name));
			if (!domInstanceSection.Any())
			{
				domInstancesSectionDefinition = domHelper.SectionDefinitions.Create(domInstancesSectionDefinition) as CustomSectionDefinition;
				domInstanceSection = new List<SectionDefinition> { domInstancesSectionDefinition };
			}
			else
			{
				// Update Section Definition (Add missing fieldDescriptors)
				List<FieldDescriptor> fieldDescriptorList = new List<FieldDescriptor> { convivaFieldDescriptor, tagFieldDescriptor, touchstreamFieldDescriptor };
				domInstanceSection = UpdateSectionDefinition(domHelper, fieldDescriptorList, domInstanceSection);
			}

			return domInstanceSection;
		}

		public static List<SectionDefinition> CreateProvisionInfoServiceDefinition(Engine engine, DomHelper domHelper)
		{
			var provisionNameFieldDescriptor = CreateFieldDescriptorObject<string>("Provision Name", "A name to describe the Event or Channel being provisioned.");
			var eventIdFieldDescriptor = CreateFieldDescriptorObject<string>("Event ID", "Unique ID to link the provision to an Event or Channel.");
			var sourceElementFieldDescriptor = CreateFieldDescriptorObject<string>("Source Element", "A DMAID/ELEMID/PID that has been configured to receive process updates (if configured).");

			var provisionInfoSectionDefinition = new CustomSectionDefinition
			{
				Name = "Provision Info",
			};
			provisionInfoSectionDefinition.AddOrReplaceFieldDescriptor(provisionNameFieldDescriptor);
			provisionInfoSectionDefinition.AddOrReplaceFieldDescriptor(eventIdFieldDescriptor);
			provisionInfoSectionDefinition.AddOrReplaceFieldDescriptor(sourceElementFieldDescriptor);

			var provisionInfoSection = domHelper.SectionDefinitions.Read(SectionDefinitionExposers.Name.Equal(provisionInfoSectionDefinition.Name));
			if (!provisionInfoSection.Any())
			{
				provisionInfoSectionDefinition = domHelper.SectionDefinitions.Create(provisionInfoSectionDefinition) as CustomSectionDefinition;
				provisionInfoSection = new List<SectionDefinition> { provisionInfoSectionDefinition };
			}
			else
			{
				// Update Section Definition (Add missing fieldDescriptors)
				List<FieldDescriptor> fieldDescriptorList = new List<FieldDescriptor> { provisionNameFieldDescriptor, eventIdFieldDescriptor, sourceElementFieldDescriptor };
				provisionInfoSection = UpdateSectionDefinition(domHelper, fieldDescriptorList, provisionInfoSection);
			}

			return provisionInfoSection;
		}

		private static List<SectionDefinition> UpdateSectionDefinition(DomHelper domHelper, List<FieldDescriptor> fieldDescriptorList, List<SectionDefinition> sectionDefinition)
		{
			var existingSectionDefinition = sectionDefinition.First() as CustomSectionDefinition;
			var previousFieldNames = existingSectionDefinition.GetAllFieldDescriptors().Select(x => x.Name).ToList();
			List<FieldDescriptor> fieldDescriptorsToAdd = new List<FieldDescriptor>();

			// Check if there's a fieldDefinition to add
			foreach (var newfieldDescriptor in fieldDescriptorList)
			{
				if (!previousFieldNames.Contains(newfieldDescriptor.Name))
				{
					fieldDescriptorsToAdd.Add(newfieldDescriptor);
				}
			}

			if (fieldDescriptorsToAdd.Count > 0)
			{
				foreach (var field in fieldDescriptorsToAdd)
				{
					existingSectionDefinition.AddOrReplaceFieldDescriptor(field);
				}

				existingSectionDefinition = domHelper.SectionDefinitions.Update(existingSectionDefinition) as CustomSectionDefinition;
				sectionDefinition = new List<SectionDefinition> { existingSectionDefinition };
			}

			return sectionDefinition;
		}

		private static FieldDescriptor CreateFieldDescriptorObject<T>(string fieldName, string toolTip)
		{
			return new FieldDescriptor
			{
				FieldType = typeof(T),
				Name = fieldName,
				Tooltip = toolTip,
			};
		}

		private static DomInstanceFieldDescriptor CreateDomInstanceFieldDescriptorObject<T>(string fieldName, string toolTip)
		{
			return new DomInstanceFieldDescriptor
			{
				FieldType = typeof(T),
				Name = fieldName,
				Tooltip = toolTip,
			};
		}
	}

	public class BehaviorDefinitions
	{
		public static DomBehaviorDefinition CreateDomBehaviorDefinition(Engine engine, SectionDefinition provisionInfoSectionDefinition, SectionDefinition domInstancesSectionDefinition)
		{
			try
			{
				var statuses = new List<DomStatus>
				{
					new DomStatus("draft", "Draft"),
					new DomStatus("ready", "Ready"),
					new DomStatus("in_progress", "In Progress"),
					new DomStatus("active", "Active"),
					new DomStatus("deactivate", "Deactivate"),
					new DomStatus("reprovision", "Reprovision"),
					new DomStatus("complete", "Complete"),
				};

				var transitions = new List<DomStatusTransition>
				{
					new DomStatusTransition("draft_to_ready", "draft", "ready"),
					new DomStatusTransition("ready_to_inprogress", "ready", "in_progress"),
					new DomStatusTransition("inprogress_to_active", "in_progress", "active"),
					new DomStatusTransition("active_to_deactivate", "active", "deactivate"),
					new DomStatusTransition("active_to_reprovision", "active", "reprovision"),
					new DomStatusTransition("deactivate_to_complete", "deactivate", "complete"),
					new DomStatusTransition("reprovision_to_inprogress", "reprovision", "in_progress"),
					new DomStatusTransition("complete_to_ready", "complete", "ready"),
				};

				return new DomBehaviorDefinition
				{
					Name = "Peacock Provision",
					InitialStatusId = "draft",
					Statuses = statuses,
					StatusTransitions = transitions,
					StatusSectionDefinitionLinks = GetStatusLinks(engine, provisionInfoSectionDefinition, domInstancesSectionDefinition),
				};
			}
			catch (Exception ex)
			{
				engine.Log($"Error on CreateDomBehaviorDefinition method with exception: {ex}");
				return null;
			}
		}

		private static List<DomStatusSectionDefinitionLink> GetStatusLinks(Engine engine, SectionDefinition provisionInfoSectionDefinition, SectionDefinition domInstancesSectionDefinition)
		{
			try
			{
				Dictionary<string, FieldDescriptorID> fieldsList = GetFieldDescriptorDictionary(provisionInfoSectionDefinition, domInstancesSectionDefinition);

				var draftStatusLinks = StatusSectionDefinitions.GetDraftStatusSectionDefinitionLinks(provisionInfoSectionDefinition, domInstancesSectionDefinition, fieldsList);
				var readyStatusLinks = StatusSectionDefinitions.GetGeneralStatusSectionDefinitionLinks(provisionInfoSectionDefinition, domInstancesSectionDefinition, fieldsList, "ready");
				var inprogressStatusLinks = StatusSectionDefinitions.GetGeneralStatusSectionDefinitionLinks(provisionInfoSectionDefinition, domInstancesSectionDefinition, fieldsList, "in_progress");
				var activeStatusLinks = StatusSectionDefinitions.GetGeneralStatusSectionDefinitionLinks(provisionInfoSectionDefinition, domInstancesSectionDefinition, fieldsList, "active");
				var deactivateStatusLinks = StatusSectionDefinitions.GetGeneralStatusSectionDefinitionLinks(provisionInfoSectionDefinition, domInstancesSectionDefinition, fieldsList, "deactivate");
				var reprovisionStatusLinks = StatusSectionDefinitions.GetGeneralStatusSectionDefinitionLinks(provisionInfoSectionDefinition, domInstancesSectionDefinition, fieldsList, "reprovision");
				var completeStatusLinks = StatusSectionDefinitions.GetGeneralStatusSectionDefinitionLinks(provisionInfoSectionDefinition, domInstancesSectionDefinition, fieldsList, "complete");

				return draftStatusLinks.Concat(readyStatusLinks).Concat(inprogressStatusLinks).Concat(activeStatusLinks).Concat(deactivateStatusLinks).Concat(reprovisionStatusLinks).Concat(completeStatusLinks).ToList();
			}
			catch (Exception ex)
			{
				engine.Log($"Error on CreateDomBehaviorDefinition method with exception: {ex}");
				return null;
			}
		}

		private static Dictionary<string, FieldDescriptorID> GetFieldDescriptorDictionary(SectionDefinition provisionInfoSectionDefinition, SectionDefinition domInstancesSectionDefinition)
		{
			var provisionInfoFields = provisionInfoSectionDefinition.GetAllFieldDescriptors();
			Dictionary<string, FieldDescriptorID> fieldsList = new Dictionary<string, FieldDescriptorID>();
			foreach (var field in provisionInfoFields)
			{
				fieldsList[field.Name] = field.ID;
			}

			var domInstanceFields = domInstancesSectionDefinition.GetAllFieldDescriptors();
			foreach (var field in domInstanceFields)
			{
				fieldsList[field.Name] = field.ID;
			}

			return fieldsList;
		}

		public class StatusSectionDefinitions
		{
			public static List<DomStatusSectionDefinitionLink> GetDraftStatusSectionDefinitionLinks(SectionDefinition provisionInfoSectionDefinition, SectionDefinition domInstancesSectionDefinition, Dictionary<string, FieldDescriptorID> fieldsList)
			{
				var draftProvisionInfoStatusLink = new DomStatusSectionDefinitionLinkId("draft", provisionInfoSectionDefinition.GetID());
				var draftDomInstanceStatusLink = new DomStatusSectionDefinitionLinkId("draft", domInstancesSectionDefinition.GetID());

				var draftStatusLinkProvisionInfo = new DomStatusSectionDefinitionLink(draftProvisionInfoStatusLink)
				{
					FieldDescriptorLinks = new List<DomStatusFieldDescriptorLink>
				{
					new DomStatusFieldDescriptorLink(fieldsList["Provision Name"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = true,
					},
					new DomStatusFieldDescriptorLink(fieldsList["Event ID"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = true,
					},
					new DomStatusFieldDescriptorLink(fieldsList["Source Element"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = false,
					},
				},
				};
				var draftStatusLinkDomInstance = new DomStatusSectionDefinitionLink(draftDomInstanceStatusLink)
				{
					FieldDescriptorLinks = new List<DomStatusFieldDescriptorLink>
				{
					new DomStatusFieldDescriptorLink(fieldsList["Conviva"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = false,
					},
					new DomStatusFieldDescriptorLink(fieldsList["TAG"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = false,
					},
					new DomStatusFieldDescriptorLink(fieldsList["Touchstream"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = false,
					},
				},
				};

				return new List<DomStatusSectionDefinitionLink>() { draftStatusLinkProvisionInfo, draftStatusLinkDomInstance };
			}

			public static List<DomStatusSectionDefinitionLink> GetGeneralStatusSectionDefinitionLinks(SectionDefinition provisionInfoSectionDefinition, SectionDefinition domInstancesSectionDefinition, Dictionary<string, FieldDescriptorID> fieldsList, string status)
			{
				var provisionInfoStatusLink = new DomStatusSectionDefinitionLinkId(status, provisionInfoSectionDefinition.GetID());
				var domInstanceStatusLink = new DomStatusSectionDefinitionLinkId(status, domInstancesSectionDefinition.GetID());

				var statusLinkProvisionInfo = new DomStatusSectionDefinitionLink(provisionInfoStatusLink)
				{
					FieldDescriptorLinks = new List<DomStatusFieldDescriptorLink>
				{
					new DomStatusFieldDescriptorLink(fieldsList["Provision Name"])
					{
						Visible = true,
						ReadOnly = true,
						RequiredForStatus = false,
					},
					new DomStatusFieldDescriptorLink(fieldsList["Event ID"])
					{
						Visible = true,
						ReadOnly = true,
						RequiredForStatus = false,
					},
					new DomStatusFieldDescriptorLink(fieldsList["Source Element"])
					{
						Visible = true,
						ReadOnly = true,
						RequiredForStatus = false,
					},
				},
				};
				var statusLinkDomInstance = new DomStatusSectionDefinitionLink(domInstanceStatusLink)
				{
					FieldDescriptorLinks = new List<DomStatusFieldDescriptorLink>
				{
					new DomStatusFieldDescriptorLink(fieldsList["Conviva"])
					{
						Visible = true,
						ReadOnly = true,
						RequiredForStatus = false,
					},
					new DomStatusFieldDescriptorLink(fieldsList["TAG"])
					{
						Visible = true,
						ReadOnly = true,
						RequiredForStatus = false,
					},
					new DomStatusFieldDescriptorLink(fieldsList["Touchstream"])
					{
						Visible = true,
						ReadOnly = true,
						RequiredForStatus = false,
					},
				},
				};

				return new List<DomStatusSectionDefinitionLink>() { statusLinkProvisionInfo, statusLinkDomInstance };
			}
		}
	}
}