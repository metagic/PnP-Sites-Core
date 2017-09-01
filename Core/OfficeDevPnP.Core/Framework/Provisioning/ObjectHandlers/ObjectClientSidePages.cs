﻿using Microsoft.SharePoint.Client;
using Newtonsoft.Json.Linq;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers
{
#if !ONPREMISES
    internal class ObjectClientSidePages: ObjectHandlerBase
    {
        public override string Name
        {
            get { return "ClientSidePages"; }
        }

        public override TokenParser ProvisionObjects(Web web, ProvisioningTemplate template, TokenParser parser, ProvisioningTemplateApplyingInformation applyingInformation)
        {
            using (var scope = new PnPMonitoredScope(this.Name))
            {
                var context = web.Context as ClientContext;

                web.EnsureProperties(w => w.ServerRelativeUrl);

                // Check if this is not a noscript site as we're not allowed to update some properties
                bool isNoScriptSite = web.IsNoScriptSite();

                foreach (var clientSidePage in template.ClientSidePages)
                {
                    // determine pages library
                    string pagesLibrary = "SitePages";
                    string pageName = $"{System.IO.Path.GetFileNameWithoutExtension(clientSidePage.PageName)}.aspx";

                    string url = $"{pagesLibrary}/{pageName}";

                    url = UrlUtility.Combine(web.ServerRelativeUrl, url);

                    var exists = true;
                    Microsoft.SharePoint.Client.File file = null;
                    try
                    {
                        file = web.GetFileByServerRelativeUrl(url);
                        web.Context.Load(file);
                        web.Context.ExecuteQueryRetry();
                    }
                    catch (ServerException ex)
                    {
                        if (ex.ServerErrorTypeName == "System.IO.FileNotFoundException")
                        {
                            exists = false;
                        }
                    }

                    Pages.ClientSidePage page = null;
                    if (exists)
                    {
                        if (clientSidePage.Overwrite)
                        {
                            // Get the existing page
                            page = web.LoadClientSidePage(pageName);
                            // Clear the page
                            page.ClearPage();
                        }
                        else
                        {
                            scope.LogWarning(CoreResources.Provisioning_ObjectHandlers_ClientSidePages_NoOverWrite, pageName);
                        }
                    }
                    else
                    {
                        // Create new client side page
                        page = web.AddClientSidePage(pageName);
                    }

                    // Load existing available controls
                    var componentsToAdd = page.AvailableClientSideComponents();

                    // if no section specified then add a default single column section
                    if (!clientSidePage.Sections.Any())
                    {
                        clientSidePage.Sections.Add(new CanvasSection() { Type = CanvasSectionType.OneColumn, Order = 10 });
                    }

                    int sectionCount = -1;
                    // Apply the "layout" and content
                    foreach(var section in clientSidePage.Sections)
                    {
                        sectionCount++;
                        switch (section.Type)
                        {
                            case CanvasSectionType.OneColumn:
                                page.AddSection(Pages.CanvasSectionTemplate.OneColumn, section.Order);
                                break;
                            case CanvasSectionType.OneColumnFullWidth:
                                page.AddSection(Pages.CanvasSectionTemplate.OneColumnFullWidth, section.Order);
                                break;
                            case CanvasSectionType.TwoColumn:
                                page.AddSection(Pages.CanvasSectionTemplate.TwoColumn, section.Order);
                                break;
                            case CanvasSectionType.ThreeColumn:
                                page.AddSection(Pages.CanvasSectionTemplate.ThreeColumn, section.Order);
                                break;
                            case CanvasSectionType.TwoColumnLeft:
                                page.AddSection(Pages.CanvasSectionTemplate.TwoColumnLeft, section.Order);
                                break;
                            case CanvasSectionType.TwoColumnRight:
                                page.AddSection(Pages.CanvasSectionTemplate.TwoColumnRight, section.Order);
                                break;
                            default:
                                page.AddSection(Pages.CanvasSectionTemplate.OneColumn, section.Order);
                                break;
                        }
                        
                        // Add controls to the section
                        if (section.Controls.Any())
                        {
                            foreach(var control in section.Controls)
                            {
                                Pages.ClientSideComponent baseControl = null;

                                // Is it a text control?
                                if (control.Type == WebPartType.Text)
                                {
                                    Pages.ClientSideText textControl = new Pages.ClientSideText();
                                    if (control.ControlProperties.Any())
                                    {
                                        var textProperty = control.ControlProperties.First();
                                        textControl.Text = textProperty.Value;
                                        page.AddControl(textControl, page.Sections[sectionCount].Columns[control.Column], control.Order);
                                    }
                                }
                                // It is a web part
                                else
                                {
                                    // Is a custom developed client side web part (3rd party)
                                    if (control.Type == WebPartType.Custom)
                                    {
                                        if (!string.IsNullOrEmpty(control.CustomWebPartName))
                                        {
                                            baseControl = componentsToAdd.Where(p => p.Name.Equals(control.CustomWebPartName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                                        }
                                        else if (control.ControlId != Guid.Empty)
                                        {
                                            baseControl = componentsToAdd.Where(p => p.Id.Equals($"{{{control.ControlId.ToString()}}}", StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                                        }
                                    }
                                    // Is an OOB client side web part (1st party)
                                    else
                                    {
                                        string webPartName = "";
                                        switch (control.Type)
                                        {
                                            case WebPartType.Image:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.Image);
                                                break;
                                            case WebPartType.BingMap:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.BingMap);
                                                break;
                                            case WebPartType.ContentEmbed:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.ContentEmbed);
                                                break;
                                            case WebPartType.ContentRollup:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.ContentRollup);
                                                break;
                                            case WebPartType.DocumentEmbed:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.DocumentEmbed);
                                                break;
                                            case WebPartType.Events:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.Events);
                                                break;
                                            case WebPartType.GroupCalendar:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.GroupCalendar);
                                                break;
                                            case WebPartType.Hero:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.Hero);
                                                break;
                                            case WebPartType.ImageGallery:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.ImageGallery);
                                                break;
                                            case WebPartType.LinkPreview:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.LinkPreview);
                                                break;
                                            case WebPartType.List:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.List);
                                                break;
                                            case WebPartType.NewsFeed:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.NewsFeed);
                                                break;
                                            case WebPartType.NewsReel:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.NewsReel);
                                                break;
                                            case WebPartType.PageTitle:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.PageTitle);
                                                break;
                                            case WebPartType.People:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.People);
                                                break;
                                            case WebPartType.PowerBIReportEmbed:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.PowerBIReportEmbed);
                                                break;
                                            case WebPartType.QuickChart:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.QuickChart);
                                                break;
                                            case WebPartType.QuickLinks:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.QuickLinks);
                                                break;
                                            case WebPartType.SiteActivity:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.SiteActivity);
                                                break;
                                            case WebPartType.VideoEmbed:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.VideoEmbed);
                                                break;
                                            case WebPartType.YammerEmbed:
                                                webPartName = Pages.ClientSidePage.ClientSideWebPartEnumToName(Pages.DefaultClientSideWebParts.YammerEmbed);
                                                break;
                                            default:
                                                break;
                                        }

                                        baseControl = componentsToAdd.Where(p => p.Name.Equals(webPartName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                                    }

                                    if (baseControl != null)
                                    {
                                        Pages.ClientSideWebPart myWebPart = new Pages.ClientSideWebPart(baseControl)
                                        {
                                            Order = control.Order
                                        };

                                        page.AddControl(myWebPart, page.Sections[sectionCount].Columns[control.Column], control.Order);

                                        // set properties using json string
                                        if (!String.IsNullOrEmpty(control.JsonControlData))
                                        {
                                            myWebPart.PropertiesJson = control.JsonControlData;
                                        }

                                        // set using property collection
                                        if (control.ControlProperties.Any())
                                        {
                                            // grab the "default" properties so we can deduct their types, needed to correctly apply the set properties
                                            var controlManifest = JObject.Parse(baseControl.Manifest);
                                            JToken controlProperties = null;
                                            if (controlManifest != null)
                                            {
                                                controlProperties = controlManifest.SelectToken("preconfiguredEntries[0].properties");
                                            }

                                            foreach (var property in control.ControlProperties)
                                            {
                                                Type propertyType = typeof(string);

                                                if (controlProperties != null)
                                                {
                                                    var defaultProperty = controlProperties.SelectToken(property.Key, false);
                                                    if (defaultProperty != null)
                                                    {
                                                        propertyType = Type.GetType($"System.{defaultProperty.Type.ToString()}");

                                                        if (propertyType == null)
                                                        {
                                                            if (defaultProperty.Type.ToString().Equals("integer", StringComparison.InvariantCultureIgnoreCase))
                                                            {
                                                                propertyType = typeof(int);
                                                            }
                                                        }
                                                    }
                                                }

                                                myWebPart.Properties[property.Key] = JToken.FromObject(Convert.ChangeType(parser.ParseString(property.Value), propertyType));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        scope.LogWarning(CoreResources.Provisioning_ObjectHandlers_ClientSidePages_BaseControlNotFound, control.ControlId, control.CustomWebPartName);
                                    }
                                }
                            }
                        }                        
                    }

                    // Persist the page
                    page.Save(pageName);

                    // Make it a news page if requested
                    if (clientSidePage.PromoteAsNewsArticle)
                    {
                        page.PromoteAsNewsArticle();
                    }

                }
            }
            return parser;
        }

        public override ProvisioningTemplate ExtractObjects(Web web, ProvisioningTemplate template, ProvisioningTemplateCreationInformation creationInfo)
        {
            using (var scope = new PnPMonitoredScope(this.Name))
            {
                // Impossible to return all files in the site currently

                // If a base template is specified then use that one to "cleanup" the generated template model
                if (creationInfo.BaseTemplate != null)
                {
                    template = CleanupEntities(template, creationInfo.BaseTemplate);
                }
            }
            return template;
        }

        private ProvisioningTemplate CleanupEntities(ProvisioningTemplate template, ProvisioningTemplate baseTemplate)
        {
            return template;
        }

        public override bool WillProvision(Web web, ProvisioningTemplate template)
        {
            if (!_willProvision.HasValue)
            {
                _willProvision = template.ClientSidePages.Any();
            }
            return _willProvision.Value;
        }

        public override bool WillExtract(Web web, ProvisioningTemplate template, ProvisioningTemplateCreationInformation creationInfo)
        {
            if (!_willExtract.HasValue)
            {
                _willExtract = false;
            }
            return _willExtract.Value;
        }
    }
#endif
}
