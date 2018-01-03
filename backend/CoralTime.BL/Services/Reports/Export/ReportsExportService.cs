﻿using AutoMapper;
using CoralTime.BL.ServicesInterfaces.Reports.DDAndGrid;
using CoralTime.BL.ServicesInterfaces.Reports.Export;
using CoralTime.Common.Constants;
using CoralTime.Common.Models;
using CoralTime.DAL.Repositories;
using CoralTime.ViewModels.Reports;
using CoralTime.ViewModels.Reports.Request.ReportsGrid;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using CoralTime.Common.Helpers;
using CoralTime.ViewModels.Reports.PDF;
using CoralTime.ViewModels.Reports.Responce.ReportsGrid;

namespace CoralTime.BL.Services.Reports.Export
{
    public partial class ReportExportService : _BaseService, IReportExportService
    {
        private IHostingEnvironment _environment { get; }
        private readonly IConfiguration _configuration;
        private readonly IReportService _reportService;

        public ReportExportService(UnitOfWork uow, IMapper mapper, IConfiguration configuration, IHostingEnvironment environment, IReportService reportService)
            : base(uow, mapper)
        {
            _configuration = configuration;
            _environment = environment;
            _reportService = reportService;
        }

        #region Constants.

        private const string ExtensionXLSX = ".xlsx";
        private const string ExtensionCSV = ".csv";
        private const string ExtensionPDF = ".pdf";

        private const string ContentTypeXLSX = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        //private const string ContentTypeXLSX = "application/octet-stream";
        private const string ContentTypeCSV = "application/csv";
        private const string ContentTypePDF = "application/pdf";

        private enum FileType
        {
            Excel = 0,
            CSV = 1,
            PDF = 2
        }

        #endregion

        #region Properties. 

        private string fileName = "CoralTime Reports";

        private DateTime DateFrom { get; set; }

        private DateTime DateTo { get; set; }

        private int GroupById { get; set; }

        private int[] ShowColumnIds { get; set; }

        private int DateFormatId { get; set; }

        private int CountPeriodCells { get; set; }

        private int CountGroupByCells { get; } = 1;

        #endregion

        #region Exclude excess properties (Exlude enums and array of Properties) 

        public enum ExternalProperties
        {
            ProjectName,
            ClientName,
            MemberName,
            Date,
            TotalActualTime,
            TotalEstimatedTime,
            GrandActualTime,
            GrandEstimatedTime,
        }

        public enum InternalProperties
        {
            TimeEntryName,
            ProjectName,
            ClientName,
            MemberName,
            TaskName,
            Date,
            TimeFrom,
            TimeTo,
            ActualTime,
            EstimatedTime,
            Description,
            TotalActualTime,
            TotalEstimatedTime,
        }

        private readonly List<string> _alwaysShowProperty = new List<string>
        {
            InternalProperties.ProjectName.ToString(),
            InternalProperties.MemberName.ToString(),
            InternalProperties.TaskName.ToString()
        };

        private enum ExcludePropertyByDefault
        {
            TimeEntryId,
            ProjectId,
            ClientId,
            MemberId,
            TaskId,
        }
        
        private string[] _excludeProperties =
        {
            ExcludePropertyByDefault.TimeEntryId.ToString(),
            ExcludePropertyByDefault.ProjectId.ToString(),
            ExcludePropertyByDefault.ClientId.ToString(),
            ExcludePropertyByDefault.MemberId.ToString(),
            ExcludePropertyByDefault.TaskId.ToString(),
        };

        private readonly ShowColumnModel[] showColumnsInfo =
        {
            new ShowColumnModel{ Id = 0, ShowColumnDescriptions = new List<ShowColumnDescription>
            {
                new ShowColumnDescription { Name = InternalProperties.ActualTime.ToString(), Description = "Show Actual Hours" },
                new ShowColumnDescription { Name = InternalProperties.TotalActualTime.ToString(), Description = "Show Total Actual Hours" },
                new ShowColumnDescription { Name = ExternalProperties.GrandActualTime.ToString(), Description = "Show Grand Actual Hours" },
            }},
            new ShowColumnModel{ Id = 1, ShowColumnDescriptions = new List<ShowColumnDescription>
            {
                new ShowColumnDescription { Name = InternalProperties.EstimatedTime.ToString(), Description = "Show Estimated Hours" },
                new ShowColumnDescription { Name = InternalProperties.TotalEstimatedTime.ToString(), Description = "Show Total Estimated Hours" },
                new ShowColumnDescription { Name = ExternalProperties.GrandEstimatedTime.ToString(), Description = "Show Grand Estimated Hours" },
            }},
            new ShowColumnModel{ Id = 2, ShowColumnDescriptions = new List<ShowColumnDescription>
            {
                new ShowColumnDescription { Name = InternalProperties.Date.ToString(), Description = "Show Date"}
            }},
            new ShowColumnModel{ Id = 3, ShowColumnDescriptions = new List<ShowColumnDescription>
            {
                new ShowColumnDescription { Name = InternalProperties.Description.ToString(), Description = "Show Notes"}
            }},
            new ShowColumnModel{ Id = 4, ShowColumnDescriptions = new List<ShowColumnDescription>
            {
                new ShowColumnDescription { Name = InternalProperties.TimeFrom.ToString(), Description = "Show Start Time"},
                new ShowColumnDescription { Name = InternalProperties.TimeTo.ToString(), Description = "Show Finish Time"}
            }}
        };

        #endregion

        #region Export Excel, CSV, PDF. 

        public FileResult ExportGroupByNone(string userName, RequestReportsGrid reportsGridData, HttpContext httpContext)
        {
            var groupByNone = _reportService.GroupByNone(userName, reportsGridData);
            var result = GetExportFileWithGrouping(reportsGridData, httpContext, groupByNone);

            return result;
        }

        public FileResult ExportGroupByProjects(string userName, RequestReportsGrid reportsGridData, HttpContext httpContext)
        {
            var groupByProjects = _reportService.GroupByProjects(userName, reportsGridData);
            var result = GetExportFileWithGrouping(reportsGridData, httpContext, groupByProjects);

            return result;
        }

        public FileResult ExportGroupByUsers(string userName, RequestReportsGrid reportsGridData, HttpContext httpContext)
        {
            var groupByUsers = _reportService.GroupByUsers(userName, reportsGridData);
            var result = GetExportFileWithGrouping(reportsGridData, httpContext, groupByUsers);

            return result;
        }

        public FileResult ExportGroupByDates(string userName, RequestReportsGrid reportsGridData, HttpContext httpContext)
        {
            var groupByDates = _reportService.GroupByDates(userName, reportsGridData);
            var result = GetExportFileWithGrouping(reportsGridData, httpContext, groupByDates);

            return result;
        }

        public FileResult ExportGroupByClients(string userName, RequestReportsGrid reportsGridData, HttpContext httpContext)
        {
            var groupByClient = _reportService.GroupByClients(userName, reportsGridData);
            var result = GetExportFileWithGrouping(reportsGridData, httpContext, groupByClient);

            return result;
        }

        #endregion

        #region Export Excel, CSV, PDF. (Common methods)

        private FileStreamResult GetExportFileWithGrouping<T>(RequestReportsGrid reportsGridData, HttpContext httpContext, IReportsGrandGridView<T> groupingList)
        {
            var fileByte = CreateReportFileByteUpdateFileNameContentType(reportsGridData, groupingList, out var contentType);

            var fileStreamResult = SaveFileToFileStreamResult(httpContext, contentType, fileByte);

            return fileStreamResult;
        }

        private FileStreamResult SaveFileToFileStreamResult(HttpContext httpContext, string contentType, byte[] fileByte)
        {
            httpContext.Response.ContentType = contentType;

            var fileStreamResult = new FileStreamResult(new MemoryStream(fileByte), new MediaTypeHeaderValue(contentType))
            {
                FileDownloadName = fileName
            };

            return fileStreamResult;
        }

        private byte[] CreateReportFileByteUpdateFileNameContentType<T>(RequestReportsGrid reportsGridData, IReportsGrandGridView<T> groupingList, out string contentType)
        {
            SetCommonValuesForExport<T>(reportsGridData);

            var file = new byte[0];
            contentType = string.Empty;

            fileName = GetFileName(fileName);

            switch (reportsGridData.FileTypeId ?? 0)
            {
                case (int) FileType.Excel:
                {
                    fileName = fileName + ExtensionXLSX;
                    file = CreateFileExcel(groupingList);
                    contentType = ContentTypeXLSX;

                    break;
                }

                case (int) FileType.CSV:
                {
                    fileName = fileName + ExtensionCSV;
                    file = CreateFileCSV(groupingList);
                    contentType = ContentTypeCSV;

                    break;
                }

                case (int) FileType.PDF:
                {
                    fileName = fileName + ExtensionPDF;
                    file = CreateFilePDF(groupingList);
                    contentType = ContentTypePDF;

                    break;
                }
            }

            return file;
        }


        private string GetFileName(string fileName)
        {
            return fileName + " " + GetAbbreviatedMonthName(DateFrom) + " - " + GetAbbreviatedMonthName(DateTo);
        }

        private string GetAbbreviatedMonthName(DateTime date)
        {
            return CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(date.Month) + " " + date.Day;
        }

        private bool RunSetCommonValuesForExport { get; set; }

        private void SetCommonValuesForExport<T>(RequestReportsGrid reportsGridData)
        {
            RunSetCommonValuesForExport = true;

            #region Set Global Properties. 

            GroupById = reportsGridData.GroupById ?? 0;
            ShowColumnIds = reportsGridData.ShowColumnIds;
            DateFormatId = reportsGridData.DateFormatId;

            DateFrom = _reportService.DateFrom;
            DateTo = _reportService.DateTo;

            #endregion

            #region Get excluded props by Grand, Entity, NestedEntity headers in arrays.

            PropsGrandHeaders = ExcludeProps(typeof(IReportsGrandGridView<T>));
            PropsEntityHeaders = ExcludePropsEntity(typeof(T));
            PropsEntityRows = ExcludeProps(typeof(IReportsGridItemsView));

            #endregion
        }

        #region Exclude excess properties

        /*
            1. Exlude one field ex:"ClientId" in external headers - ExcludeExternalPropInDependenceByGrouping() add field in Array "string[] _excludeProperties" from "enum InternalProperties".
            2. Exclude plural fields (Ids) in grid - ExcludeProps(): add field in Array "string[] _excludeProperties" from "enum ExcludePropertyByDefault"
            3. Exclude by custom options - ExcludeProps():           add field in Array "string[] _excludeProperties" from "enum ExternalProperties" AND "enum InternalProperties"
            4. Rename some properties.
        */

        private bool IsPropByDefaultGrouping(string propName)
        {
            var result = propName == InternalProperties.ProjectName.ToString() && GroupById == (int) Constants.ReportsGroupBy.Project
                         || propName == InternalProperties.MemberName.ToString() && GroupById == (int) Constants.ReportsGroupBy.User
                         || propName == InternalProperties.Date.ToString() && GroupById == (int) Constants.ReportsGroupBy.Date
                         || propName == InternalProperties.ClientName.ToString() && GroupById == (int) Constants.ReportsGroupBy.Client
                         || propName == InternalProperties.TimeEntryName.ToString() && GroupById == (int) Constants.ReportsGroupBy.None;

            return result;
        }

        private List<PropertyInfo> ExcludeProps(Type type)
        {
            var hideColumns = AddHideColumns();

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
   
            var availableProps = new List<PropertyInfo>();
            foreach (var prop in props)
            {
                if (hideColumns.All(x => x != prop.Name))
                {
                    availableProps.Add(prop);
                }
            }

            return availableProps;
        }

        private List<PropertyInfo> ExcludePropsEntity(Type type)
        {
            var hideColumns = AddHideColumns();

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var availableProps = new List<PropertyInfo>();

            foreach (var prop in props)
            {
                if (hideColumns.All(x => x != prop.Name))
                {
                    availableProps.Add(prop);
                }
            }

            return availableProps;
        }

        private string[] AddHideColumns()
        {
            if (ShowColumnIds != null)
            {
                foreach (var showColumnInfo in showColumnsInfo)
                {
                    var isAddPropToExcludeArray = !ShowColumnIds.Contains(showColumnInfo.Id);
                    if (isAddPropToExcludeArray)
                    {
                        var showColumnDescriptionsById = showColumnsInfo.FirstOrDefault(x => x.Id == showColumnInfo.Id)?.ShowColumnDescriptions;
                        
                        foreach (var tmpItem in showColumnDescriptionsById)
                        {
                            if (GroupById == (int)Constants.ReportsGroupBy.Date && showColumnDescriptionsById.Any(x => x.Name.Contains(InternalProperties.Date.ToString())))
                            {
                                continue;
                            }

                            _excludeProperties = _excludeProperties.Append(tmpItem.Name).ToArray();
                        }
                    }
                }
            }

            return _excludeProperties;
        }

        private List<string> RenameNestedEntityHeaders(List<PropertyInfo> props)
        {
            var result = new List<string>();

            foreach (var prop in props)
            {
                if (IsPropByDefaultGrouping(prop.Name))
                {
                    continue;
                }

                if (prop.Name == InternalProperties.ClientName.ToString())
                {
                    result.Add("Client");
                    continue;
                }

                if (prop.Name == InternalProperties.MemberName.ToString())
                {
                    result.Add("User");
                    continue;
                }

                if (prop.Name == InternalProperties.ProjectName.ToString())
                {
                    result.Add("Project");
                    continue;
                }

                if (prop.Name == InternalProperties.TaskName.ToString())
                {
                    result.Add("Task");
                    continue;
                }

                if (prop.Name == InternalProperties.Description.ToString())
                {
                    result.Add("Notes");
                    continue;
                }

                if (prop.Name == InternalProperties.ActualTime.ToString())
                {
                    result.Add("Actual Hours");
                    continue;
                }

                if (prop.Name == InternalProperties.EstimatedTime.ToString())
                {
                    result.Add("Estimated Hours");
                    continue;
                }

                if (prop.Name == InternalProperties.TimeFrom.ToString())
                {
                    result.Add("Start");
                    continue;
                }

                if (prop.Name == InternalProperties.TimeTo.ToString())
                {
                    result.Add("Finish");
                    continue;
                }

                result.Add(prop.Name);
            }

            return result;
        }

        public enum NameDisplays
        {
            Notes
        }

        private string GetNameDisplayForGrandAndTotalHeaders(string propName)
        {
            if (propName == ExternalProperties.GrandActualTime.ToString())
            {
                return "Grand Actual Time: ";
            }

            if (propName == ExternalProperties.GrandEstimatedTime.ToString())
            {
                return "Grand Estimated Time: ";
            }

            if (propName == InternalProperties.TotalActualTime.ToString())
            {
                return "Total Actual Time: ";
            }

            if (propName == InternalProperties.TotalEstimatedTime.ToString())
            {
                return "Total Estimated Time: ";
            }

            if (propName == InternalProperties.TimeEntryName.ToString())
            {
                return string.Empty;
            }

            if (propName == InternalProperties.ProjectName.ToString())
            {
                var tmpName = "Project";

                if (GroupById == (int)Constants.ReportsGroupBy.Project)
                {
                    tmpName = tmpName + ": ";
                }

                return tmpName;
            }

            if (propName == ExternalProperties.Date.ToString())
            {
                return "Date: ";
            }

            if (propName.Contains(InternalProperties.MemberName.ToString()))
            {
                var tmpName = "User";

                if (GroupById == (int)Constants.ReportsGroupBy.User)
                {
                    tmpName = tmpName + ": ";
                }

                return tmpName;
            }

            if (propName == InternalProperties.ClientName.ToString())
            {
                var tmpName = "Client";

                if (GroupById == (int)Constants.ReportsGroupBy.Client)
                {
                    tmpName = tmpName + ": ";
                }

                return tmpName;
            }

            if (propName == InternalProperties.Date.ToString())
            {
                var tmpName = "Date";

                if (GroupById == (int)Constants.ReportsGroupBy.User)
                {
                    tmpName = tmpName + ": ";
                }

                return tmpName;
            }

            /*
             * 
             *                 if (GroupById == (int)Constants.ReportsGroupBy.User)
                {
                    PDFcell.NameDisplay = PDFcell.NameDisplay + ": ";
                }
             * */

            return propName;
        }

        private List<PropertyInfo> ExludePropByDefaultGrouping(List<PropertyInfo> props)
        {
            var availableProps = new List<PropertyInfo>();

            foreach (var prop in props)
            {
                if (IsPropByDefaultGrouping(prop.Name))
                {
                    continue;
                }

                availableProps.Add(prop);
            }

            return availableProps;
        }

        private PDFCell GetNameDisplayForTotalHeaderPDF(PropertyInfo prop, string value)
        {
            var PDFcell = new PDFCell
            {
                NameDisplay = prop.Name,
                NameDefault = prop.Name
            };

            #region Grand headers 

            if (prop.Name == ExternalProperties.GrandActualTime.ToString())
            {
                PDFcell.NameDisplay = "Grand Actual Time: ";
                return PDFcell;
            }

            if (prop.Name == ExternalProperties.GrandEstimatedTime.ToString())
            {
                PDFcell.NameDisplay = "Grand Estimated Time: ";
                return PDFcell;
            }

            #endregion

            #region Total headers 
             
            if (prop.Name == InternalProperties.TotalActualTime.ToString())
            {
                PDFcell.NameDisplay = "Total Actual Time: ";
                return PDFcell;
            }

            if (prop.Name == InternalProperties.TotalEstimatedTime.ToString())
            {
                PDFcell.NameDisplay = "Total Estimated Time: ";
                return PDFcell;
            }

            if (prop.Name == InternalProperties.ClientName.ToString() && value == Constants.WithoutClient.Name)
            {
                PDFcell.NameDisplay = string.Empty;
                return PDFcell;
            }

            if (prop.Name == InternalProperties.TimeEntryName.ToString())
            {
                PDFcell.NameDisplay = string.Empty;
                return PDFcell;
            }

            #endregion

            #region Entity headers

            if (prop.Name == InternalProperties.ProjectName.ToString())
            {
                PDFcell.NameDisplay = "Project";

                if (GroupById == (int)Constants.ReportsGroupBy.Project)
                {
                    PDFcell.NameDisplay = PDFcell.NameDisplay + ": ";
                }

                return PDFcell;
            }

            if (prop.Name == InternalProperties.ClientName.ToString())
            {
                PDFcell.NameDisplay = "Client";

                if (GroupById == (int)Constants.ReportsGroupBy.Client)
                {
                    PDFcell.NameDisplay = PDFcell.NameDisplay + ": ";
                }

                return PDFcell;
            }

            if (prop.Name == InternalProperties.Date.ToString())
            {
                PDFcell.NameDisplay = "Date";

                if (GroupById == (int) Constants.ReportsGroupBy.Date)
                {
                    PDFcell.NameDisplay = PDFcell.NameDisplay + ": ";
                }

                return PDFcell;
            }

            if (prop.Name.Contains(InternalProperties.MemberName.ToString()))
            {
                PDFcell.NameDisplay = "User";

                if (GroupById == (int)Constants.ReportsGroupBy.User)
                {
                    PDFcell.NameDisplay = PDFcell.NameDisplay + ": ";
                }

                return PDFcell;
            }

            if (prop.Name == InternalProperties.TaskName.ToString())
            {
                PDFcell.NameDisplay = "Task";
                return PDFcell;
            }

            if (prop.Name == InternalProperties.Description.ToString())
            {
                PDFcell.NameDisplay = "Notes";
                return PDFcell;
            }

            if (prop.Name == InternalProperties.ActualTime.ToString())
            {
                PDFcell.NameDisplay = "Actual Hours";
                return PDFcell;
            }

            if (prop.Name == InternalProperties.EstimatedTime.ToString())
            {
                PDFcell.NameDisplay = "Estimated Hours";
                return PDFcell;
            }

            if (prop.Name == InternalProperties.TimeFrom.ToString())
            {
                PDFcell.NameDisplay = "Start";
                return PDFcell;
            }

            if (prop.Name == InternalProperties.TimeTo.ToString())
            {
                PDFcell.NameDisplay = "Finish";
                return PDFcell;
            }

            #endregion

            return PDFcell;
        }

        #endregion

        #region Common Arrays of available Properties

        private List<PropertyInfo> PropsGrandHeaders = new List<PropertyInfo>();
        private List<PropertyInfo> PropsEntityHeaders = new List<PropertyInfo>();
        private List<PropertyInfo> PropsEntityRows = new List<PropertyInfo>();

        private string GetValueForPeriodCell()
        {
            var dateFormat = new GetDateFormat().GetDateFormaDotNetById(DateFormatId);
            //value = DateTime.Parse(value).ToString(dateFormat, );
            var valueForPeriodCell = "Period: " + DateFrom.ToString(dateFormat, CultureInfo.InvariantCulture) + " - " + DateTo.ToString(dateFormat, CultureInfo.InvariantCulture);
            return valueForPeriodCell;
        }

        private PDFCell GetPeriodPDFCell()
        {
            var dateFormat = new GetDateFormat().GetDateFormaDotNetById(DateFormatId);

            var pdfCell = new PDFCell
            {
                NameDefault = "PeriodName",
                NameDisplay = "Period: ",
                Value = DateFrom.ToString(dateFormat) + " - " + DateTo.ToString(dateFormat),
            };

            return pdfCell;
        }

        #endregion

        private TCell CreateCell<T, TCell>(PropertyInfo prop, T entity) where TCell: PDFCell, new()
        {
            var value = "EmptyValue";

            if (entity != null)
            {
                value = GetFormattedValueForCell(prop, entity);
            }

            var cell = GetNameDisplayForTotalHeaderPDF(prop, value);

            var grandEntityHeadersModel = new TCell
            {
                NameDefault = cell.NameDefault,
                NameDisplay = cell.NameDisplay,
                Value = value
            };

            return grandEntityHeadersModel;
        }

        private string GetFormattedValueForCell<T>(PropertyInfo prop, T entity)
        {
            var value = GetValueFromProp(prop, entity);

            value = UpdateTimeFormatForValue(prop, value);
            value = UpdateProjectNameToUpperCase(prop, value);
            value = UpdateDateFormat(prop, value);
            value = ResetValueForGroupByNone(prop, value);

            return value;
        }

        #endregion
    }
}