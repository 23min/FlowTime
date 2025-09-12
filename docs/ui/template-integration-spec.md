# FlowTime UI: Template Integration Specification

## Overview

This specification defines how the FlowTime UI should integrate with the new FlowTime-Sim parameterized template system to provide users with dynamic scenario configuration capabilities.

## UI Component Architecture

### Template Browser Component
**Purpose**: Display available simulation templates with filtering and selection
**Location**: `/ui/src/components/TemplateBrowser.tsx`

**Features**:
- Category filtering (Theoretical, Domain)
- Search/filter by tags
- Template cards with title, description, and parameter count
- "Configure" button for parameterized templates
- "Run Default" button for non-parameterized templates

```tsx
interface TemplateBrowserProps {
  onTemplateSelect: (template: Template) => void;
  selectedCategory?: 'theoretical' | 'domain';
}
```

### Parameter Configuration Component  
**Purpose**: Dynamic form generation based on template parameter schema
**Location**: `/ui/src/components/ParameterConfig.tsx`

**Features**:
- Dynamic form generation from parameter schema
- Real-time validation with error display
- Parameter grouping (Basic, Advanced, Timing, etc.)
- Default value population
- Help tooltips from parameter descriptions
- Live YAML preview (optional)

```tsx
interface ParameterConfigProps {
  template: Template;
  onParametersChange: (params: Record<string, any>) => void;
  onGenerate: (params: Record<string, any>) => void;
  initialValues?: Record<string, any>;
}
```

### Scenario Generator Service
**Purpose**: Handle API communication with FlowTime-Sim
**Location**: `/ui/src/services/TemplateService.ts`

```tsx
class TemplateService {
  async getTemplates(category?: string): Promise<Template[]>;
  async generateScenario(templateId: string, parameters: Record<string, any>): Promise<string>;
  async validateParameters(templateId: string, parameters: Record<string, any>): Promise<ValidationResult>;
}
```

## User Experience Flow

### Template Selection Flow
```
1. User lands on "New Simulation" page
2. Template Browser shows categorized templates
3. User filters by category/tags  
4. User clicks "Configure" on a template
5. Parameter Configuration form appears
6. User adjusts parameters with real-time validation
7. User clicks "Generate & Run"
8. System generates YAML and starts simulation
```

### UI States
- **Loading**: Fetching templates from API
- **Template Selection**: Browsing available templates
- **Parameter Configuration**: Setting template parameters
- **Generating**: Converting template to YAML scenario
- **Ready**: YAML generated, ready to simulate
- **Error**: Validation or generation errors

## Page Layout Specifications

### Template Selection Page (`/simulate/new`)

```
┌─────────────────────────────────────────────────────────┐
│ FlowTime - New Simulation                               │
├─────────────────────────────────────────────────────────┤
│ Category Filter: [All] [Theoretical] [Domain]          │
│ Search: [________________] 🔍                           │
├─────────────────────────────────────────────────────────┤
│ ┌───────────────┐  ┌───────────────┐  ┌───────────────┐ │
│ │ Poisson Demo  │  │ IT System     │  │ Manufacturing │ │
│ │ Stochastic    │  │ Microservices │  │ Production    │ │
│ │ arrivals demo │  │ Web app load  │  │ Bottleneck    │ │
│ │               │  │ testing       │  │ analysis      │ │
│ │ 2 parameters  │  │ 4 parameters  │  │ 3 parameters  │ │
│ │ [Configure]   │  │ [Configure]   │  │ [Configure]   │ │
│ └───────────────┘  └───────────────┘  └───────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### Parameter Configuration Page (`/simulate/configure/{templateId}`)

```
┌─────────────────────────────────────────────────────────┐
│ FlowTime - Configure IT System Microservices           │
├─────────────────────────────────────────────────────────┤
│ ← Back to Templates                    [Preview YAML]   │
├─────────────────────────────────────────────────────────┤
│ Basic Parameters                                        │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ Request Rate (req/min)          [100    ] ⓘ        │ │
│ │ Range: 10 - 10,000                                  │ │
│ │                                                     │ │
│ │ Time Bins                       [6      ] ⓘ        │ │
│ │ Range: 3 - 24                                       │ │
│ │                                                     │ │
│ │ Minutes per Bin                 [60     ] ⓘ        │ │
│ │ Range: 15 - 480                                     │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ ▼ Advanced Parameters                                   │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ Random Seed                     [789    ] ⓘ        │ │
│ │ Range: 1 - 999,999                                  │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│              [Generate & Run]  [Save as Preset]        │
└─────────────────────────────────────────────────────────┘
```

## Form Input Components

### Number Input Component
```tsx
interface NumberInputProps {
  parameter: TemplateParameter;
  value: number;
  onChange: (value: number) => void;
  error?: string;
}

// Features:
// - Min/max validation
// - Step increments
// - Unit display
// - Error state styling
```

### Parameter Group Component
```tsx
interface ParameterGroupProps {
  title: string;
  parameters: TemplateParameter[];
  values: Record<string, any>;
  onChange: (name: string, value: any) => void;
  errors: Record<string, string>;
  collapsible?: boolean;
  defaultExpanded?: boolean;
}
```

## API Integration Patterns

### Template Loading
```typescript
const useTemplates = (category?: string) => {
  const [templates, setTemplates] = useState<Template[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadTemplates = async () => {
      try {
        setLoading(true);
        const data = await templateService.getTemplates(category);
        setTemplates(data);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    loadTemplates();
  }, [category]);

  return { templates, loading, error };
};
```

### Parameter Validation Hook
```typescript
const useParameterValidation = (template: Template) => {
  const [errors, setErrors] = useState<Record<string, string>>({});
  
  const validateParameter = (name: string, value: any) => {
    const param = template.parameters.find(p => p.name === name);
    if (!param) return;

    const error = validateParameterValue(param, value);
    setErrors(prev => ({
      ...prev,
      [name]: error || undefined
    }));
  };

  return { errors, validateParameter };
};
```

### Scenario Generation Hook
```typescript
const useScenarioGeneration = () => {
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const generateScenario = async (templateId: string, parameters: Record<string, any>) => {
    try {
      setGenerating(true);
      setError(null);
      const scenario = await templateService.generateScenario(templateId, parameters);
      return scenario;
    } catch (err) {
      setError(err.message);
      throw err;
    } finally {
      setGenerating(false);
    }
  };

  return { generateScenario, generating, error };
};
```

## Validation & Error Handling

### Client-Side Validation
```typescript
const validateParameterValue = (param: TemplateParameter, value: any): string | null => {
  if (param.type === 'number') {
    const num = Number(value);
    if (isNaN(num)) return `${param.title} must be a number`;
    if (param.minimum !== undefined && num < param.minimum) {
      return `${param.title} must be at least ${param.minimum}`;
    }
    if (param.maximum !== undefined && num > param.maximum) {
      return `${param.title} must be at most ${param.maximum}`;
    }
  }
  
  if (param.type === 'integer') {
    const int = parseInt(value);
    if (isNaN(int) || int !== Number(value)) {
      return `${param.title} must be a whole number`;
    }
    // Check min/max constraints...
  }
  
  return null;
};
```

### Error Display Component
```tsx
const ErrorMessage: React.FC<{ error?: string }> = ({ error }) => {
  if (!error) return null;
  
  return (
    <div className="error-message" role="alert">
      <Icon name="warning" />
      {error}
    </div>
  );
};
```

## Responsive Design Considerations

### Mobile Layout
- Stack parameter inputs vertically
- Collapsible parameter groups by default
- Touch-friendly input controls
- Simplified template cards

### Desktop Layout
- Side-by-side template selection and configuration
- Expandable parameter groups
- Optional live YAML preview pane
- Keyboard navigation support

## Accessibility Requirements

### Screen Reader Support
- Proper ARIA labels for all form inputs
- Error announcements via `aria-live` regions
- Form validation state communication
- Template card semantic structure

### Keyboard Navigation
- Tab order through template cards
- Arrow key navigation in parameter forms
- Keyboard shortcuts for common actions
- Focus management during state transitions

## Performance Considerations

### Template Caching
```typescript
// Cache templates in SessionStorage
const TEMPLATE_CACHE_KEY = 'flowtime.templates';

const useTemplateCache = () => {
  const getFromCache = (category?: string): Template[] | null => {
    const cached = sessionStorage.getItem(`${TEMPLATE_CACHE_KEY}.${category || 'all'}`);
    return cached ? JSON.parse(cached) : null;
  };

  const setCache = (templates: Template[], category?: string) => {
    sessionStorage.setItem(`${TEMPLATE_CACHE_KEY}.${category || 'all'}`, JSON.stringify(templates));
  };

  return { getFromCache, setCache };
};
```

### Debounced Parameter Updates
```typescript
const useDebouncedParameters = (onUpdate: (params: Record<string, any>) => void, delay = 300) => {
  const [parameters, setParameters] = useState<Record<string, any>>({});
  const debouncedUpdate = useMemo(() => debounce(onUpdate, delay), [onUpdate, delay]);

  const updateParameter = (name: string, value: any) => {
    const updated = { ...parameters, [name]: value };
    setParameters(updated);
    debouncedUpdate(updated);
  };

  return { parameters, updateParameter };
};
```

## Testing Strategy

### Component Testing
- Template Browser rendering and filtering
- Parameter form generation from schema
- Validation error display
- API integration error handling

### Integration Testing  
- End-to-end template selection to scenario generation
- Error state handling across components
- Browser back/forward navigation
- Mobile responsive behavior

### API Mocking
```typescript
// Mock template service for testing
const mockTemplateService = {
  getTemplates: jest.fn().mockResolvedValue(mockTemplates),
  generateScenario: jest.fn().mockResolvedValue(mockScenario),
};
```

This specification provides the foundation for implementing a rich, accessible, and performant template configuration UI that leverages the FlowTime-Sim parameterized template system.