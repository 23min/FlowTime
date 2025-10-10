# FlowTime-Sim Known Issues

**Last Updated:** September 25, 2025 (Bug fixes completed)

This document tracks known issues and bugs in FlowTime-Sim that need to be addressed.

---

## 🐛 Active Issues

*No active issues*

---

## 📋 Resolved Issues

### 1. Template System Parameter Schema Issues (Multiple Bugs) - RESOLVED

**Issue ID:** SIM-BUG-001  
**Reported:** September 25, 2025  
**Updated:** September 25, 2025  
**Priority:** High  
**Component:** Template System (`/v1/sim/templates` and `/v1/sim/templates/{id}/generate`)

**Description:**
Systematic analysis revealed **two distinct but related bugs** in the template system affecting parameter schema handling:

#### **BUG 1A: Missing Parameters in Raw Templates**
**Affected Templates:** `it-system-microservices`, `supply-chain-multi-tier`

Raw templates retrieved from `/v1/sim/templates` are missing the `parameters` section in metadata. This prevents UI from generating proper parameter forms.

**Current State:**
```yaml
# it-system-microservices raw template
metadata:
  title: 'IT System with Microservices'
  description: '...'
  templateId: 'it-system-microservices'
  tags: [intermediate, microservices, web-scale, modern, it-systems]
  # ← MISSING parameters section
```

**Expected State:**
```yaml
metadata:
  # ... existing fields ...
  parameters:
    - name: bins
      type: integer
      # ... parameter definitions
```

#### **BUG 1B: Parameters Not Removed from Generated Models**
**Affected Templates:** `transportation-basic`, `manufacturing-line`

Generated models returned from `/v1/sim/templates/{id}/generate` still contain the `parameters` section in metadata, even though handlebars are correctly resolved.

**Current State:**
```yaml
# Generated model (should be clean)
metadata:
  title: 'Transportation Network'
  # ... other fields ...
  parameters:  # ← THIS SHOULD NOT BE HERE
    - name: bins
      type: integer
      # ... full parameter schema
```

**Expected State:**
```yaml
# Generated model (clean metadata)
metadata:  
  title: 'Transportation Network'
  description: '...'
  templateId: 'transportation-basic'
  tags: [beginner, transportation, logistics, capacity]
  # parameters section should be stripped
```

#### **Root Cause Analysis**

| Template | Raw Template Has Parameters | Generated Model Strips Parameters | Status |
|----------|---------------------------|----------------------------------|--------|
| `transportation-basic` | ✅ YES | ❌ NO (Bug 1B) | ❌ Partial |
| `manufacturing-line` | ✅ YES | ❌ NO (Bug 1B) | ❌ Partial |  
| `it-system-microservices` | ❌ NO (Bug 1A) | N/A | ❌ Missing |
| `supply-chain-multi-tier` | ❌ NO (Bug 1A) | N/A | ❌ Missing |

#### **Impact**
- **UI Parameter Forms**: Missing parameters prevent proper form generation
- **UI Generated Models**: Parameter pollution confuses model preview display  
- **API Contract Inconsistency**: Some templates work correctly, others don't
- **Development Workflow**: Template integration unreliable

#### **Complete Fix Required**
1. **Add parameters section** to `it-system-microservices` and `supply-chain-multi-tier` raw templates
2. **Modify generation endpoint** to strip `parameters` section from generated model metadata
3. **Ensure consistency** across all 4 templates

#### **Test Cases**
```bash
# Test 1: All raw templates should have parameters in metadata  
for template in it-system-microservices supply-chain-multi-tier manufacturing-line transportation-basic; do
  has_params=$(curl -s "http://localhost:8090/v1/sim/templates" | jq -r ".[] | select(.id==\"$template\") | .yaml" | grep -c "parameters:")
  [ "$has_params" -gt 0 ] && echo "✅ $template has parameters" || echo "❌ $template missing parameters"
done

# Test 2: All generated models should NOT have parameters in metadata
for template in it-system-microservices supply-chain-multi-tier manufacturing-line transportation-basic; do  
  curl -s -X POST "http://localhost:8090/v1/sim/templates/$template/generate" \
    -H "Content-Type: application/json" -d '{"bins":4,"binMinutes":60}' | \
    jq -r '.scenario' | grep -q "parameters:" && echo "❌ $template has parameters" || echo "✅ $template clean"
done
```

#### **Resolution** ✅
**Fixed in:** Feature branch `feature/templates-v0.3.2/enhanced-parameter-types`  
**Commit:** TBD  
**Date:** September 25, 2025

**Changes Made:**
1. **Added parameters sections** to `it-system-microservices.yaml` and `supply-chain-multi-tier.yaml` templates  
2. Legacy pathway removed. Generation now handled by `FlowTime.Sim.Core.Services.NodeBasedTemplateService`, which strips the `parameters` section from generated model metadata by design.
3. **Added proper parameter definitions** with titles, descriptions, default values, and validation constraints

**Test Results:**
- ✅ All 88 tests passing
- ✅ All raw templates now contain parameters section
- ✅ Generated models have parameters section stripped
- ✅ API contract consistency achieved across all 4 templates

**Status:** **RESOLVED** - Both BUG 1A and BUG 1B fixed

---

## 📝 Issue Reporting

To report new issues:
1. **Search existing issues** in this document first
2. **Add to Active Issues** section above
3. **Use format**: Issue ID, Priority, Component, Description, Expected/Current Behavior, Impact, Solution
4. **Update Last Updated** date at top of document
5. **Reference in commit**: `docs: add SIM-BUG-XXX to known issues`

**Priority Levels:**
- **Critical**: System broken, blocking development
- **High**: Major functionality affected
- **Medium**: Noticeable issue, workaround available  
- **Low**: Minor issue, cosmetic or edge case