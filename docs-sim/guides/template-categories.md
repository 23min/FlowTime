# FlowTime-Sim Template Categories Guide

> **Purpose:** Organized educational progression from simulation fundamentals to real-world domain modeling.

## Overview

FlowTime-Sim templates are organized into categories that support different learning objectives and use cases. This categorization helps users find appropriate templates for their educational or testing needs.

## Categories

### **Theoretical Templates**
**Purpose:** Teaching simulation fundamentals and mathematical concepts  
**Target Users:** Students, researchers, simulation developers  
**Pedagogical Value:** Understanding the mechanics of stochastic processes

#### Current Templates:
- **`const-quick`** - Constant arrivals demo (3 bins, deterministic)
  - Perfect for understanding basic simulation mechanics
  - Demonstrates artifact generation and series structure
  - Tags: `beginner`, `constant`, `deterministic`, `quick`

- **`poisson-demo`** - Poisson arrivals demo (λ=5, 4 bins)  
  - Demonstrates stochastic arrival patterns
  - Shows randomness with controlled parameters
  - Tags: `beginner`, `poisson`, `stochastic`, `mathematical`

#### Future Expansions:
- `exponential-arrivals` - Exponential distribution patterns
- `normal-service-times` - Gaussian service time modeling
- `bursty-traffic` - Compound Poisson processes
- `seasonal-patterns` - Time-varying arrival rates

### **Domain Templates**
**Purpose:** Real-world system modeling and business education  
**Target Users:** Business analysts, operations teams, students in applied fields  
**Pedagogical Value:** Understanding how systems work in practice

#### Current Templates:
- **`it-system-microservices`** - Modern web application
  - Request queues, load balancer, service dependencies
  - Demonstrates microservices architecture patterns
  - Tags: `intermediate`, `microservices`, `web-scale`, `modern`, `it-systems`

- **`manufacturing-basic`** - Production line simulation
  - Workstation throughput and bottleneck identification
  - Shows manufacturing flow principles
  - Tags: `beginner`, `manufacturing`, `production`, `bottleneck`

- **`transportation-basic`** - Transportation network
  - Demand and capacity constraints in logistics
  - Hub-and-spoke network patterns
  - Tags: `beginner`, `transportation`, `logistics`, `capacity`

## API Endpoints

### List All Templates
```
GET /api/v1/templates
```

### Filter by Category
```
GET /api/v1/templates?category=theoretical
GET /api/v1/templates?category=domain
```

### List Categories
```
GET /api/v1/templates/categories
```

### Deprecated Endpoints
```
GET /api/v1/scenarios          # Use /templates instead
GET /api/v1/scenarios/categories # Use /templates/categories instead
```

## Educational Progression

### **Level 1: Fundamentals** (`theoretical` templates)
Start with `const-quick` and `poisson-demo` to understand:
- How simulation works
- Difference between deterministic and stochastic
- Artifact structure and series generation

### **Level 2: Applied Systems** (`domain` templates)
Progress to domain templates to learn:
- Real-world system modeling
- Business-relevant bottleneck analysis
- Cross-domain pattern recognition

### **Level 3: Advanced Integration** (Future)
Eventually progress to multi-component, multi-class templates with:
- Complex routing and priority systems
- Retry logic and failure modeling
- Advanced capacity planning

## Integration with FlowTime-vnext

When FlowTime-vnext UI is in "API mode", it automatically fetches templates from FlowTime-Sim via the template API endpoints. The category system provides better organization in the UI:

- **Theoretical templates** → "FlowTime-Sim Fundamentals" category
- **Domain templates** → "FlowTime-Sim Business Models" category

This ensures users get both pedagogical depth (fundamentals) and practical relevance (domain models) when using FlowTime-Sim as their simulation backend.

## Benefits

1. **Clear Learning Path**: Users can start with fundamentals and progress to applied scenarios
2. **Targeted Use Cases**: API consumers can filter to specific categories they need
3. **Future Extensibility**: New categories can be added without breaking existing integrations
4. **Pedagogical Value**: Both theoretical understanding and practical application supported
5. **API Flexibility**: Backward compatible with existing `/sim/scenarios` endpoint
