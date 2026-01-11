# Database Architecture and Issues Diagram

## System Architecture Overview

```mermaid
graph TB
    subgraph "UI Layer"
        A[ActiveRoundPageModel]
        B[RoundsPageModel]
        C[CoursesPageModel]
        D[RoundSummaryPageModel]
    end
    
    subgraph "Repository Layer"
        E[RoundRepository]
        F[PlayerRepository]
        G[CourseRepository]
        H[GolfSeedDataService]
    end
    
    subgraph "Database Layer"
        I[(SQLite Database)]
        J[Round Table]
        K[Hole Table]
        L[Player Table]
        M[Course Table]
        N[CourseHole Table]
    end
    
    subgraph "Synchronization"
        O[Init Semaphore]
        P[Write Semaphore]
        Q[Auto-save Debouncer]
    end
    
    A --> E
    B --> E
    C --> F
    C --> E
    D --> E
    E --> I
    F --> I
    G --> I
    H --> I
    H --> F
    H --> G
    E --> O
    E --> P
    A --> Q
    I --> J
    I --> K
    I --> L
    I --> M
    I --> N
    J --> K
    M --> N
```

## Identified Issues and Impact

### 1. Race Condition in Repository Initialization

```mermaid
graph LR
    subgraph "Current Problem"
        A[Thread 1] --> B[Check _hasBeenInitialized]
        C[Thread 2] --> B
        B --> D[Both pass check]
        D --> E[Both enter Init()]
        E --> F[Race condition]
        F --> G[Duplicate initialization]
    end
    
    subgraph "Solution"
        H[Thread 1] --> I[Semaphore Wait]
        C --> I
        I --> J[Check _hasBeenInitialized]
        J --> K{Already initialized?}
        K -->|Yes| L[Return]
        K -->|No| M[Initialize]
        M --> N[Set _hasBeenInitialized]
        N --> O[Release Semaphore]
    end
```

### 2. Write Semaphore Contention

```mermaid
graph TB
    subgraph "Current Problem - Global Lock"
        A[CreateNewRound] --> P[_writeSemaphore]
        B[SaveItemAsync] --> P
        C[SaveHoleAsync] --> P
        D[DeleteItemAsync] --> P
        P --> E[All operations blocked]
    end
    
    subgraph "Solution - Scoped Locks"
        F[CreateNewRound] --> P1[RoundWriteSemaphore]
        B[SaveItemAsync] --> P1
        C[SaveHoleAsync] --> P2[HoleWriteSemaphore]
        D[DeleteItemAsync] --> P1
        P1 --> G[Round operations]
        P2 --> H[Concurrent hole operations]
    end
```

### 3. Auto-save Race Condition

```mermaid
graph LR
    subgraph "Current Problem"
        A[Property Change] --> B[Cancel previous CTS]
        A --> C[Create new CTS]
        C --> D[Task.Delay 150ms]
        A --> E[Rapid changes]
        E --> B
        B --> F[Memory leak potential]
    end
    
    subgraph "Solution"
        G[Property Change] --> H[Debouncer]
        H --> I[Single delayed operation]
        I --> J[SaveHoleOnlyAsync]
        H --> K[Cancel previous]
        K --> L[No memory leaks]
    end
```

### 4. Database Integrity Check Issues

```mermaid
graph TB
    subgraph "Current Problem"
        A[CheckDatabaseIntegrity] --> B[SELECT name FROM sqlite_master]
        B --> C[Always returns true]
        C --> D[Meaningless check]
        D --> E[Corruption undetected]
    end
    
    subgraph "Solution"
        F[CheckDatabaseIntegrity] --> G[PRAGMA quick_check]
        G --> H{Corruption detected?}
        H -->|Yes| I[Backup and restore]
        H -->|No| J[Proceed normally]
    end
```

## Data Flow Analysis

### Round Creation Flow

```mermaid
sequenceDiagram
    participant U as UI
    participant A as ActiveRoundPageModel
    participant R as RoundRepository
    participant DB as SQLite Database
    
    U->>A: Create New Round
    A->>R: CreateNewRoundAsync()
    R->>R: Wait for write semaphore
    R->>R: Initialize database if needed
    R->>DB: Begin transaction
    R->>DB: INSERT INTO Round
    R->>DB: SELECT last_insert_rowid()
    R->>DB: INSERT INTO Hole (x18)
    R->>DB: Commit transaction
    R->>R: Release write semaphore
    R->>A: Return Round object
    A->>U: Update UI
```

### Hole Update Flow

```mermaid
sequenceDiagram
    participant U as UI
    participant A as ActiveRoundPageModel
    participant R as RoundRepository
    participant DB as SQLite Database
    
    U->>A: Update Hole Score
    A->>A: CurrentHole_PropertyChanged
    A->>A: Debounced save
    A->>R: SaveHoleAsync()
    R->>R: Wait for write semaphore
    R->>R: Check database integrity
    R->>DB: Begin transaction
    R->>DB: UPDATE Hole
    R->>DB: UPDATE Round (total score)
    R->>DB: Commit transaction
    R->>R: Release write semaphore
    R->>A: Save complete
    A->>U: Update UI
```

## Concurrency Issues Timeline

```mermaid
gantt
    title Concurrency Issues Timeline
    dateFormat  X
    axisFormat  %s
    
    section Critical Issues
    Race Condition in Init     :a1, 0, 4
    Write Semaphore Contention :a2, 2, 6
    Auto-save Race Condition   :a3, 1, 5
    
    section Performance Issues
    Connection Management     :a4, 3, 7
    Transaction Scope         :a5, 4, 8
    
    section Reliability Issues
    Database Integrity Check  :a6, 5, 9
    Error Handling            :a7, 6, 10
```

## Recommended Architecture Changes

```mermaid
graph TB
    subgraph "New Architecture"
        A[UI Layer] --> B[Repository Layer]
        B --> C[Database Layer]
        
        subgraph "Repository Layer"
            D[RoundRepository]
            E[PlayerRepository]
            F[CourseRepository]
            G[RepositoryBase]
        end
        
        subgraph "Synchronization Layer"
            H[Connection Pool]
            I[Scoped Semaphores]
            J[Transaction Manager]
            K[Retry Mechanism]
        end
        
        subgraph "Monitoring Layer"
            L[Performance Monitor]
            M[Error Handler]
            N[Health Check]
        end
        
        G --> H
        D --> I
        D --> J
        D --> K
        D --> L
        D --> M
        D --> N
    end
```

This diagram illustrates the recommended architecture changes to address the identified timing, race condition, and stability issues in the current implementation.