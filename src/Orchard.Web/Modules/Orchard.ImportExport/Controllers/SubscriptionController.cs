﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Orchard.ContentManagement;
using Orchard.DisplayManagement;
using Orchard.Environment.Extensions;
using Orchard.ImportExport.Models;
using Orchard.ImportExport.Permissions;
using Orchard.ImportExport.Services;
using Orchard.ImportExport.ViewModels;
using Orchard.Localization;
using Orchard.Recipes.Services;
using Orchard.UI.Admin;
using Orchard.UI.Navigation;
using Orchard.UI.Notify;

namespace Orchard.ImportExport.Controllers {
    [Admin]
    [OrchardFeature("Orchard.Deployment")]
    public class SubscriptionController : Controller, IUpdateModel {
        private readonly IOrchardServices _orchardServices;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IRecurringScheduledTaskManager _recurringScheduledTaskManager;
        private readonly IDeploymentService _deploymentService;
        private readonly IRecipeResultAccessor _recipeJournal;

        public SubscriptionController(
            IOrchardServices services,
            ISubscriptionService subscriptionService,
            IRecurringScheduledTaskManager recurringScheduledTaskManager,
            IDeploymentService deploymentService,
            IRecipeResultAccessor recipeJournal,
            IShapeFactory shapeFactory
            ) {
            _orchardServices = services;
            _subscriptionService = subscriptionService;
            _recurringScheduledTaskManager = recurringScheduledTaskManager;
            _deploymentService = deploymentService;
            _recipeJournal = recipeJournal;
            Services = services;
            T = NullLocalizer.Instance;
            Shape = shapeFactory;
        }

        public IOrchardServices Services { get; private set; }
        public Localizer T { get; set; }
        private dynamic Shape { get; set; }

        public ActionResult Index(PagerParameters pagerParameters) {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            var pager = new Pager(Services.WorkContext.CurrentSite, pagerParameters);
            var subscriptions = Services.ContentManager
                .Query<DeploymentSubscriptionPart, DeploymentSubscriptionPartRecord>(VersionOptions.Latest)
                .List().ToList();

            var pagerShape = Shape.Pager(pager).TotalItemCount(subscriptions.Count());

            return View(new SubscriptionsViewModel {
                Subscriptions = subscriptions
                    .Skip(pager.GetStartIndex())
                    .Take(pager.PageSize)
                    .Select(PopulateSubscriptionSummary)
                    .ToList(),
                Pager = pagerShape
            });
        }

        public ActionResult Create() {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            var model = new CreateSubscriptionViewModel {
                Sources = _deploymentService.GetDeploymentSourceConfigurations(),
                Targets = _deploymentService.GetDeploymentTargetConfigurations(),
                SubscriptionTypes = new List<string> {DeploymentType.Export.ToString(), DeploymentType.Import.ToString()}
            };

            return View(model);
        }

        [HttpPost, ActionName("Create")]
        public ActionResult CreatePost() {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            var model = new CreateSubscriptionViewModel();

            if (!TryUpdateModel(model) || !ModelState.IsValid) {
                model.Sources = _deploymentService.GetDeploymentSourceConfigurations();
                model.Targets = _deploymentService.GetDeploymentTargetConfigurations();
                model.SubscriptionTypes = new List<string> {DeploymentType.Import.ToString(), DeploymentType.Export.ToString()};
                return View(model);
            }
            var subscription = _orchardServices.ContentManager.New("DeploymentSubscription").As<DeploymentSubscriptionPart>();
            _orchardServices.ContentManager.Create(subscription);
            subscription.Title = model.Title;
            DeploymentType deploymentType;
            subscription.DeploymentType = Enum.TryParse(model.SelectedDeploymentType, out deploymentType) ? deploymentType : deploymentType;
            subscription.DeploymentConfiguration = subscription.DeploymentType == DeploymentType.Import ?
                Services.ContentManager.Get(model.SelectedDeploymentSourceId) : Services.ContentManager.Get(model.SelectedDeploymentTargetId);

            _orchardServices.ContentManager.Create(subscription);

            Services.Notifier.Information(T("Subscription {0} created successfully", subscription.Title));

            return RedirectToAction("Edit", new {id = subscription.Id});
        }

        public ActionResult Edit(int id) {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            var subscription = _orchardServices.ContentManager.Get<DeploymentSubscriptionPart>(id);
            if (subscription == null)
                return HttpNotFound();

            dynamic model = Services.ContentManager.BuildEditor(subscription);
            return View((object) model);
        }

        [HttpPost, ActionName("Edit")]
        public ActionResult EditPost(int id) {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            var subscription = _orchardServices.ContentManager.Get<DeploymentSubscriptionPart>(id);
            dynamic model = Services.ContentManager.UpdateEditor(subscription, this);

            if (!ModelState.IsValid) {
                Services.TransactionManager.Cancel();
                return View((object) model);
            }

            Services.Notifier.Information(T("Subscription {0} updated successfully", subscription.Title));

            return RedirectToAction("Index");
        }

        public ActionResult RunSubscriptionTask(int id) {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            _subscriptionService.RunSubscriptionTask(id);

            Services.Notifier.Information(T("Subscription has been run"));

            return RedirectToAction("Index");
        }

        public ActionResult DownloadSubscription(int id, string executionId = null) {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            var deploymentFile = _subscriptionService.GetDeploymentFile(id, executionId ?? Guid.NewGuid().ToString("n"));

            if (string.IsNullOrEmpty(deploymentFile))
                return HttpNotFound();

            if (Path.GetExtension(deploymentFile) == ".xml") {
                return File(deploymentFile, "text/xml", "subscription.xml");
            }
            return File(deploymentFile, "application/zip", "subscription.nupkg");
        }

        public ActionResult GetRecipeJournal(string executionId) {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ViewDeploymentHistory, T("Not allowed to view deployment history.")))
                return new HttpUnauthorizedResult();

            var journal = _recipeJournal.GetResult(executionId);
            var result = Json(journal);
            result.JsonRequestBehavior = JsonRequestBehavior.AllowGet;
            return result;
        }

        bool IUpdateModel.TryUpdateModel<TModel>(TModel model, string prefix, string[] includeProperties, string[] excludeProperties) {
            return TryUpdateModel(model, prefix, includeProperties, excludeProperties);
        }

        public void AddModelError(string key, LocalizedString errorMessage) {
            ModelState.AddModelError(key, errorMessage.ToString());
        }

        private SubscriptionSummaryViewModel PopulateSubscriptionSummary(DeploymentSubscriptionPart part) {
            var lastTaskRun = _recurringScheduledTaskManager.GetLastTaskRun(part.Id, null);
            var nextTaskRun = _recurringScheduledTaskManager.GetNextScheduledTask(part.Id);

            var summary = new SubscriptionSummaryViewModel {
                Id = part.Id,
                Name = part.Title,
                DeploymentType = part.DeploymentType,
                ContentItem = part.ContentItem
            };

            if (lastTaskRun != null) {

                summary.LastRunStatus = lastTaskRun.RunStatus.ToString();

                //Due to roll back of transactions on error, update status if failure logged in journal
                if (lastTaskRun.RunStatus == RunStatus.Running) {
                    var recipeStatus = _recipeJournal.GetResult(lastTaskRun.ExecutionId);

                    if (!recipeStatus.IsSuccessful) {
                        _recurringScheduledTaskManager.SetTaskCompleted(lastTaskRun.ExecutionId, RunStatus.Fail);
                    }
                }

                switch (lastTaskRun.RunStatus) {
                    case RunStatus.Started:
                    case RunStatus.Running:
                        summary.LastRunDateTime = lastTaskRun.RunStartUtc;
                        break;
                    case RunStatus.Success:
                    case RunStatus.Fail:
                    case RunStatus.Cancelled:
                        summary.LastRunDateTime = lastTaskRun.RunCompletedUtc;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (nextTaskRun != null && nextTaskRun.ScheduledUtc.HasValue) {
                summary.NextRun = nextTaskRun.ScheduledUtc;
            }

            return summary;
        }
    }
}
