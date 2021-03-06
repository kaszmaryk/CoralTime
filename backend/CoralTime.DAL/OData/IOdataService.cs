﻿using CoralTime.ViewModels.Clients;
using CoralTime.ViewModels.Errors;
using CoralTime.ViewModels.MemberProjectRoles;
using CoralTime.ViewModels.Projects;
using CoralTime.ViewModels.Settings;
using CoralTime.ViewModels.Tasks;
using CoralTime.ViewModels.TimeEntries;
using CoralTime.ViewModels.ProjectRole;
using CoralTime.ViewModels.Member;
using System.Collections.Generic;

namespace CoralTime.DAL.OData
{
    public interface IOdataService
    {
        IEnumerable<ProjectView> Projects { get; }

        IEnumerable<ClientView> Clients { get; }

        IEnumerable<MemberView> Members { get; }

        IEnumerable<TimeEntryView> TimeEntries { get; }

        IEnumerable<MemberProjectRoleView> MemberProjectRoles { get; }

        IEnumerable<ProjectRoleView> ProjectRoles { get; }

        IEnumerable<TaskView> Tasks { get; }
        
        IEnumerable<ErrorView> Errors { get; }

        IEnumerable<SettingsView> Settings { get; }

        IEnumerable<ManagerProjectsView> ManagerProjects { get; }

        IEnumerable<ProjectNameView> ProjectsNames { get; }
        
     }
}