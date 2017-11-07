﻿using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using AutoMapper;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Mapping;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Trees;

namespace Umbraco.Web.Models.Mapping
{
    /// <summary>
    /// Declares model mappings for media.
    /// </summary>
    internal class MediaModelMapper : MapperConfiguration
    {
        public override void ConfigureMappings(IConfiguration config, ApplicationContext applicationContext)
        {
            //FROM IMedia TO MediaItemDisplay
            config.CreateMap<IMedia, MediaItemDisplay>()
                .ForMember(display => display.Udi, expression => expression.MapFrom(content => Udi.Create(Constants.UdiEntityType.Media, content.Key)))
                .ForMember(display => display.Owner, expression => expression.ResolveUsing(new OwnerResolver<IMedia>()))
                .ForMember(display => display.Icon, expression => expression.MapFrom(content => content.ContentType.Icon))
                .ForMember(display => display.ContentTypeAlias, expression => expression.MapFrom(content => content.ContentType.Alias))
                .ForMember(display => display.IsChildOfListView, expression => expression.Ignore())
                .ForMember(display => display.Trashed, expression => expression.MapFrom(content => content.Trashed))
                .ForMember(display => display.ContentTypeName, expression => expression.MapFrom(content => content.ContentType.Name))
                .ForMember(display => display.Properties, expression => expression.Ignore())
                .ForMember(display => display.TreeNodeUrl, expression => expression.Ignore())
                .ForMember(display => display.Notifications, expression => expression.Ignore())
                .ForMember(display => display.Errors, expression => expression.Ignore())
                .ForMember(display => display.Published, expression => expression.Ignore())
                .ForMember(display => display.Updater, expression => expression.Ignore())
                .ForMember(display => display.Alias, expression => expression.Ignore())
                .ForMember(display => display.IsContainer, expression => expression.Ignore())
                .ForMember(display => display.HasPublishedVersion, expression => expression.Ignore())
                .ForMember(display => display.Tabs, expression => expression.ResolveUsing(new TabsAndPropertiesResolver(applicationContext.Services.TextService)))
                .ForMember(display => display.ContentType, expression => expression.ResolveUsing<MediaTypeBasicResolver>())
                .ForMember(display => display.MediaLink, expression => expression.ResolveUsing(
                    content => string.Join(",", content.GetUrls(UmbracoConfig.For.UmbracoSettings().Content, applicationContext.ProfilingLogger.Logger))))
                .AfterMap((media, display) => AfterMap(media, display, applicationContext.Services.DataTypeService, applicationContext.Services.TextService, applicationContext.Services.ContentTypeService, applicationContext.ProfilingLogger.Logger));

            //FROM IMedia TO ContentItemBasic<ContentPropertyBasic, IMedia>
            config.CreateMap<IMedia, ContentItemBasic<ContentPropertyBasic, IMedia>>()
                .ForMember(display => display.Udi, expression => expression.MapFrom(content => Udi.Create(Constants.UdiEntityType.Media, content.Key)))
                .ForMember(dto => dto.Owner, expression => expression.ResolveUsing(new OwnerResolver<IMedia>()))
                .ForMember(dto => dto.Icon, expression => expression.MapFrom(content => content.ContentType.Icon))
                .ForMember(dto => dto.Trashed, expression => expression.MapFrom(content => content.Trashed))
                .ForMember(dto => dto.ContentTypeAlias, expression => expression.MapFrom(content => content.ContentType.Alias))
                .ForMember(dto => dto.Published, expression => expression.Ignore())
                .ForMember(dto => dto.Updater, expression => expression.Ignore())
                .ForMember(dto => dto.Alias, expression => expression.Ignore())
                .ForMember(dto => dto.HasPublishedVersion, expression => expression.Ignore());

            //FROM IMedia TO ContentItemDto<IMedia>
            config.CreateMap<IMedia, ContentItemDto<IMedia>>()
                .ForMember(display => display.Udi, expression => expression.MapFrom(content => Udi.Create(Constants.UdiEntityType.Media, content.Key)))
                .ForMember(dto => dto.Owner, expression => expression.ResolveUsing(new OwnerResolver<IMedia>()))
                .ForMember(dto => dto.Published, expression => expression.Ignore())
                .ForMember(dto => dto.Updater, expression => expression.Ignore())
                .ForMember(dto => dto.Icon, expression => expression.Ignore())
                .ForMember(dto => dto.Alias, expression => expression.Ignore())
                .ForMember(dto => dto.HasPublishedVersion, expression => expression.Ignore());            
        }

        private static void AfterMap(IMedia media, MediaItemDisplay display, IDataTypeService dataTypeService, ILocalizedTextService localizedText, IContentTypeService contentTypeService, ILogger logger)
        {
            // Adapted from ContentModelMapper
            //map the IsChildOfListView (this is actually if it is a descendant of a list view!)
            var parent = media.Parent();
            display.IsChildOfListView = parent != null && (parent.ContentType.IsContainer || contentTypeService.HasContainerInPath(parent.Path));

            //map the tree node url
            if (HttpContext.Current != null)
            {
                var urlHelper = new UrlHelper(HttpContext.Current.Request.RequestContext);
                var url = urlHelper.GetUmbracoApiService<MediaTreeController>(controller => controller.GetTreeNode(display.Id.ToString(), null));
                display.TreeNodeUrl = url;
            }

            if (media.ContentType.IsContainer)
            {
                TabsAndPropertiesResolver.AddListView(display, "media", dataTypeService, localizedText);
            }

            TabsAndPropertiesResolver.MapGenericProperties(media, display, localizedText);
        }

        /// <summary>
        /// Resolves a <see cref="ContentTypeBasic"/> from the <see cref="IContent"/> item and checks if the current user
        /// has access to see this data
        /// </summary>
        private class MediaTypeBasicResolver : ValueResolver<IMedia, ContentTypeBasic>
        {
            protected override ContentTypeBasic ResolveCore(IMedia source)
            {
                //TODO: This would be much nicer with the IUmbracoContextAccessor so we don't use singletons
                //If this is a web request and there's a user signed in and the 
                // user has access to the settings section, we will 
                if (HttpContext.Current != null && UmbracoContext.Current != null &&
                    UmbracoContext.Current.Security.CurrentUser != null
                    && UmbracoContext.Current.Security.CurrentUser.AllowedSections.Any(x => x.Equals(Constants
                        .Applications.Settings)))
                {
                    var contentTypeBasic = Mapper.Map<ContentTypeBasic>(source.ContentType);
                    return contentTypeBasic;
                }
                //no access
                return null;
            }

        }
    }
}
