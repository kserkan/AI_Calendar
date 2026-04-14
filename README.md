# ­ SmartCalendar

**SmartCalendar** is an intelligent calendar management application built with **ASP.NET Core 8.0** that combines event management, AI-powered recommendations, Google Calendar integration, and weather information all in one platform.

---

## ­ Project Overview

SmartCalendar is a full-stack web application designed to help users:
- **Manage events** with detailed information (title, date, time, location, description)
- **Get AI-powered recommendations** using Google's Gemini API
- **Sync with Google Calendar** for seamless event management
- **Receive reminders** via email notifications
- **View weather information** for event planning
- **Track holidays** from Turkey's official calendar
- **Organize events** with custom tags

---

## ­Architecture

### Technology Stack

| Layer | Technology |
|-------|-----------|
| **Backend Framework** | ASP.NET Core 8.0 |
| **Database** | MySQL 8.0 |
| **ORM** | Entity Framework Core |
| **Authentication** | ASP.NET Core Identity + JWT + Google OAuth2 |
| **API Documentation** | Swagger/OpenAPI |
| **Containerization** | Docker & Docker Compose |
| **AI Services** | Google Gemini API |
| **Email** | SMTP (Gmail) |

### Project Structure

```
SmartCalendar/
 Controllers/              # MVC Controllers & API Endpoints
    AccountController.cs         # User authentication & registration
    CalendarController.cs        # Event CRUD operations
    AIController.cs              # AI recommendations & parsing
    DashboardController.cs       # User dashboard
    HomeController.cs            # Main page
    ProfileController.cs         # User profile management
    EventParserController.cs     # Natural language event parsing
    WeatherController.cs         # Weather information

 Models/                  # Data Models & DTOs
    User.cs                      # User model (extends IdentityUser)
    Event.cs                     # Event model with reminders
    EventDto.cs                  # DTO for event transfer
    Tag.cs                       # Event tags
    EventTag.cs                  # Many-to-many relationship
    HolidayItem.cs               # Holiday model
    RecommendationModel.cs       # AI recommendations
    PromptRequest.cs             # AI request model
    Weather/                     # Weather-related models
ViewModels/                  # View-specific models
    
    Services/                # Business Logic & External Services
    GoogleCalendarService.cs     # Google Calendar API integration
    AIService.cs                 # Gemini AI integration
    WeatherService.cs            # Weather API integration
    HolidayService.cs            # Holiday information
    ReminderService.cs           # Background reminder service
    SmtpEmailService.cs          # Email notifications
    
Data/                    # Database Context & Migrations
    ApplicationDbContext.cs      # EF Core DbContext
     DesignTimeDbContextFactory.cs
    Migrations/                  # Database migrations
    Seed/                        # Initial database seed
    
    Views/                   # Razor Views
    wwwroot/                 # Static files (CSS, JS, images)
    Program.cs               # Application configuration & startup
    appsettings.json         # Configuration settings
    appsettings.Development.json
    Dockerfile               # Docker build configuration
    docker-compose.yml       # Multi-container orchestration
```

---

## ­şöæ Key Features

### 1. **User Authentication & Authorization**
- Local registration and login
- Google OAuth2 authentication
- JWT token-based API authentication
- ASP.NET Core Identity integration
- Secure password management

### 2. **Event Management**
- Create, read, update, delete events
- Event properties: title, start/end date, location, description
- Event reminders (configurable minutes before)
- Google Calendar synchronization
- Event tagging and categorization

### 3. **AI-Powered Features**
- **Event Recommendations**: Gemini AI analyzes user's event history
- **Natural Language Parsing**: Convert text to structured events
- Intelligent event suggestion based on patterns

### 4. **Calendar Integration**
- Google Calendar OAuth2 sync
- Import/export events
- View Turkish holidays from official calendar
- Real-time event synchronization

### 5. **Notifications & Reminders**
- Background reminder service (hosted service)
- Email notifications via SMTP (Gmail)
- Configurable reminder timing
- User preference management

### 6. **Additional Features**
- Weather information for event planning
- Dark mode support (stored in user profile)
- Holiday calendar (Turkey)
- User profile management
- Responsive dashboard

---

## ­şøá´©Å Technology Details

### Authentication Flow

```
User Login/Register
    Ôåô
ASP.NET Core Identity
    Ôö£ÔöÇÔåÆ Cookie Authentication (Web browsers)
    Ôö£ÔöÇÔåÆ JWT Authentication (Mobile/API clients)
    ÔööÔöÇÔåÆ Google OAuth2 (SSO)
    Ôåô
Session Established
    Ôö£ÔöÇÔåÆ Claims-based authorization
    ÔööÔöÇÔåÆ Role-based access control
```

### Data Models Overview

**User Model:**
```csharp
public class User : IdentityUser
{
    public string FullName { get; set; }
    public bool ReceiveReminders { get; set; }
    public ICollection<Event> Events { get; set; }
}
```

**Event Model:**
```csharp
public class Event
{
    public int Id { get; set; }
    public string Title { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string UserId { get; set; }
    public int? ReminderMinutesBefore { get; set; }
    public bool ReminderSent { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? GoogleEventId { get; set; }
    public ICollection<Tag> Tags { get; set; }
}
```

---

## ­şÜÇ Getting Started

### Prerequisites

- **.NET 8.0 SDK** or later
- **MySQL 8.0** server
- **Docker & Docker Compose** (for containerized deployment)
- Google OAuth2 credentials (for authentication)
- Gemini API key (for AI features)
- Gmail SMTP credentials (for email notifications)

### Local Development Setup

#### 1. Clone the Repository
```bash
git clone https://github.com/yourusername/SmartCalendar.git
cd SmartCalendar/SmartCalendar
```

#### 2. Configure Application Settings

Update `appsettings.Development.json` with your credentials:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=calendar_db;User=root;Password=your_password;"
  },
  "GoogleAuth": {
    "ClientId": "your_google_client_id",
    "ClientSecret": "your_google_client_secret",
    "RedirectUri": "https://localhost:7189/signin-google"
  },
  "SmtpSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "User": "your_email@gmail.com",
    "Pass": "your_app_password"
  },
  "Gemini": {
    "ApiKey": "your_gemini_api_key"
  },
  "Jwt": {
    "Key": "your_jwt_secret_key",
    "ExpireMinutes": 60
  }
}
```

#### 3. Create Database & Run Migrations
```bash
dotnet ef database update
```

#### 4. Run the Application
```bash
dotnet run
```

The application will be available at `https://localhost:7189`

### Docker Deployment

#### 1. Build and Run with Docker Compose
```bash
docker-compose up -d
```

This will:
- Create MySQL database container
- Build and run the ASP.NET Core API on port 5111
- Initialize database with migrations and seed data

#### 2. Access the Application
- Web UI: `http://localhost:5111`
- Swagger API: `http://localhost:5111/swagger`
- MySQL: `localhost:3306`

#### 3. Stop Services
```bash
docker-compose down
```

---

## ­şôí API Endpoints

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/Account/Register` | User registration |
| POST | `/Account/Login` | User login |
| GET | `/Account/Logout` | Logout |
| POST | `/Account/GoogleLogin` | Google OAuth2 login |

### Calendar Events
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/Calendar/Events` | Get user's events |
| POST | `/Calendar/CreateEvent` | Create new event |
| PUT | `/Calendar/UpdateEvent/{id}` | Update event |
| DELETE | `/Calendar/DeleteEvent/{id}` | Delete event |
| GET | `/Calendar/GetGoogleEvents` | Sync Google Calendar |

### AI Features
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/AI/GetSuggestion` | Get AI event recommendations |
| POST | `/AI/ParseEvent` | Parse natural language text to event |

### Dashboard
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/Dashboard` | User dashboard |
| GET | `/Dashboard/MonthEvents` | Get month events |

### Weather
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/Weather` | Get weather information |

### Profile
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/Profile` | Get user profile |
| POST | `/Profile/UpdateProfile` | Update profile |

---

## ­şöÉ Security Features

1. **Authentication**
   - Password hashing with PBKDF2
   - JWT tokens with expiration
   - Google OAuth2 integration
   - Secure cookie handling

2. **Authorization**
   - Role-based access control
   - Claim-based authorization
   - User-specific data isolation

3. **Data Protection**
   - Entity Framework Core parameterized queries (SQL injection prevention)
   - CSRF token validation
   - SameSite cookie policy
   - Secure HTTP headers

4. **API Security**
   - JWT bearer token validation
   - CORS policy configured for specific origins
   - Rate limiting via middleware

---

## ­şôè Database Schema

### Key Tables

**Users**
- AspNetUsers (Identity framework)
- FullName, ReceiveReminders fields

**Events**
- Id (Primary Key)
- Title, Description, Location
- StartDate, EndDate
- UserId (Foreign Key ÔåÆ AspNetUsers)
- ReminderMinutesBefore, ReminderSent
- GoogleEventId (for sync)

**Tags & EventTags**
- Many-to-many relationship
- Event categorization

**Migrations**
- 12+ migrations for schema evolution
- Support for dark mode, reminders, descriptions, Google integration

---

## ­şÄ¿ Frontend

### Views
- **Home**: Landing page & authentication
- **Dashboard**: Calendar view & event overview
- **Profile**: User settings & preferences
- **Events**: Event creation & management forms

### Static Files
- CSS stylesheets in `wwwroot/css/`
- JavaScript in `wwwroot/js/`
- Images in `wwwroot/images/`

---

## ­şöä Background Services

### ReminderService (Hosted Service)
- Runs continuously in the background
- Checks for events with due reminders
- Sends email notifications 24/7
- Configurable via user preferences

---

## ­şôØ Environment Variables

### Development
```
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Server=localhost;Database=calendar_db;User=root;Password=root;
```

### Production (Docker)
```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=mysql;Database=calendar_db;User=root;Password=root;
```

---

## ­şÉø Troubleshooting

### Database Connection Issues
- Ensure MySQL is running on port 3306
- Check credentials in `appsettings.json`
- Verify database name is `calendar_db`

### Docker Login Issues
- Data protection keys need to be persisted
- Volume mapping: `./keys:/root/.aspnet/DataProtection-Keys`
- Ensure `keys/` directory exists with proper permissions

### Google OAuth Issues
- Verify Client ID and Secret in appsettings
- Check redirect URI matches configured URL
- Ensure Google APIs are enabled in Cloud Console

### Email Notification Issues
- Enable "Less secure app access" for Gmail
- Use app-specific password (not regular password)
- SMTP settings must match in appsettings

### AI Features Not Working
- Verify Gemini API key is valid
- Ensure API is enabled in Google Cloud
- Check network connectivity to Google APIs

---

## ­şÜÇ Deployment

### Azure App Service
```bash
# Publish to Azure
dotnet publish -c Release
# Use Azure DevOps or GitHub Actions for CI/CD
```

### Docker Hub
```bash
docker build -t yourusername/smartcalendar:latest .
docker push yourusername/smartcalendar:latest
```

### Kubernetes
```bash
kubectl apply -f k8s-deployment.yaml
```

---

## ­şôÜ Additional Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Entity Framework Core](https://docs.microsoft.com/ef/core)
- [Google Calendar API](https://developers.google.com/calendar)
- [Google Gemini API](https://ai.google.dev)
- [Docker Documentation](https://docs.docker.com)

---

## ­şæ¿ÔÇı­şÆ╗ Development Team

**Maintainer**: Kerim Serkan ┼Şahin
**Email**: kerimserkann@gmail.com

---

## ­şôä License

This project is proprietary software. All rights reserved.

---

## ­şñØ Contributing

For contributions, please:
1. Create a feature branch
2. Make your changes
3. Submit a pull request
4. Ensure all tests pass

---

## ­şôŞ Support

For issues, questions, or feature requests, please:
- Open an issue on GitHub
- Contact the development team
- Check existing documentation

---

**Last Updated**: January 2026  
**Project Version**: 1.0.0
