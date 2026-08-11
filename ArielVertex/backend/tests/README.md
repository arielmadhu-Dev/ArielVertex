# Backend tests

Run the complete backend suite from the repository root:

```powershell
dotnet test ArielVertex.sln -c Release
```

The integration tests boot the real ASP.NET Core pipeline against a uniquely named temporary
SQLite database. The normal seed data is loaded with a test-only password. Automation, Microsoft
Graph, Outlook delivery, Teams delivery, and external AI are disabled, so the suite never contacts
external systems or modifies the development database.

Current coverage includes:

- health, login, JWT authentication, request validation, and capability authorization;
- anonymous-access enforcement across every controller family;
- project visibility, project-scoped access, status submission, and document validation;
- business-safe status projections and employee feedback/performance confidentiality;
- feedback approval/publication, review scheduling/completion, expense approval/payment, and
  offline meeting-minutes workflows;
- HR and system-administrator module smoke tests;
- performance rating boundaries, weighted scoring, N/A redistribution, and appraisal blending.

Live Microsoft Entra/Graph, Outlook, Teams, Anthropic, PostgreSQL, reverse-proxy, and malware-scanner
checks belong in a separately configured staging test run because they require external credentials
or infrastructure.
