# Horizon Private Training College – Student Information System (prototype)

A web-based student information system built with ASP.NET Core 8 MVC.

## How to run the application

### What you need
- **.NET 8 SDK** (or a newer SDK with the .NET 8 runtime installed). Check with `dotnet --list-sdks`. Download from https://dotnet.microsoft.com/download/dotnet/8.0
- **Visual Studio 2022 (version 17.13 or later)** with the *ASP.NET and web development* workload. This is only needed for Option A. Older versions cannot open the `.slnx` solution file, so use Option B instead.
- An internet connection for the icons and font. The app still runs without one, but the icons won't show.

No database is needed. The app creates its own data file with demo data the first time it starts.

### Option A: Visual Studio
1. Open `Student Information system.slnx` in Visual Studio.
2. At the top, choose the **https** (or **IIS Express**) profile next to the green play button.
3. Press **F5** (or **Ctrl+F5** to run without debugging).
4. The browser opens on the home page. If asked to trust the development certificate, click **Yes**.

### Option B: Command line
1. Open a terminal in the `Student Information system` folder (the one with the `.csproj` file).
2. The first time only, trust the HTTPS development certificate:
   ```
   dotnet dev-certs https --trust
   ```
3. Start the app:
   ```
   dotnet run --launch-profile https
   ```
4. Open **https://localhost:7218** in your browser. For plain HTTP, use `dotnet run --launch-profile http` and open **http://localhost:5293**.
5. Press **Ctrl+C** in the terminal to stop the app.

### Signing in
Click **Portal login** and use one of the demo accounts below. To test the full applicant journey, click **Apply online**, submit an application, then sign in as `admin` to approve it.

### Troubleshooting
- **"The file is locked by: IIS Express Worker Process"** when building: the app is still running. Stop it in Visual Studio (Shift+F5) or close IIS Express from the system tray, then build again.
- **Port already in use:** another copy of the app is running. Stop it, or change the port in `Properties/launchSettings.json`.
- **Browser warns that the connection is not private:** run `dotnet dev-certs https --trust` and restart the browser.
- **Start over with fresh demo data:** stop the app, delete the `App_Data` folder, and start it again.

## Demo accounts (password for all: `Password123!`)
| Username | Role |
|---|---|
| `student`, `student2`, `student3` | Student (active) |
| `applicant` | Student whose application is awaiting approval |
| `lecturer`, `lecturer2` | Lecturer |
| `admin`, `principal` | Management |

## Features
- **Public:** landing page with open courses, online application with ID copy + high school certificate upload.
- **Student portal:** dashboard, online course registration (approved by management), course content, results with weighted marks, printable academic record, document upload/retrieval, profile, communications.
- **Lecturer portal:** gradebook to capture marks + comments (optionally notifies the class), publish course content (files, links, assignments with due dates), message a class or student.
- **Management portal:** approve applications, approve/reject course registrations, searchable student records, document retrieval and verification, course setup and lecturer assignment, staff accounts, performance reports, announcements to everyone / students / lecturers / a course / an individual.

## Prototype notes
- Data is stored in `App_Data/sis-data.json`; uploaded files in `App_Data/uploads` (outside `wwwroot`, served only through permission-checked downloads). Delete `App_Data` to reset to demo data.
- `App_Data` is git-ignored because it holds personal documents.
- For production: move to SQL Server + EF Core, real email/SMS for communications, and remove the demo accounts from the login page.
