# Implementing RabbitMQ - Documentation

**Document Date: 14th June 2026**



---

## Table of Contents

1. What is RabbitMQ and Why Should I Use It?
2. RabbitMQ Architecture Overview
3. Setting Up RabbitMQ with Docker
4. Understanding RabbitMQ.Client vs MassTransit
5. Publisher and Consumer Implementation
6. Message Queue Priority System (Low, Medium, High)
7. Dead Letter Queue (DLQ) Implementation
8. Best Practices and Code Organization
9. Summary and Next Steps

---

## 1. What is RabbitMQ and Why Should I Use It?

### Understanding RabbitMQ

When I first started working with distributed systems, I realized that having my services communicate directly with each other created a lot of coupling and made the system fragile. That's when I discovered RabbitMQ.

RabbitMQ is a **message broker** - We can think of it as a post office for my applications. Just like a post office takes letters from senders and delivers them to receivers, RabbitMQ takes messages from producers and ensures they reach consumers, even if the consumer isn't ready at that moment.

Instead of Service A directly calling Service B (which requires B to be online and available), Service A sends a message to RabbitMQ saying "Hey, I need this done," and then continues with its work. When Service B is ready, it picks up the message and processes it. This decoupling is powerful.

### Why I Chose RabbitMQ for My ReportSystem

In my ReportSystem project, I have three independent services:
- An ASP.NET Core API that manages report jobs
- A Python service that generates SQL based on user prompts
- A consumer that executes the SQL and returns results

Without RabbitMQ, these would need to know about each other's endpoints and handle timeouts and failures. With RabbitMQ, I've achieved:

**Asynchronous Processing**: When my API receives a report generation request, I immediately queue it and return a response to the user. The actual SQL generation happens in the background through the Python service.

**Decoupling**: My Python service doesn't care about my API's implementation. As long as we both understand the message format, we can evolve independently.

**Reliability**: If my Python service crashes, the message stays in the queue. When the service comes back online, it processes the message as if nothing happened.

**Scalability**: I can run multiple consumers of the same message type, and RabbitMQ will distribute the load. If I need to process reports faster, I simply start more Python workers.

---

## 2. RabbitMQ Architecture Overview

### The Core Components I Work With

When I design a messaging system with RabbitMQ, I need to understand these key components:

| Component | What It Does | In My ReportSystem |
|-----------|-------------|------------------|
| **Producer** | Application that sends messages | ASP.NET API that creates report jobs |
| **Exchange** | Entry point for messages; decides where they go | `reports_exchange` that routes different message types |
| **Queue** | Storage for messages waiting to be processed | `sql_generation_queue`, `execution_queue` |
| **Consumer** | Application that receives and processes messages | Python service, Execution consumer |
| **Binding** | Connection between exchange and queue | Maps message types to appropriate queues |

### How Messages Flow Through My System

I've designed my ReportSystem with this flow in mind:

```
User creates report request
        ↓
API creates ReportJob in database
        ↓
API publishes GenerateSqlCommand to RabbitMQ
        ↓
Python service consumes GenerateSqlCommand
        ↓
Python generates SQL and publishes SqlGeneratedEvent
        ↓
API consumes SqlGeneratedEvent and updates job status
        ↓
API publishes ExecuteReportCommand
        ↓
Consumer consumes ExecuteReportCommand
        ↓
Consumer executes SQL and publishes ReportExecutionCompletedEvent
        ↓
API shows user the results
```

This separation lets each component focus on its responsibility.

---

## 3. Setting Up RabbitMQ with Docker

### My Docker Compose Configuration

I use Docker to run RabbitMQ because it makes development consistent across my team. Here's what I have:

```yaml
version: '3.8'

services:
  rabbitmq:
    image: rabbitmq:3-management
    container_name: rabbitmq
    ports:
      - "5672:5672"      # AMQP protocol port - where clients connect
      - "15672:15672"    # Management UI - for monitoring and debugging
    environment:
      RABBITMQ_DEFAULT_USER: admin
      RABBITMQ_DEFAULT_PASS: StrongPass123
    restart: unless-stopped
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq

volumes:
  rabbitmq_data:
```

### Understanding My Configuration

When I run this setup, here's what happens:

- **image: rabbitmq:3-management** - I'm using RabbitMQ version 3 with the management plugin included. This gives me a nice web UI for monitoring.

- **ports: 5672** - This is the AMQP port. My applications connect here to send and receive messages.

- **ports: 15672** - This is my management console. I can open http://localhost:15672 in my browser and log in with admin/StrongPass123 to see queues, monitor messages, and debug issues.

- **volumes: rabbitmq_data** - I mount a volume so my queue data persists even if the container restarts. This is crucial for production.

### Starting RabbitMQ

```bash
docker-compose up -d
```

Once I run this, RabbitMQ is available at localhost:5672 for my applications and localhost:15672 for my management interface.

---

## 4. Understanding RabbitMQ.Client vs MassTransit

### The Challenge I Faced

I initially wanted to use MassTransit for everything because it's a powerful abstraction layer. However, I encountered a problem: **MassTransit is only for .NET**. My Python service needed to communicate with the same RabbitMQ broker.

### Comparison: When I Use Each

| Aspect | RabbitMQ.Client | MassTransit |
|--------|-----------------|-----------|
| **Level of Abstraction** | Low-level; direct AMQP protocol | High-level; convention-based |
| **Learning Curve** | Steeper; I write more boilerplate | Gentler; more magic happens automatically |
| **Language Support** | Multiple languages can use it | Only .NET ecosystem |
| **Message Serialization** | I handle it manually | Automatic |
| **Broker Support** | RabbitMQ only | Many brokers (RabbitMQ, Azure Service Bus, AWS SQS, etc.) |
| **Retry Policies** | I implement them myself | Built-in, very configurable |
| **Code Length** | More verbose | Much cleaner |

### My Solution

I made a practical decision:
- For my **C# API and consumers**, I use **MassTransit** because its conventions save me time and provide excellent features out of the box.
- For my **Python service**, I use the raw **RabbitMQ client library** (pika) because it's the standard in Python and works perfectly with AMQP.

Both speak the same AMQP protocol, so they communicate seamlessly.

---

## 5. Publisher and Consumer Implementation

### Defining My Message Contracts

I start by defining the messages my system will exchange. I use C# records for this because they're immutable and perfect for representing data.

#### ExecuteReportCommand.cs

```csharp
namespace ReportSystem.Api.Contracts.Commands;

public record ExecuteReportCommand
{
    public Guid JobId { get; init; }
    public string? GeneratedSql { get; init; }
    public int Priority { get; init; }
}
```

#### GenerateSqlCommand.cs

```csharp
namespace ReportSystem.Api.Contracts.Commands;

public record GenerateSqlCommand
{
    public Guid JobId { get; init; }
    public required string ReportName { get; init; }
    public required string Prompt { get; init; }
    public string? InputDataJson { get; init; }
    public int Priority { get; init; }
}
```

#### SqlGeneratedEvent.cs

```csharp
namespace ReportSystem.Api.Contracts.Events;

public record SqlGeneratedEvent
{
    public Guid JobId { get; init; }
    public string? GeneratedSql { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### Publishing Messages

When I receive a request to create a report, here's how I publish the message:

```csharp
[HttpPost("create")]
public async Task<ActionResult> CreateReportJob(
    [FromBody] CreateReportJobDto dto,
    [FromServices] IPublishEndpoint publishEndpoint)
{
    // Step 1: Save the job to my database
    var job = new ReportJob
    {
        Id = Guid.NewGuid(),
        ReportName = dto.ReportName,
        Prompt = dto.Prompt,
        Status = JobStatus.WaitingForSqlGeneration,
        Progress = 10,
        Priority = dto.Priority ?? 0,  // I use this for queue priority
        CreatedAt = DateTime.UtcNow
    };
    
    await _jobRepository.AddAsync(job);
    
    // Step 2: Publish the command to RabbitMQ
    await publishEndpoint.Publish(new GenerateSqlCommand
    {
        JobId = job.Id,
        ReportName = dto.ReportName,
        Prompt = dto.Prompt,
        InputDataJson = dto.InputDataJson,
        Priority = job.Priority
    });
    
    return Ok(new { jobId = job.Id, message = "Report job created" });
}
```

I like this pattern because it separates my API logic from messaging logic. The responsibility is clear: persist the data, then notify interested parties.

### Consuming Messages

When my consumer receives a message, here's how I process it:

```csharp
namespace ReportSystem.Api.RabbitMQ.Consumers;

public class ExecuteReportCommandConsumer : IConsumer<ExecuteReportCommand>
{
    private readonly IReportJobRepository _repository;
    private readonly ILogger<ExecuteReportCommandConsumer> _logger;
    private readonly string _connectionString;

    public ExecuteReportCommandConsumer(
        IReportJobRepository repository,
        ILogger<ExecuteReportCommandConsumer> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found");
    }

    public async Task Consume(ConsumeContext<ExecuteReportCommand> context)
    {
        var jobId = context.Message.JobId;
        _logger.LogInformation("Processing ExecuteReportCommand for JobId: {JobId}", jobId);

        try
        {
            // Get the job from database
            var job = await _repository.GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("Job not found: {JobId}", jobId);
                return;
            }

            // Update status to executing
            job.Status = JobStatus.Executing;
            job.Progress = 80;
            job.CurrentStep = "Executing SQL";
            job.StartedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);

            // Execute the SQL
            var sqlToExecute = context.Message.GeneratedSql ?? job.GeneratedSql;
            if (string.IsNullOrEmpty(sqlToExecute))
            {
                throw new InvalidOperationException("No SQL to execute");
            }

            var result = await ExecuteSqlAsync(sqlToExecute);

            // Mark as completed
            job.Status = JobStatus.Completed;
            job.Progress = 100;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);

            // Publish completion event so others know
            await context.Publish(new ReportExecutionCompletedEvent
            {
                JobId = jobId,
                Success = true,
                ResultJson = result,
                ExecutionTimeMs = (long)(DateTime.UtcNow - job.StartedAt!.Value).TotalMilliseconds
            });

            _logger.LogInformation("Job {JobId} completed successfully", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing report job: {JobId}", jobId);
            
            // Publish failure event
            await context.Publish(new ReportExecutionCompletedEvent
            {
                JobId = jobId,
                Success = false,
                ErrorMessage = ex.Message
            });
            
            throw;  // Let MassTransit handle retry logic
        }
    }

    private async Task<string> ExecuteSqlAsync(string sql)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        var results = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return JsonSerializer.Serialize(results);
    }
}
```

I appreciate this pattern because:
- MassTransit automatically handles retries if I throw an exception
- I get structured logging
- I can publish events to notify other parts of the system
- The ConsumeContext gives me access to useful metadata

### Configuring MassTransit in My Application

Here's how I set everything up in my Program.cs:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add my business services
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IReportJobRepository, ReportJobRepository>();
builder.Services.AddScoped<IReportJobService, ReportJobService>();
builder.Services.AddScoped<IReportJobPublisher, ReportJobPublisher>();

// Configure RabbitMQ with MassTransit
var rabbitMqSettings = builder.Configuration.GetSection("RabbitMQ");
builder.Services.AddMassTransit(x =>
{
    // Register my consumers
    x.AddConsumer<ExecuteReportCommandConsumer>();
    x.AddConsumer<SqlGeneratedEventConsumer>();

    // Configure RabbitMQ transport
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitMqSettings["Host"], "/", h =>
        {
            h.Username(rabbitMqSettings["Username"] ?? "admin");
            h.Password(rabbitMqSettings["Password"] ?? "StrongPass123");
        });

        // Configure the endpoint for my consumer
        cfg.ReceiveEndpoint("execute-report-queue", e =>
        {
            e.PrefetchCount = 10;  // Process up to 10 messages concurrently
            e.ConfigureConsumer<ExecuteReportCommandConsumer>(context);
        });

        cfg.ReceiveEndpoint("sql-generated-queue", e =>
        {
            e.PrefetchCount = 5;
            e.ConfigureConsumer<SqlGeneratedEventConsumer>(context);
        });
    });
});

var app = builder.Build();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
```

### My appsettings.json Configuration

```json
{
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "admin",
    "Password": "StrongPass123"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Username=postgres;Password=pass123;Database=reports_db"
  }
}
```

---

## 6. Message Queue Priority System (Low, Medium, High)

### Why I Need Queue Priorities

In my ReportSystem, not all reports are equally urgent. Some reports are:
- **High Priority**: Executive dashboards needed for immediate decisions
- **Medium Priority**: Regular operational reports
- **Low Priority**: Historical analysis or backups

Without priorities, a low-priority report might queue-jump a critical executive report, which would be a problem. This is where RabbitMQ's priority queue feature comes in handy.

### How I Implement Priority Queues

RabbitMQ supports priority queues, but I need to set them up correctly. Here's my approach:

#### Step 1: Define Priority Levels

I create an enum for clarity:

```csharp
namespace ReportSystem.Api.Enums;

public enum MessagePriority
{
    Low = 0,
    Medium = 5,
    High = 10
}
```

#### Step 2: Create Priority-Enabled Queues

In my RabbitMQ setup, I declare queues with the `x-max-priority` argument:

```csharp
namespace ReportSystem.Api.RabbitMQ.Configuration;

public static class RabbitMqPriorityConfiguration
{
    public static void ConfigurePriorityQueues(this IRabbitMqBusFactoryConfigurator cfg)
    {
        // Configure execute-report queue with priorities
        cfg.ReceiveEndpoint("execute-report-queue", e =>
        {
            // Enable priorities: 0 (lowest) to 10 (highest)
            e.SetQueueArgument("x-max-priority", 10);
            
            e.PrefetchCount = 10;
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            
            e.ConfigureConsumer<ExecuteReportCommandConsumer>(context);
        });

        // Configure sql-generation queue with priorities
        cfg.ReceiveEndpoint("sql-generation-queue", e =>
        {
            e.SetQueueArgument("x-max-priority", 10);
            
            e.PrefetchCount = 5;
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            
            e.ConfigureConsumer<SqlGenerationConsumer>(context);
        });
    }
}
```

#### Step 3: Set Priority When Publishing

When I publish a message, I set the priority:

```csharp
public async Task PublishGenerateSqlCommandAsync(
    Guid jobId, 
    string reportName, 
    string prompt, 
    string? inputData, 
    MessagePriority priority)
{
    try
    {
        await _publishEndpoint.Publish(
            new GenerateSqlCommand
            {
                JobId = jobId,
                ReportName = reportName,
                Prompt = prompt,
                InputDataJson = inputData,
                Priority = (int)priority
            },
            context => 
            {
                // Set the message priority in RabbitMQ
                context.SetPriority((byte)priority);
            });
        
        _logger.LogInformation(
            "Published GenerateSqlCommand for JobId: {JobId} with priority: {Priority}", 
            jobId, 
            priority);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error publishing GenerateSqlCommand");
        throw;
    }
}
```

#### Step 4: Use Priorities in My API

When creating a report job, I accept priority from the client:

```csharp
[HttpPost("create")]
public async Task<ActionResult> CreateReportJob(
    [FromBody] CreateReportJobDto dto)
{
    var jobId = Guid.NewGuid();
    
    // Determine priority based on user input
    var priority = (MessagePriority)(dto.Priority ?? 0);
    
    // Validate priority
    if (!Enum.IsDefined(typeof(MessagePriority), priority))
    {
        return BadRequest("Invalid priority. Use 0 (Low), 5 (Medium), or 10 (High)");
    }

    var job = new ReportJob
    {
        Id = jobId,
        ReportName = dto.ReportName,
        Prompt = dto.Prompt,
        Status = JobStatus.WaitingForSqlGeneration,
        Priority = (int)priority,
        CreatedAt = DateTime.UtcNow
    };
    
    await _jobRepository.AddAsync(job);
    
    // Publish with priority
    await _reportJobPublisher.PublishGenerateSqlCommandAsync(
        jobId,
        dto.ReportName,
        dto.Prompt,
        dto.InputDataJson,
        priority);
    
    return Ok(new 
    { 
        jobId = job.Id, 
        message = "Report job created",
        priority = priority.ToString()
    });
}
```

### How Priority Works in RabbitMQ

Here's what I understand about how RabbitMQ handles priorities:

1. **Queue Declaration**: When I set `x-max-priority: 10`, RabbitMQ allocates memory for 11 priority levels (0-10).

2. **Message Ordering**: When multiple consumers are available, RabbitMQ sends higher-priority messages first. A high-priority message (10) will be consumed before a low-priority message (0).

3. **Within Queue Order**: Messages of the same priority are processed in FIFO order.

4. **Consumer Impact**: If a consumer is already processing a low-priority message, it won't be interrupted. But the next message fetched will be high-priority if one exists.

### Important Considerations with Priority Queues

When I implemented this, I learned some important things:

**Performance Trade-off**: Priority queues use more memory than regular queues because RabbitMQ has to maintain separate lists for each priority level. So I only use them where priorities truly matter.

**No Preemption**: RabbitMQ doesn't interrupt a consumer that's already processing a message. The priority only affects which message the consumer fetches next.

**Fair Distribution**: Even with priorities, I set PrefetchCount carefully. With `PrefetchCount = 10`, a fast consumer could fetch all high-priority messages and starve low-priority ones. I might use `PrefetchCount = 1` for critical systems.

**Default Priority**: Messages without an explicit priority default to 0 (lowest). I always set the priority explicitly to be safe.

---

## 7. Dead Letter Queue (DLQ) Implementation

### Why I Need a Dead Letter Queue

I learned this the hard way: sometimes messages can't be processed. Maybe:
- The message format is invalid
- A service is having a bad day and keeps failing
- The database is temporarily unavailable
- The message references a job that doesn't exist

Without a DLQ, I'd either:
- Lose the message (bad - data loss)
- Keep retrying forever (bad - resource waste and delays)
- Manually handle failed messages (bad - not scalable)

A DLQ solves this by automatically moving messages that fail too many times to a separate queue where I can inspect and handle them.

### Configuring DLQ in My System

Here's how I set up the DLQ for my ExecuteReportCommand consumer:

```csharp
cfg.ReceiveEndpoint("execute-report-queue", e =>
{
    // Enable priority
    e.SetQueueArgument("x-max-priority", 10);
    
    // Retry policy: retry 3 times with 5-second intervals
    e.UseMessageRetry(r => 
    {
        r.Interval(3, TimeSpan.FromSeconds(5));
    });
    
    // Configure Dead Letter Exchange
    var dlx = new FanoutExchange
    {
        Name = "execute-report-dlx",
        Durable = true,
        AutoDelete = false
    };
    
    // Bind DLQ
    e.Bind(dlx);
    e.BindDeadLetterQueue("execute-report-dlq");
    
    e.PrefetchCount = 10;
    e.ConfigureConsumer<ExecuteReportCommandConsumer>(context);
});
```

Let me explain what each part does:

**Retry Policy**: When an exception is thrown in my consumer, MassTransit catches it. With `r.Interval(3, TimeSpan.FromSeconds(5))`, it waits 5 seconds and tries again. After 3 failed attempts, the message is moved to the DLQ.

**Dead Letter Exchange**: This is an exchange (routing point) that receives messages that couldn't be processed.

**BindDeadLetterQueue**: This creates a queue called `execute-report-dlq` and connects it to the DLX.

### Monitoring and Debugging with DLQ

I can view failed messages in the RabbitMQ management console at http://localhost:15672:

1. Navigate to the "Queues" tab
2. Look for `execute-report-dlq`
3. Click on it to see the failed messages
4. Click on a message to inspect its content

Here's what I look for:
- **Headers**: Exception information from the consumer
- **Payload**: The actual message that failed
- **Timestamp**: When it failed

### Handling DLQ Messages

Once I've debugged and fixed the issue, I can:

**Option 1: Manually Requeue**: I use the management UI to republish the message back to the original queue.

**Option 2: Automated Monitoring**: I create a separate consumer for DLQ messages:

```csharp
public class DeadLetterQueueMonitor : IConsumer<Fault<ExecuteReportCommand>>
{
    private readonly ILogger<DeadLetterQueueMonitor> _logger;
    private readonly IEmailService _emailService;

    public DeadLetterQueueMonitor(
        ILogger<DeadLetterQueueMonitor> logger,
        IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public async Task Consume(ConsumeContext<Fault<ExecuteReportCommand>> context)
    {
        var jobId = context.Message.Message.JobId;
        var exception = context.Message.Exceptions.FirstOrDefault();
        
        _logger.LogError(
            "Message failed and moved to DLQ - JobId: {JobId}, Exception: {Exception}",
            jobId,
            exception?.Message);
        
        // Send alert email to operations team
        await _emailService.SendAsync(
            to: "ops@company.com",
            subject: "DLQ Alert: Report Job Failed",
            body: $"Job {jobId} failed after 3 retries. Error: {exception?.Message}");
    }
}
```

Then I register this in my configuration:

```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ExecuteReportCommandConsumer>();
    x.AddConsumer<DeadLetterQueueMonitor>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        // ... queue configuration ...
        
        cfg.ReceiveEndpoint("dead-letter-monitoring", e =>
        {
            e.ConfigureConsumer<DeadLetterQueueMonitor>(context);
        });
    });
});
```

---

## 8. Best Practices and Code Organization

### Organizing My Code Structure

As my project grew, I realized I needed clear organization. Here's the structure I use now:

```
ReportSystem.Api/
├── Contracts/                          # Message definitions
│   ├── Commands/
│   │   ├── GenerateSqlCommand.cs
│   │   └── ExecuteReportCommand.cs
│   ├── Events/
│   │   ├── SqlGeneratedEvent.cs
│   │   ├── ReportExecutionCompletedEvent.cs
│   │   └── ReportJobFailedEvent.cs
│   └── Faults/
│       └── MessageProcessingFault.cs
│
├── RabbitMQ/                           # Messaging infrastructure
│   ├── Consumers/
│   │   ├── IMessageConsumer.cs
│   │   ├── ExecuteReportCommandConsumer.cs
│   │   ├── SqlGeneratedEventConsumer.cs
│   │   └── DeadLetterQueueMonitor.cs
│   ├── Publishers/
│   │   ├── IReportJobPublisher.cs
│   │   └── ReportJobPublisher.cs
│   ├── Configuration/
│   │   ├── RabbitMqConfiguration.cs
│   │   ├── QueueConfiguration.cs
│   │   └── PriorityQueueConfiguration.cs
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs
│
├── Services/                           # Business logic
│   ├── IReportJobService.cs
│   └── ReportJobService.cs
│
├── Controllers/
│   └── ReportController.cs
│
├── Entities/
│   ├── ReportJob.cs
│   └── ReportResult.cs
│
└── Program.cs
```

### Creating a Publisher Abstraction

I don't let my controllers directly use MassTransit. Instead, I create an abstraction:

```csharp
namespace ReportSystem.Api.RabbitMQ.Publishers;

public interface IReportJobPublisher
{
    Task PublishGenerateSqlCommandAsync(
        Guid jobId,
        string reportName,
        string prompt,
        string? inputData,
        MessagePriority priority = MessagePriority.Medium);
    
    Task PublishExecuteReportCommandAsync(
        Guid jobId,
        string generatedSql,
        MessagePriority priority = MessagePriority.Medium);
    
    Task PublishReportJobCancelledAsync(Guid jobId);
}

public class ReportJobPublisher : IReportJobPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ReportJobPublisher> _logger;

    public ReportJobPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<ReportJobPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishGenerateSqlCommandAsync(
        Guid jobId,
        string reportName,
        string prompt,
        string? inputData,
        MessagePriority priority = MessagePriority.Medium)
    {
        try
        {
            await _publishEndpoint.Publish(
                new GenerateSqlCommand
                {
                    JobId = jobId,
                    ReportName = reportName,
                    Prompt = prompt,
                    InputDataJson = inputData,
                    Priority = (int)priority
                },
                context =>
                {
                    context.SetPriority((byte)priority);
                });
            
            _logger.LogInformation(
                "GenerateSqlCommand published for JobId: {JobId}, Priority: {Priority}",
                jobId,
                priority);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish GenerateSqlCommand for JobId: {JobId}",
                jobId);
            throw;
        }
    }

    public async Task PublishExecuteReportCommandAsync(
        Guid jobId,
        string generatedSql,
        MessagePriority priority = MessagePriority.Medium)
    {
        try
        {
            await _publishEndpoint.Publish(
                new ExecuteReportCommand
                {
                    JobId = jobId,
                    GeneratedSql = generatedSql,
                    Priority = (int)priority
                },
                context =>
                {
                    context.SetPriority((byte)priority);
                });
            
            _logger.LogInformation(
                "ExecuteReportCommand published for JobId: {JobId}, Priority: {Priority}",
                jobId,
                priority);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish ExecuteReportCommand for JobId: {JobId}",
                jobId);
            throw;
        }
    }

    public async Task PublishReportJobCancelledAsync(Guid jobId)
    {
        try
        {
            await _publishEndpoint.Publish(new ReportJobCancelledEvent
            {
                JobId = jobId,
                CancelledAt = DateTime.UtcNow
            });
            
            _logger.LogInformation("ReportJobCancelledEvent published for JobId: {JobId}", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish ReportJobCancelledEvent for JobId: {JobId}",
                jobId);
            throw;
        }
    }
}
```

I like this approach because:
- My controller doesn't know about MassTransit
- I can change messaging implementation without touching controllers
- Logging and error handling is centralized
- I can add features like circuit breakers or caching easily

### Refactoring Program.cs for Cleanliness

I moved all RabbitMQ configuration to an extension method:

```csharp
namespace ReportSystem.Api.RabbitMQ.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddReportSystemMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register publisher
        services.AddScoped<IReportJobPublisher, ReportJobPublisher>();

        // Register MassTransit
        var rabbitMqSettings = configuration.GetSection("RabbitMQ");
        
        services.AddMassTransit(x =>
        {
            // Add consumers
            x.AddConsumer<ExecuteReportCommandConsumer>();
            x.AddConsumer<SqlGeneratedEventConsumer>();
            x.AddConsumer<DeadLetterQueueMonitor>();

            // Configure RabbitMQ
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(
                    rabbitMqSettings["Host"],
                    "/",
                    h =>
                    {
                        h.Username(rabbitMqSettings["Username"] ?? "admin");
                        h.Password(rabbitMqSettings["Password"] ?? "StrongPass123");
                    });

                // Configure endpoints
                ConfigureExecuteReportEndpoint(cfg, context);
                ConfigureSqlGenerationEndpoint(cfg, context);
                ConfigureDeadLetterEndpoint(cfg, context);
            });
        });
    }

    private static void ConfigureExecuteReportEndpoint(
        IRabbitMqBusFactoryConfigurator cfg,
        IBusRegistrationContext context)
    {
        cfg.ReceiveEndpoint("execute-report-queue", e =>
        {
            e.SetQueueArgument("x-max-priority", 10);
            e.PrefetchCount = 10;
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            e.BindDeadLetterQueue("execute-report-dlq");
            e.ConfigureConsumer<ExecuteReportCommandConsumer>(context);
        });
    }

    private static void ConfigureSqlGenerationEndpoint(
        IRabbitMqBusFactoryConfigurator cfg,
        IBusRegistrationContext context)
    {
        cfg.ReceiveEndpoint("sql-generation-queue", e =>
        {
            e.SetQueueArgument("x-max-priority", 10);
            e.PrefetchCount = 5;
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            e.BindDeadLetterQueue("sql-generation-dlq");
            e.ConfigureConsumer<SqlGeneratedEventConsumer>(context);
        });
    }

    private static void ConfigureDeadLetterEndpoint(
        IRabbitMqBusFactoryConfigurator cfg,
        IBusRegistrationContext context)
    {
        cfg.ReceiveEndpoint("dead-letter-monitoring", e =>
        {
            e.PrefetchCount = 1;
            e.ConfigureConsumer<DeadLetterQueueMonitor>(context);
        });
    }
}
```

Now my Program.cs is clean:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IReportJobRepository, ReportJobRepository>();
builder.Services.AddScoped<IReportJobService, ReportJobService>();

// Add all messaging configuration in one line
builder.Services.AddReportSystemMessaging(builder.Configuration);

var app = builder.Build();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
```

### Error Handling Strategy

I've learned that good error handling in messaging is critical:

```csharp
public async Task Consume(ConsumeContext<ExecuteReportCommand> context)
{
    var jobId = context.Message.JobId;
    
    try
    {
        // Validate the message
        if (context.Message.JobId == Guid.Empty)
        {
            _logger.LogWarning("Received message with empty JobId");
            return;  // Don't retry - it's an invalid message
        }

        var job = await _repository.GetByIdAsync(jobId);
        if (job == null)
        {
            _logger.LogWarning("Job not found: {JobId}", jobId);
            return;  // Job doesn't exist, no point retrying
        }

        // Process the message
        await ProcessJobAsync(job);
    }
    catch (SqlException ex) when (ex.Message.Contains("timeout"))
    {
        _logger.LogWarning(ex, "Timeout executing SQL for job {JobId}", jobId);
        throw;  // Retry - it's temporary
    }
    catch (SqlException ex)
    {
        _logger.LogError(ex, "SQL error executing job {JobId}", jobId);
        throw;  // Retry - might be temporary
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error processing job {JobId}", jobId);
        throw;  // Let retry policy handle it
    }
}
```

The key insight: I only throw exceptions when I want MassTransit to retry. If it's a validation error or missing data, I just log and return.

---

## 9. Summary and Next Steps

### What I've Built

I've created a distributed report generation system using RabbitMQ where:

1. **My API** receives requests and publishes messages with priority levels (Low, Medium, High)
2. **My Python service** consumes SQL generation commands and publishes results
3. **My consumer** processes reports with full observability
4. **Priority queues** ensure critical reports are handled first
5. **Dead letter queues** catch and alert on failures
6. **Retry policies** handle temporary failures automatically

### The Architecture Benefits I Experience

- **Scalability**: I can spin up more consumers when demand increases
- **Reliability**: No message loss; failed messages are tracked and stored
- **Decoupling**: Services are independent and can evolve separately
- **Observability**: Every step is logged and visible in the RabbitMQ UI
- **Fairness**: Priority ensures critical work gets done first

### What I Would Do Next

If I were extending this system, I'd consider:

1. **Circuit Breaker Pattern**: Detect when services are struggling and pause publishing
2. **Correlation IDs**: Track a user's request across all services
3. **Message Compression**: For large payloads, compress before sending
4. **Saga Pattern**: Handle complex workflows that span multiple services
5. **Metrics Collection**: Integration with Prometheus or similar for monitoring
6. **Consumer Groups**: Load balance consumers across multiple instances
7. **Message TTL**: Auto-expire messages that are too old to be useful

### Key Takeaways

Working with RabbitMQ taught me:

- **Asynchronous messaging** is the backbone of scalable distributed systems
- **Priorities** matter in real systems where not all work is equal
- **Dead letter queues** are not optional - they're essential
- **Abstractions** (like IReportJobPublisher) prevent tight coupling
- **Logging and observability** are as important as the code itself

I'm confident this approach will serve the ReportSystem well as it grows, and the patterns I've established can extend to other parts of the application.

---

## Appendix: Quick Reference

### Queue Configuration Quick Reference

```csharp
// Basic queue with priority
cfg.ReceiveEndpoint("my-queue", e =>
{
    e.SetQueueArgument("x-max-priority", 10);
    e.PrefetchCount = 10;
    e.ConfigureConsumer<MyConsumer>(context);
});

// Queue with retry and DLQ
cfg.ReceiveEndpoint("my-queue", e =>
{
    e.SetQueueArgument("x-max-priority", 10);
    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    e.BindDeadLetterQueue("my-queue-dlq");
    e.ConfigureConsumer<MyConsumer>(context);
});

// Publishing with priority
await publishEndpoint.Publish(
    new MyMessage { /* data */ },
    context => context.SetPriority((byte)MessagePriority.High));
```

### Message Priority Levels

| Level | Name | Value | Use Case |
|-------|------|-------|----------|
| 10 | High | 10 | Executive reports, critical operations |
| 5 | Medium | 5 | Regular operational reports |
| 0 | Low | 0 | Backups, historical analysis, batch jobs |

### Docker Commands I Use

```bash
# Start RabbitMQ
docker-compose up -d

# Stop RabbitMQ
docker-compose down

# View logs
docker-compose logs -f rabbitmq

# Access management UI
http://localhost:15672

# Clear all data and restart
docker-compose down -v
docker-compose up -d
```

---

**This documentation represents my journey implementing RabbitMQ in a production-ready distributed system. I hope it helps others avoid the mistakes I made and embrace the patterns that work.**

**Last Updated: June 2026**
