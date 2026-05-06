# B-Lab Revit API Suite - Version 1.0.5.0 Update

## Overview
This update introduces significant enhancements to the **Dimension Automation**, **Master Export**, and **RoomSheet** tools, alongside a complete professional UI overhaul.

## Key Features

### 1. Dimension Automation Upgrades
- **Angled Grid Support**: Dimensions now correctly align to non-orthogonal grid systems.
- **Curtain Wall Mullions**: New mode to automatically dimension mullion spacing in curtain walls.
- **Linked Model Coordination**: Toggle support for host-only or linked-element dimensioning.
- **Element Groups**: Intelligent detection to respect or ignore element groups during dimensioning.

### 2. Master Export Dashboard (Wide Layout)
- **New Horizontal Interface**: Replaced the vertical sidebar with a wide (850px) 3-column professional dashboard.
- **Multi-Format Bulk Export**: Parallel processing for PDF, DWG, and Navisworks NWC.
- **Link-Based NWC Slicing**: Option to export linked models as individual NWC files.
- **Level-Based Export**: Automatic vertical slicing of models into floor-by-floor coordination NWCs using dynamic section boxes.
- **Searchable Sheet Selection**: Integrated filtering system for large-scale sheet sets.

### 3. RoomSheet Generation
- **Automated 500mm Offset Cropping**: 
  - **Plan Views**: Follow room footprints with a precise 500mm buffer.
  - **3D Views**: Section boxes are expanded by 500mm in all directions (X, Y, Z).
  - **Elevations**: Automatic crop activation for interior elevations.
- **Link Transformation**: Robust support for creating room sheets from rooms located in linked models.

### 4. Professional UI Redesign
- **Light Theme**: Clean white background with high-contrast typography.
- **Enterprise Aesthetics**: Consistent use of Autodesk-Blue accents, rounded borders, and structured sectioning.
- **Sidebar & Dashboard Modes**: Optimized layouts for focused automation tasks and comprehensive export workflows.

## Technical Details
- **Version**: 1.0.5.0
- **Compatibility**: Revit 2024, 2025, 2026, 2027
- **Frameworks**: .NET Framework 4.8 / .NET 8 / .NET 10
- **Build Status**: Verified Clean (0 Errors)

---
*Developed by B-Lab - Precision BIM Solutions*
