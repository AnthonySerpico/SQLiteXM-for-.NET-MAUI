# SQLiteXM Sample Applications

This directory contains three sample applications demonstrating different aspects of SQLiteXM. Each sample serves a specific learning purpose.

---

## 📚 Sample Overview

### 1️⃣ [QueryGalleryDemo](QueryGalleryDemo/) - Interactive Query Showcase

**🎯 Purpose:** Comprehensive demonstration of SQLiteXM's query capabilities

**📦 Pre-Built Version Available:** ✅ [Download Windows ZIP](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/releases/latest)

**What it demonstrates:**
- 50+ working query examples across 10 categories
- Basic queries (SELECT, WHERE, ORDER BY, LIKE)
- Joins and table relationships (INNER, LEFT JOIN)
- Aggregations (COUNT, SUM, AVG, GROUP BY)
- Advanced LINQ (pagination, complex sorting, compound filters)
- Many-to-many relationships via junction tables
- Transaction patterns (commit, rollback, atomic operations)
- Data modification (INSERT, UPDATE, DELETE, bulk operations)
- Raw SQL execution from configuration files
- Performance metrics and optimization techniques
- Realistic data: ~25,000 records in Chinook-style music database

**Recommended for:**
- Developers evaluating SQLiteXM
- Learning query patterns and best practices
- Understanding LINQ-to-SQL translation
- Seeing working examples of every feature

**How to use:**
- **Download & Run:** Extract the [pre-built Windows ZIP](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/releases/latest) and launch the app
- **Build from Source:** Open solution, set QueryGalleryDemo as startup project, run on any platform

---

### 2️⃣ [DirectBindingDemo](DirectBindingDemo/) - UI Binding Tutorial

**🎯 Purpose:** Teaching example for data binding patterns

**📦 Pre-Built Version:** ❌ Source code only - designed for step-through learning

**What it demonstrates:**
- Direct entity-to-UI binding with INotifyPropertyChanged
- ObservableCollection population from database queries
- Real-time UI updates when data changes
- MVVM pattern with CommunityToolkit.Mvvm
- Form validation and data entry patterns
- Entity persistence via SaveAsync() / DeleteAsync()

**Recommended for:**
- Developers building MAUI forms and data-entry screens
- Learning MVVM binding patterns
- Understanding entity lifecycle (create → modify → save → delete)
- Integrating SQLiteXM into ViewModels

**How to use:**
1. Open the solution in Visual Studio
2. Set `DirectBindingDemo` as the startup project
3. **Read the code** in `Views/` and `ViewModels/` folders
4. Run the app and interact with the UI
5. **Set breakpoints** to step through binding updates
6. Examine how entity changes propagate to the UI

**Key files to study:**
- `ViewModels/EmailPasswordViewModel.cs` - Entity binding patterns
- `Views/EmailPasswordPage.xaml` - Binding syntax and validation
- `Models/User.cs` - Entity definition with INotifyPropertyChanged

---

### 3️⃣ [RegistrationDemo](RegistrationDemo/) - User Management Tutorial

**🎯 Purpose:** Teaching example for registration, validation, and authentication flows

**📦 Pre-Built Version:** ❌ Source code only - designed for step-through learning

**What it demonstrates:**
- Multi-page registration wizard flow
- Field-level validation (email format, password strength, required fields)
- Password hashing and secure storage patterns
- User authentication and session management
- Entity validation before persistence
- Error handling and user feedback
- Navigation between registration steps

**Recommended for:**
- Developers building login/registration systems
- Learning validation patterns
- Understanding secure password storage
- Implementing multi-step workflows

**How to use:**
1. Open the solution in Visual Studio
2. Set `RegistrationDemo` as the startup project
3. **Read the code** in `Services/` and `ViewModels/` folders
4. Run the app and complete the registration flow
5. **Set breakpoints** in validation logic
6. Study how validation errors surface to the UI

**Key files to study:**
- `ViewModels/RegistrationViewModel.cs` - Validation and state management
- `Services/AuthenticationService.cs` - Password hashing and user lookup
- `Models/User.cs` - Entity with validation attributes
- `Views/RegistrationPage.xaml` - Multi-step UI flow

---

## 🚀 Getting Started with the Samples

### Option 1: Run QueryGalleryDemo Immediately (No Build Required)

1. **Download:** [Latest Release ZIP](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/releases/latest)
2. **Extract** the ZIP to any folder
3. **Run** `QueryGalleryDemo.exe` (Windows 10+ with .NET 9 Runtime)
4. Browse 50+ query examples with live execution and performance metrics

### Option 2: Build from Source (All Samples)

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI.git
   ```

2. **Open Solution:**
   - Launch Visual Studio 2022 or later
   - Open `SQLiteXM.sln`

3. **Select a Sample:**
   - Right-click the sample project (QueryGalleryDemo, DirectBindingDemo, or RegistrationDemo)
   - Set as Startup Project

4. **Choose Platform:**
   - Windows: Run directly
   - Android: Select Android emulator or physical device
   - iOS/Mac: Requires Mac with Xcode

5. **Build & Run:**
   - Press F5 or click the Run button

### System Requirements

**Development:**
- Visual Studio 2022+ or VS Code with .NET MAUI workload
- .NET 9 SDK
- Platform-specific tooling (Android SDK, Xcode for iOS/Mac)

**Running Pre-Built (QueryGalleryDemo only):**
- Windows 10 version 17763 or higher
- .NET 9 Desktop Runtime

---

## 📖 Learning Path

**New to SQLiteXM?** Follow this recommended path:

1. **Start with QueryGalleryDemo** → Download the pre-built app, explore categories, run queries
2. **Study DirectBindingDemo source** → Open in Visual Studio, step through binding code
3. **Examine RegistrationDemo source** → Understand validation and authentication patterns
4. **Build your own app** → Apply learned patterns to your project

**Already experienced?** Jump straight to the category that matches your needs:
- **Database queries** → QueryGalleryDemo (run pre-built)
- **UI binding** → DirectBindingDemo (read source)
- **Forms & validation** → RegistrationDemo (read source)

---

## 🗂️ Sample Comparison

| Feature | QueryGalleryDemo | DirectBindingDemo | RegistrationDemo |
|---------|------------------|-------------------|------------------|
| **Pre-built download** | ✅ Windows ZIP | ❌ Source only | ❌ Source only |
| **Primary purpose** | Feature showcase | Binding tutorial | Validation tutorial |
| **Database size** | ~25,000 records | ~10-20 records | ~5-10 records |
| **Best for** | Evaluating SQLiteXM | Learning binding | Learning forms |
| **Complexity** | Advanced | Intermediate | Intermediate |
| **Code to study** | Query patterns | ViewModel binding | Validation logic |
| **Interactive learning** | Run queries live | Step through code | Step through code |
| **Use case** | Query gallery | Data entry forms | User registration |

---

## 💡 Additional Resources

**SQLiteXM Documentation:**
- [Getting Started Guide](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/README.md)
- [Entity Configuration](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/README.md)
- [LINQ Query Syntax](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/README.md)
- [Transaction Patterns](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/README.md)

**NuGet Package:**
- [SQLiteXM on NuGet](https://www.nuget.org/packages/SQLiteXM/)

**Questions or Issues?**
- [GitHub Issues](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/issues)
- [Discussions](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/discussions)

---

## 🏗️ Building All Samples

To build all samples at once:

```bash
# From solution root
dotnet build SQLiteXM.sln -c Release
```

To build QueryGalleryDemo for distribution:

```powershell
# From solution root
.\Build-QueryGalleryDemo.ps1
```

This creates a ready-to-distribute ZIP in `QueryGalleryDemo-Distribution/`.

---

**Ready to integrate SQLiteXM into your app?** Start with the [Quick Start Guide](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI#-quick-start-2-minutes) in the main README.
