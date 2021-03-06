﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using Telerik.Web.UI;

    public class DnnGridDataItem : GridDataItem
    {
        public DnnGridDataItem(GridTableView ownerTableView, int itemIndex, int dataSetIndex)
            : base(ownerTableView, itemIndex, dataSetIndex)
        {
        }

        public DnnGridDataItem(GridTableView ownerTableView, int itemIndex, int dataSetIndex, GridItemType itemType)
            : base(ownerTableView, itemIndex, dataSetIndex, itemType)
        {
        }
    }
}
