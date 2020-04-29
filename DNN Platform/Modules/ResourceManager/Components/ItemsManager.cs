﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNetNuke.Entities;
using DotNetNuke.Entities.Content;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.Assets;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.FileSystem.EventArgs;
using DotNetNuke.Services.Localization;
using Dnn.Modules.ResourceManager.Components.Common;
using Dnn.Modules.ResourceManager.Exceptions;
using Dnn.Modules.ResourceManager.Helpers;
using Dnn.Modules.ResourceManager.Services.Dto;

namespace Dnn.Modules.ResourceManager.Components
{
    public class ItemsManager : ServiceLocator<IItemsManager, ItemsManager>, IItemsManager
    {
        #region Members

        private const int MaxDescriptionLength = 500;
        private readonly IContentController _contentController;
        private readonly IRoleController _roleController;
        private readonly IFileManager _fileManager;
        private readonly IAssetManager _assetManager;
        private readonly IPermissionsManager _permissionsManager;

        #endregion

        #region Constructors

        public ItemsManager()
        {
            _contentController = ContentController.Instance;
            _roleController = RoleController.Instance;
            _fileManager = FileManager.Instance;
            _assetManager = AssetManager.Instance;
            _permissionsManager = PermissionsManager.Instance;
        }

        #endregion

        public ContentPage GetFolderContent(int folderId, int startIndex, int numItems, string sorting, int moduleMode)
        {
            var noPermissionMessage = Localization.GetExceptionMessage("UserHasNoPermissionToBrowseFolder",
                Constants.UserHasNoPermissionToBrowseFolderDefaultMessage);

            if (!_permissionsManager.HasFolderContentPermission(folderId, moduleMode))
            {
                throw new FolderPermissionNotMetException(noPermissionMessage);
            }

            try
            {
                return AssetManager.Instance.GetFolderContent(folderId, startIndex, numItems, sorting + " ASC");
            }
            catch (AssetManagerException)
            {
                throw new FolderPermissionNotMetException(noPermissionMessage);
            }
        }

        public Stream GetFileContent(int fileId, out string fileName, out string contentType)
        {
            var file = _fileManager.GetFile(fileId, true);

            if (!_permissionsManager.HasGetFileContentPermission(file.FolderId))
            {
                throw new FolderPermissionNotMetException(LocalizationHelper.GetString(Constants.UserHasNoPermissionToDownloadKey));
            }

            var content = _fileManager.GetFileContent(file);
            fileName = file.FileName;
            contentType = file.ContentType;

            EventManager.Instance.OnFileDownloaded(new FileDownloadedEventArgs
            {
                FileInfo = file,
                UserId = UserController.Instance.GetCurrentUserInfo().UserID
            });
            return content;
        }

        public IFolderInfo CreateNewFolder(string folderName, int parentFolderId, int folderMappingId, string mappedName,
            int moduleMode)
        {
            if (!_permissionsManager.HasAddFoldersPermission(moduleMode, parentFolderId))
            {
                throw new FolderPermissionNotMetException(LocalizationHelper.GetString(Constants.UserHasNoPermissionToAddFoldersKey));
            }

            return AssetManager.Instance.CreateFolder(folderName, parentFolderId, folderMappingId, mappedName);
        }

        public void SaveFileDetails(IFileInfo file, FileDetailsRequest fileDetails)
        {
            var propertyChanged = false;

            _assetManager.RenameFile(file.FileId, fileDetails.FileName);
            if (file.Title != fileDetails.Title)
            {
                file.Title = fileDetails.Title;
                propertyChanged = true;
            }

            if (file.Description != fileDetails.Description)
            {
                file.Description = fileDetails.Description;
                if (!string.IsNullOrEmpty(file.Description) && file.Description.Length > MaxDescriptionLength)
                {
                    file.Description = file.Description.Substring(0, MaxDescriptionLength);
                }
                propertyChanged = true;
            }

            if (propertyChanged)
            {
                FileManager.Instance.UpdateFile(file);
            }
        }

        public void SaveFolderDetails(IFolderInfo folder, FolderDetailsRequest folderDetails)
        {
            _assetManager.RenameFolder(folderDetails.FolderId, folderDetails.FolderName);

            var currentUserInfo = UserController.Instance.GetCurrentUserInfo();
        }

        public void DeleteFile(int fileId, int moduleMode, int groupId)
        {
            var file = FileManager.Instance.GetFile(fileId);
            if (file == null)
            {
                throw new NotFoundException("File doesn't exist.");
            }

            if (moduleMode == (int) Constants.ModuleModes.Group && IsGroupIcon(file))
            {
                throw new FolderPermissionNotMetException(LocalizationHelper.GetString(Constants.GroupIconCantBeDeletedKey));
            }

            if (!_permissionsManager.HasDeletePermission(moduleMode, file.FolderId))
            {
                throw new FolderPermissionNotMetException(LocalizationHelper.GetString("UserHasNoPermissionToDeleteFile.Error"));
            }

            AssetManager.Instance.DeleteFile(fileId);
        }

        public void DeleteFolder(int folderId, bool unlinkAllowedStatus, int moduleMode)
        {
            var folder = FolderManager.Instance.GetFolder(folderId);
            if (folder == null)
            {
                throw new NotFoundException("Folder doesn't exist.");
            }

            if (!_permissionsManager.HasDeletePermission(moduleMode, folderId))
            {
                throw new FolderPermissionNotMetException(LocalizationHelper.GetString("UserHasNoPermissionToDeleteFolder.Error"));
            }

            var nonDeletedSubfolders = new List<IFolderInfo>();
            AssetManager.Instance.DeleteFolder(folderId, unlinkAllowedStatus, nonDeletedSubfolders);
        }

        #region Private methods

        private bool IsGroupIcon(IFileInfo file)
        {
            var groupId = Utils.GetFolderGroupId(file.FolderId);
            if (groupId < 0)
            {
                return false;
            }

            var portalId = PortalSettings.Current.PortalId;
            var role = _roleController.GetRoleById(portalId, groupId);
            return role?.IconFile?.Substring(7) == file.FileId.ToString();
        }

        private ContentItem CreateFileContentItem(string contentType)
        {
            var typeController = new ContentTypeController();
            var contentTypeFile = (from t in typeController.GetContentTypes() where t.ContentType == contentType select t).SingleOrDefault();

            if (contentTypeFile == null)
            {
                contentTypeFile = new ContentType { ContentType = contentType };
                contentTypeFile.ContentTypeId = typeController.AddContentType(contentTypeFile);
            }

            var objContent = new ContentItem
            {
                ContentTypeId = contentTypeFile.ContentTypeId,
                Indexed = false,
            };

            objContent.ContentItemId = _contentController.AddContentItem(objContent);
            return objContent;
        }

        #endregion

        #region Service Locator

        protected override Func<IItemsManager> GetFactory()
        {
            return () => new ItemsManager();
        }

        #endregion
    }
}