using ArielVertex.Application.Performance;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Infrastructure.Persistence;

/// <summary>
/// Seeds a realistic, demo-ready dataset (spec section 3 example users + the MIB workflow from
/// section 5) so the product is fully navigable on first boot. Idempotent: no-ops if users exist.
/// </summary>
public static class DataSeeder
{
    private static readonly string[] Palette =
        { "#1E7FD4", "#17203E", "#2FA8E0", "#6C5CE7", "#00B894", "#E17055", "#0984E3", "#E84393", "#0EA5A5", "#7C3AED" };

    public static async Task SeedAsync(AppDbContext db, AuthSettings auth)
    {
        await db.Database.EnsureCreatedAsync();
        await DatabaseSchemaUpgrader.ApplyAsync(db);
        if (await db.Users.AnyAsync()) return;

        var hash = BCrypt.Net.BCrypt.HashPassword(auth.SeedPassword);
        var now = DateTime.UtcNow;
        int color = 0;
        string NextColor() => Palette[color++ % Palette.Length];

        // ---- Departments ----
        var dEng = new Department { Name = "Engineering", Code = "ENG" };
        var dDel = new Department { Name = "Delivery", Code = "DEL" };
        var dHr = new Department { Name = "Human Resources", Code = "HR" };
        var dBiz = new Department { Name = "Business", Code = "BIZ" };
        var dAdm = new Department { Name = "Administration", Code = "ADM" };
        var dQa = new Department { Name = "Quality", Code = "QA" };
        db.Departments.AddRange(dEng, dDel, dHr, dBiz, dAdm, dQa);
        await db.SaveChangesAsync();

        User Mk(string name, string first, PortalRole role, string designation, Department dept, string skills = "")
            => new()
            {
                Name = name,
                Email = $"{first}@{auth.AllowedDomain}".ToLowerInvariant(),
                PasswordHash = hash,
                Role = role,
                Designation = designation,
                DepartmentId = dept.Id,
                Skills = skills,
                Status = EmployeeStatus.Active,
                JoiningDate = now.AddYears(-2).AddDays(color * 17),
                AvatarColor = NextColor(),
                IsProvisionedFromEntra = false
            };

        // ---- Users (spec section 3) ----
        var superAdmin = Mk("Ariel Administrator", "admin", PortalRole.SuperAdmin, "Platform Owner", dAdm);
        var amit = Mk("Arc Menon", "arc", PortalRole.CeoAdmin, "Chief Executive Officer", dAdm);
        var manita = Mk("Mareena Thomas", "mareena", PortalRole.HrManager, "HR Manager", dHr);
        var shuchita = Mk("Surbeen Kaur", "surbeen", PortalRole.HrDirector, "HR Director", dHr);
        var anjali  = Mk("anjali Kaur", "anjali", PortalRole.HrRecruiter, "HR Recruiter", dHr);
        var sudhir = Mk("Shepherd Dsouza", "shepherd", PortalRole.ProjectManager, "Project Manager", dDel);
        var madhu = Mk("Maria Fernandes", "maria", PortalRole.ProjectCoordinator, "Project Coordinator", dDel);
        var amrit = Mk("Arveen Malhotra", "arveen", PortalRole.BusinessDirector, "Business Director", dBiz);
        var arun = Mk("Arun Mehta", "arun", PortalRole.BusinessManager, "Business Manager", dBiz);
        var sid = Mk("Sid Kapoor", "sid", PortalRole.BusinessPerson, "Business Analyst", dBiz);
        var komal = Mk("Komal Jain", "komal", PortalRole.BusinessPerson, "Business Analyst", dBiz);
        var nikhil = Mk("Nikhil Reddy", "nikhil", PortalRole.TechnicalLead, "Technical Lead", dEng, "Architecture, Code Review, .NET, React");
        var amandeep = Mk("Amandeep Kaur", "amandeep", PortalRole.Frontdesk, "Front Desk Executive", dAdm);
        var rajat = Mk("Rajat Malhotra", "rajat", PortalRole.SystemAdmin, "System Administrator", dAdm);
        var akshay = Mk("Akshay Gupta", "akshay", PortalRole.Accountant, "Accountant", dAdm);
        var rahul = Mk("Rahul Deshmukh", "rahul", PortalRole.Employee, "Software Engineer", dEng, "C#, .NET, React, SQL");
        var priya = Mk("Priya Menon", "priya", PortalRole.Employee, "Software Engineer", dEng, "React, TypeScript, Node");
        var sneha = Mk("Sneha Pillai", "sneha", PortalRole.Employee, "QA Engineer", dQa, "Automation, Selenium, API Testing");
        var vikram = Mk("Vikram Bose", "vikram", PortalRole.Employee, "Software Engineer", dEng, "Java, Spring, Angular");

        var users = new[] { superAdmin, amit, manita, shuchita, anjali, sudhir, madhu, amrit, arun, sid, komal,
            nikhil, amandeep, rajat, akshay, rahul, priya, sneha, vikram };
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        foreach (var u in users) u.EmployeeCode = $"AV{u.Id:D4}";
        // Reporting lines
        foreach (var dev in new[] { rahul, priya, vikram }) dev.ManagerId = sudhir.Id;
        sneha.ManagerId = sudhir.Id;
        nikhil.ManagerId = sudhir.Id;
        madhu.ManagerId = sudhir.Id;
        sid.ManagerId = arun.Id; komal.ManagerId = arun.Id;
        await db.SaveChangesAsync();

        // ---- Projects (spec section 5 MIB + a second for occupancy) ----
        var mib = new Project
        {
            Code = "MIB", Name = "MIB Portal", Description = "Managed Investment Brokerage client portal.",
            Status = ProjectStatus.Active, Health = ProjectHealth.Green, Priority = Priority.High,
            StartDate = now.AddMonths(-4), ExpectedEndDate = now.AddMonths(3),
            ClientName = "MIB Financial", BusinessOwner = "Sid Kapoor", Tags = "web,finance,react,dotnet",
            Notes = "Flagship delivery. Weekly customer calls on Thursdays."
        };
        var apex = new Project
        {
            Code = "APEX", Name = "Apex CRM", Description = "Sales CRM revamp for Apex Retail.",
            Status = ProjectStatus.Active, Health = ProjectHealth.Amber, Priority = Priority.Medium,
            StartDate = now.AddMonths(-2), ExpectedEndDate = now.AddMonths(5),
            ClientName = "Apex Retail", BusinessOwner = "Komal Jain", Tags = "crm,angular,java",
            Notes = "Amber due to pending API access from client."
        };
        db.Projects.AddRange(mib, apex);
        await db.SaveChangesAsync();

        ProjectMember PM(Project p, User u, ProjectRole role, int alloc) => new()
        { ProjectId = p.Id, UserId = u.Id, RoleOnProject = role, AllocationPct = alloc, StartDate = p.StartDate, IsActive = true };

        db.ProjectMembers.AddRange(
            PM(mib, sudhir, ProjectRole.ProjectManager, 20),
            PM(mib, madhu, ProjectRole.ProjectCoordinator, 30),
            PM(mib, nikhil, ProjectRole.TechnicalLead, 40),
            PM(mib, sid, ProjectRole.BusinessPerson, 10),
            PM(mib, amrit, ProjectRole.BusinessPerson, 0),
            PM(mib, rahul, ProjectRole.Developer, 100),
            PM(mib, priya, ProjectRole.Developer, 80),
            PM(mib, sneha, ProjectRole.QA, 60),
            PM(apex, sudhir, ProjectRole.ProjectManager, 15),
            PM(apex, madhu, ProjectRole.ProjectCoordinator, 20),
            PM(apex, vikram, ProjectRole.Developer, 100),
            PM(apex, priya, ProjectRole.Developer, 20),
            PM(apex, komal, ProjectRole.BusinessPerson, 10)
        );
        await db.SaveChangesAsync();

        // ---- Status updates (spec 6.6) — Sneha intentionally missing today ----
        StatusUpdate SU(User u, int daysAgo, UpdateStatus st, string done, string next, string blockers, decimal hrs, string client)
            => new()
            {
                ProjectId = mib.Id, UserId = u.Id, UpdateDate = now.AddDays(-daysAgo).Date,
                WorkCompleted = done, NextPlannedWork = next, Blockers = blockers, HoursSpent = hrs, Status = st,
                InternalNote = blockers.Length > 0 ? "Escalated to tech lead." : "",
                ClientShareableSummary = client
            };
        db.StatusUpdates.AddRange(
            SU(rahul, 1, UpdateStatus.OnTrack, "Completed portfolio dashboard API and unit tests.", "Wire dashboard charts to live endpoints.", "", 8, "Portfolio dashboard backend completed."),
            SU(rahul, 0, UpdateStatus.OnTrack, "Integrated charts, fixed pagination.", "Start transactions export.", "", 7.5m, "Dashboard charts now live."),
            SU(priya, 1, UpdateStatus.AtRisk, "Built onboarding flow UI.", "Handle KYC validation states.", "Awaiting KYC rules from client.", 6, "Onboarding screens in progress."),
            SU(priya, 0, UpdateStatus.Blocked, "Blocked on KYC ruleset.", "Resume once ruleset received.", "KYC ruleset still pending from MIB Financial.", 3, "Onboarding blocked pending client input.")
        );
        await db.SaveChangesAsync();

        // ---- Reviews (spec 6.7) ----
        var codeReviewReq = new ReviewRequest
        {
            ProjectId = mib.Id, SubjectUserId = rahul.Id, RequestedById = manita.Id, AssignedToId = nikhil.Id,
            ReviewType = ReviewType.CodeReview, Status = ReviewRequestStatus.Open,
            Notes = "Quarterly code quality review for dashboard module.", DueDate = now.AddDays(7)
        };
        var projReviewReq = new ReviewRequest
        {
            ProjectId = mib.Id, SubjectUserId = priya.Id, RequestedById = manita.Id, AssignedToId = sudhir.Id,
            ReviewType = ReviewType.ProjectReview, Status = ReviewRequestStatus.Scheduled,
            Notes = "Mid-project contribution review.", DueDate = now.AddDays(4)
        };
        db.ReviewRequests.AddRange(codeReviewReq, projReviewReq);
        await db.SaveChangesAsync();

        db.ReviewMeetings.Add(new ReviewMeeting
        {
            ReviewRequestId = projReviewReq.Id, Title = "MIB Project Review — Priya Menon",
            Description = "Mid-project contribution review.", ScheduledAt = now.AddDays(2).Date.AddHours(10),
            DurationMinutes = 45, Attendees = $"{sudhir.Email},{priya.Email}",
            OutlookEventId = "AV-EVT-mibprojectrev-seed", TeamsJoinUrl = "https://teams.microsoft.com/l/meetup-join/av-placeholder/seed-projreview",
            ScheduledById = madhu.Id
        });
        await db.SaveChangesAsync();

        // ---- Feedback (spec 6.8) — one Published, one awaiting HR approval ----
        var fbPublished = new Feedback
        {
            SubjectUserId = rahul.Id, AuthorId = sudhir.Id, ProjectId = mib.Id, Period = "2026-Q2",
            Status = FeedbackStatus.Published,
            ConstructiveSummary = "Strong, reliable contributor who consistently delivers quality backend work on time.",
            Strengths = "Excellent code quality; proactive on testing; dependable delivery.",
            ImprovementAreas = "Increase participation in design discussions.",
            ActionPlan = "Lead one design walkthrough next quarter.",
            InternalNotes = "Consider for senior engineer track.",
            ApprovedById = manita.Id, ApprovedAt = now.AddDays(-20), PublishedAt = now.AddDays(-20)
        };
        var fbDraft = new Feedback
        {
            SubjectUserId = priya.Id, AuthorId = sudhir.Id, ProjectId = mib.Id, Period = "2026-Q3",
            Status = FeedbackStatus.Submitted,
            ConstructiveSummary = "Creative front-end engineer; navigated a blocker-heavy sprint with resilience.",
            Strengths = "Strong UI craftsmanship; good client empathy.",
            ImprovementAreas = "Communicate blockers earlier.",
            ActionPlan = "Adopt daily blocker flag in status updates.",
            InternalNotes = "Awaiting HR approval before publishing."
        };
        db.Feedbacks.AddRange(fbPublished, fbDraft);
        await db.SaveChangesAsync();

        void AddScores(Feedback f, params (string key, int score, bool na)[] rows)
        {
            foreach (var (key, score, na) in rows)
            {
                var cat = PerformanceModel.Categories.First(c => c.Key == key);
                db.FeedbackCategoryScores.Add(new FeedbackCategoryScore
                { FeedbackId = f.Id, Category = key, Score = score, NotApplicable = na, Comment = "" });
            }
        }
        AddScores(fbPublished, ("technical", 88, false), ("delivery", 85, false), ("collaboration", 74, false),
            ("client", 0, true), ("accountability", 90, false), ("availability", 82, false));
        AddScores(fbDraft, ("technical", 80, false), ("delivery", 72, false), ("collaboration", 78, false),
            ("client", 70, false), ("accountability", 75, false), ("availability", 68, false));
        await db.SaveChangesAsync();

        // ---- Performance reports (spec 6.9) — trend for Rahul's employee dashboard ----
        void AddReport(User u, string period, decimal score, bool published)
        {
            var rpt = new PerformanceReport
            {
                SubjectUserId = u.Id, PeriodType = PerformancePeriodType.Monthly, Period = period,
                OverallScore = score, Rating = PerformanceModel.RatingFor(score),
                Strengths = "Reliable delivery and strong technical execution.",
                ImprovementAreas = "Design-level collaboration.",
                RecommendedActions = "Lead a design session; mentor a junior.",
                DataSources = "Status updates, code review, project review, PM feedback.",
                IsPublished = published, ApprovedById = published ? manita.Id : null,
                PublishedAt = published ? now : null
            };
            db.PerformanceReports.Add(rpt);
            db.SaveChanges();
            foreach (var c in PerformanceModel.Categories)
            {
                var s = c.Key == "client" ? 0 : (int)Math.Clamp(score + (c.Key.Length % 5) - 2, 60, 98);
                db.PerformanceCategoryScores.Add(new PerformanceCategoryScore
                {
                    PerformanceReportId = rpt.Id, Category = c.Key, Score = c.Key == "client" ? 0 : s,
                    Weight = c.DefaultWeight, NotApplicable = c.Key == "client"
                });
            }
            db.SaveChanges();
        }
        AddReport(rahul, "2026-04", 79.5m, true);
        AddReport(rahul, "2026-05", 83.0m, true);
        AddReport(rahul, "2026-06", 86.5m, true);

        // ---- Business comments (spec 6.3) ----
        db.ProjectComments.AddRange(
            new ProjectComment { ProjectId = mib.Id, AuthorId = sid.Id, Message = "Client confirmed the KYC ruleset will be shared by Friday — should unblock the onboarding module." },
            new ProjectComment { ProjectId = mib.Id, AuthorId = sudhir.Id, Message = "Thanks Sid. Priya is holding onboarding until then; dashboard work is on track in the meantime." }
        );
        await db.SaveChangesAsync();

        // ---- Resource request (spec 6.11) ----
        db.ResourceRequests.Add(new ResourceRequest
        {
            ProjectId = mib.Id, RequestedById = sudhir.Id, RoleTitle = "Senior React Developer",
            Skills = "React, TypeScript, State Management", Reason = "Scale onboarding module delivery.",
            Priority = Priority.High, Count = 1, ExpectedStartDate = now.AddDays(21),
            Status = ResourceRequestStatus.UnderReview
        });
        await db.SaveChangesAsync();

        // ---- Notifications ----
        db.Notifications.AddRange(
            new Notification { RecipientId = nikhil.Id, Type = NotificationType.ReviewRequested, Title = "Code review requested", Message = "HR requested a code review for Rahul Deshmukh on MIB Portal.", Link = "/reviews" },
            new Notification { RecipientId = rahul.Id, Type = NotificationType.FeedbackPublished, Title = "Your Q2 feedback is available", Message = "Your approved performance feedback for 2026-Q2 has been published.", Link = "/my-performance" },
            new Notification { RecipientId = manita.Id, Type = NotificationType.FeedbackSubmitted, Title = "Feedback awaiting approval", Message = "Sudhir submitted Q3 feedback for Priya Menon.", Link = "/feedback" },
            new Notification { RecipientId = sudhir.Id, Type = NotificationType.StatusMissed, Title = "Missing status update", Message = "Sneha Pillai has not submitted today's status update for MIB Portal.", Link = "/status-updates" }
        );

        // ---- Seed audit + sync history ----
        // ---- Expense module (Front Desk) ----
        db.ExpenseSettings.Add(new ExpenseSetting
        {
            ApprovalRequiredByDefault = true,
            ApproverRoles = "HrDirector,Accountant",
            DailySummaryRecipients = amit.Email,      // CEO by default
            WeeklySummaryRecipients = shuchita.Email  // HR Director
        });
        db.Expenses.AddRange(
            new Expense { Title = "Office stationery restock", Category = ExpenseCategory.OfficeSupplies, Amount = 4250m, Vendor = "Staples India", ExpenseDate = now.AddDays(-2), PaymentMethod = "UPI", InvoiceNumber = "INV-2211", Status = ExpenseStatus.Paid, ApprovalRequired = false, RaisedById = amandeep.Id, PaidAt = now.AddDays(-1) },
            new Expense { Title = "Pantry supplies (coffee, tea)", Category = ExpenseCategory.Refreshments, Amount = 3100m, Vendor = "BigBasket", ExpenseDate = now.AddDays(-1), PaymentMethod = "Card", InvoiceNumber = "BB-88231", Status = ExpenseStatus.PaymentRequested, ApprovalRequired = true, RaisedById = amandeep.Id },
            new Expense { Title = "AC servicing - 3rd floor", Category = ExpenseCategory.Maintenance, Amount = 6800m, Vendor = "CoolCare Services", ExpenseDate = now, PaymentMethod = "Bank Transfer", InvoiceNumber = "CC-5540", Status = ExpenseStatus.Approved, ApprovalRequired = true, RaisedById = amandeep.Id, ApproverId = shuchita.Id, DecidedAt = now, DecisionNote = "Approved." }
        );

        // ---- Bill module (Accounts Payable) ----
        db.BillSettings.Add(new BillSetting
        {
            ApprovalRequiredByDefault = true,
            ApproverRoles = "HrDirector,Accountant",
            DailySummaryRecipients = amit.Email,
            WeeklySummaryRecipients = shuchita.Email,
            ReminderDaysBefore = "7,3,1",
            ReminderRecipients = shuchita.Email
        });
        db.Bills.AddRange(
            new Bill { Title = "Office rent - September", Category = "Office Rent", Amount = 85000m, Vendor = "ABC Properties", BillDate = now.AddDays(-5), DueDate = now.AddDays(25), PaymentMethod = "Bank Transfer", InvoiceNumber = "RENT-2026-09", Status = BillStatus.Submitted, ApprovalRequired = true, RaisedById = amandeep.Id },
            new Bill { Title = "AWS cloud hosting", Category = "Software Subscription", Amount = 12500m, Vendor = "Amazon Web Services", BillDate = now.AddDays(-10), DueDate = now.AddDays(5), PaymentMethod = "Card", InvoiceNumber = "AWS-INV-88231", Status = BillStatus.Approved, ApprovalRequired = true, RaisedById = amandeep.Id, ApproverId = shuchita.Id, DecidedAt = now.AddDays(-2), DecisionNote = "Approved." },
            new Bill { Title = "Internet broadband - Q3", Category = "Utilities", Amount = 4800m, Vendor = "Jio Fiber", BillDate = now.AddDays(-30), DueDate = now.AddDays(-2), PaymentMethod = "UPI", InvoiceNumber = "JIO-Q3-441", Status = BillStatus.Paid, ApprovalRequired = false, RaisedById = amandeep.Id, PaidAt = now.AddDays(-1) }
        );
        // ---- Admin-editable configuration (settings, feature flags) ----
        PlatformSetting Ps(string key, string val, string group, string label, string type, string desc) =>
            new() { Key = key, Value = val, Group = group, Label = label, Type = type, Description = desc };
        db.PlatformSettings.AddRange(
            Ps("pip.enabled", "true", "Performance Improvement (PIP)", "Auto-PIP enabled", "bool", "Automatically start a PIP when an approved score falls below the threshold."),
            Ps("pip.thresholdPercent", "50", "Performance Improvement (PIP)", "PIP threshold (%)", "number", "Approved performance below this percentage auto-initiates a PIP and emails HR + the employee."),
            Ps("feature.expenses", "true", "Modules", "Expenses (Front Desk)", "bool", "Show/hide the Expenses module across the portal."),
            Ps("feature.bills", "true", "Modules", "Bills (Accounts Payable)", "bool", "Show/hide the Bills module across the portal."),
            Ps("feature.pip", "true", "Modules", "Performance Improvement Plans", "bool", "Show/hide the PIP module."),
            Ps("feature.reviews", "true", "Modules", "Reviews", "bool", "Show/hide the Reviews module."),
            Ps("feature.feedback", "true", "Modules", "Feedback", "bool", "Show/hide the Feedback module."),
            Ps("feature.performanceReports", "true", "Modules", "Performance Reports", "bool", "Show/hide the Performance Reports module."),
            Ps("feature.resources", "true", "Modules", "Resource Visibility", "bool", "Show/hide Resource Visibility."),
            Ps("feature.hiring", "true", "Modules", "Hiring Requests", "bool", "Show/hide Hiring Requests."),
            Ps("feature.reports", "true", "Modules", "Reports", "bool", "Show/hide the Reports module."),
            Ps("feature.minutes", "true", "Modules", "Meetings & Minutes", "bool", "Show/hide the meeting-minutes module (Project Coordinator & HR)."),
            Ps("minutes.aiPolish", "true", "Meetings & Minutes", "AI-polished minutes", "bool", "When an AI service is configured (Ai:Enabled + key), use it for AI-grade minutes & grammar. Off = always use the built-in generator.")
        );
        db.NotificationTemplates.Add(new NotificationTemplate
        {
            Key = "pip.combined", Name = "PIP initiated (HR + employee)",
            Subject = "Performance Improvement Plan initiated — {employee}",
            Body = "A Performance Improvement Plan has been initiated for {employee}.\n\nThe {period} approved performance score of {score}% is below the {threshold}% threshold. HR and the employee are notified together so a supportive improvement plan can begin. Please review the plan in the portal.\n\n— Ariel Vertex",
            Placeholders = "{employee}, {score}, {threshold}, {period}"
        });
        db.NotificationTemplates.Add(new NotificationTemplate
        {
            Key = "minutes.sent", Name = "Minutes of meeting (to attendees)",
            Subject = "Minutes of meeting — {title}",
            Body = "Hello,\n\nPlease find below the minutes of \"{title}\" held on {date}.\n\n{minutes}\n\nIf anything needs correcting, reply and we'll send an updated version.\n\n— Ariel Vertex",
            Placeholders = "{title}, {date}, {minutes}"
        });

        // ---- Meeting minutes (Project Coordinator & HR) — one sent, one awaiting preview ----
        var mmSent = new MeetingMinute
        {
            Title = "MIB Portal — Weekly Sync", MeetingDate = now.AddDays(-3).Date.AddHours(11),
            Location = "Teams", CreatedById = madhu.Id,
            Attendees = $"{sudhir.Email}, {nikhil.Email}, {rahul.Email}, {priya.Email}, {sid.Email}",
            RawNotes = "dashboard api done, charts wired up\nkyc still blocked, waiting on client ruleset\nagreed to demo onboarding next thursday\npriya to prepare test data by wednesday\nnikhil will review the export module",
            GeneratedByAi = false, Status = MeetingMinuteStatus.Sent,
            ApprovedById = madhu.Id, ApprovedAt = now.AddDays(-3).Date.AddHours(12), SentAt = now.AddDays(-3).Date.AddHours(12), SentCount = 1,
            MinutesText =
                "# Minutes of Meeting — MIB Portal — Weekly Sync\n\n" +
                "**Date:** " + now.AddDays(-3).Date.AddHours(11).ToString("dddd, dd MMM yyyy, HH:mm") + "\n" +
                "**Location:** Teams\n" +
                $"**Attendees:** {sudhir.Email}, {nikhil.Email}, {rahul.Email}, {priya.Email}, {sid.Email}\n\n" +
                "## Discussion\n- Dashboard API is complete and the charts are wired up.\n- KYC is still blocked, waiting on the client ruleset.\n\n" +
                "## Decisions\n- Agreed to demo the onboarding flow next Thursday.\n\n" +
                "## Action Items\n- Priya to prepare test data by Wednesday.\n- Nikhil will review the export module."
        };
        var mmDraft = new MeetingMinute
        {
            Title = "Apex CRM — Kickoff Notes", MeetingDate = now.Date.AddHours(15),
            Location = "Meeting Room 2", CreatedById = madhu.Id,
            Attendees = $"{sudhir.Email}, {vikram.Email}, {komal.Email}",
            RawNotes = "scope confirmed for phase 1\nclient wants angular front end\nvikram to set up repo and ci\nkomal will share the requirement doc tomorrow\ndecided weekly calls on tuesdays",
            Status = MeetingMinuteStatus.Draft
        };
        db.MeetingMinutes.AddRange(mmSent, mmDraft);

        // ---- Performance management (appraisal cycle, appraisals, goals, promotion, training) ----
        var cycle = new AppraisalCycle
        {
            Name = "Q3 2026 Appraisal", StartDate = now.AddDays(-20), EndDate = now.AddDays(20),
            Status = CycleStatus.Active, CreatedById = manita.Id
        };
        db.AppraisalCycles.Add(cycle);
        db.Appraisals.AddRange(
            new Appraisal { Cycle = cycle, EmployeeId = rahul.Id, ManagerId = sudhir.Id, Stage = AppraisalStage.SelfPending },
            new Appraisal { Cycle = cycle, EmployeeId = priya.Id, ManagerId = sudhir.Id, Stage = AppraisalStage.SelfSubmitted,
                SelfRating = 4.0m, SelfComments = "Delivered the reporting module ahead of schedule.", SelfSubmittedAt = now.AddDays(-3) },
            new Appraisal { Cycle = cycle, EmployeeId = sneha.Id, ManagerId = sudhir.Id, Stage = AppraisalStage.ManagerCompleted,
                SelfRating = 3.5m, SelfComments = "Improved automation coverage.", SelfSubmittedAt = now.AddDays(-6),
                ManagerRating = 3.8m, ManagerComments = "Strong quality focus; grow on API testing depth.", ManagerReviewedAt = now.AddDays(-2) },
            new Appraisal { Cycle = cycle, EmployeeId = vikram.Id, ManagerId = sudhir.Id, Stage = AppraisalStage.Released,
                SelfRating = 4.0m, SelfSubmittedAt = now.AddDays(-8), ManagerRating = 4.2m, ManagerReviewedAt = now.AddDays(-4),
                FinalRating = 4.2m, ReleasedAt = now.AddDays(-1) }
        );
        db.Goals.AddRange(
            new Goal { EmployeeId = rahul.Id, AssignedById = sudhir.Id, Cycle = cycle, Title = "Ship the billing API v2",
                Description = "Design and deliver v2 with full test coverage.", Category = "Project Delivery", Weightage = 40, Progress = 55,
                TargetDate = now.AddDays(35), Status = GoalStatus.InProgress },
            new Goal { EmployeeId = rahul.Id, AssignedById = sudhir.Id, Cycle = cycle, Title = "Level up on system design",
                Category = "Technical Growth", Weightage = 30, Progress = 20, TargetDate = now.AddDays(60), Status = GoalStatus.InProgress },
            new Goal { EmployeeId = priya.Id, AssignedById = sudhir.Id, Cycle = cycle, Title = "Own the design system",
                Category = "Quality & Ownership", Weightage = 50, Progress = 70, TargetDate = now.AddDays(30), Status = GoalStatus.InProgress }
        );
        db.Promotions.Add(new Promotion
        {
            EmployeeId = priya.Id, RecommendedById = sudhir.Id, CurrentDesignation = "Software Engineer",
            ProposedDesignation = "Senior Software Engineer", ProposedSalary = 1400000m,
            Justification = "Consistently exceeds delivery expectations and mentors juniors.", Stage = PromotionStage.ManagerRecommended
        });
        db.TrainingRecommendations.Add(new TrainingRecommendation
        {
            EmployeeId = rahul.Id, CreatedById = sudhir.Id, SkillGap = "System Design",
            RecommendedTraining = "Distributed Systems fundamentals", DurationMonths = 3, Status = TrainingStatus.Recommended, Source = "AI"
        });

        db.AuditLogs.Add(new AuditLog { ActorName = "system", Action = AuditAction.EmployeeSyncRun, EntityType = "Sync", Summary = "Initial seed dataset created." });
        db.MicrosoftSyncLogs.Add(new MicrosoftSyncLog { RunAt = now.AddDays(-1), Status = SyncStatus.Success, Created = 0, Updated = 0, WasManual = false, TriggeredBy = "system", Message = "Directory sync disabled (local mode)." });
        await db.SaveChangesAsync();
    }
}
