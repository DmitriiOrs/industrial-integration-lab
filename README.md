# SolidWorks Data Extraction Node (ETL)

## Overview
This ETL console application acts as an **Edge Extraction Node** for building a Digital Thread in modern manufacturing environments. It serves as the primary data extraction step in an Extract, Transform, Load (ETL) pipeline. 

The application connects to the SOLIDWORKS API to extract assembly structures, metadata, and sheet metal cut-list information into a vendor-independent Canonical Product model. This universal JSON output can be seamlessly routed into relational ERP systems for procurement or Graph Databases (e.g., Neo4j) for deep traceability and dependency analysis.

## Core Features
* **Canonical Data Model:** Decouples proprietary CAD data from downstream business systems using a universal `CanonicalProduct` domain structure.
* **Deep Tree Traversal:** Recursively parses SOLIDWORKS assemblies and sub-assemblies of any depth.
* **Sheet Metal & Cut-Lists:** Identifies and extracts precise cut-list properties (bounding box, thickness, mass, material).
* **Safe COM Interop:** Implements reliable memory management and strict COM object release patterns (`Marshal.ReleaseComObject`) to prevent CAD application memory leaks and crashes.

## Architecture Pipeline
The application strictly separates data extraction from data representation using Clean Architecture principles:

`SOLIDWORKS API` ➔ `SolidWorksConnector` ➔ `SolidWorksReader` ➔ `CanonicalProduct` ➔ `JsonExporter` ➔ `JSON File`

## Requirements
* **OS:** Windows 10/11
* **CAD:** SOLIDWORKS 2022 or newer
* **Framework:** .NET 8.0

## Getting Started
1. Open SOLIDWORKS and load your target assembly (`.SLDASM`).
2. Run the C# application console.
3. The extracted hierarchical data will be saved as a JSON file in your specified directory (default: `C:\Temp\ExportedAssembly.json`).

## Project Goals (Roadmap)
### Extraction
- [x] SOLIDWORKS API
- [ ] Multi-CAD support (Inventor, KOMPAS-3D)

### Transformation
- [x] Canonical Product model
- [ ] Data normalization
- [ ] Validation
- [ ] Unit conversion

### Loading
- [x] JSON
- [ ] SQL Server / PostgreSQL
- [ ] Graph Databases (Neo4j)
- [ ] REST API
- [ ] Message Queues (Kafka / RabbitMQ)

### Enterprise Integration
- [ ] ERP (Enterprise Resource Planning)
- [ ] PLM (Product Lifecycle Management)
- [ ] MES (Manufacturing Execution System)

## Known Limitations
* **Derived / Inserted Parts:** The current parser (v1.0) does not extract parent-file metadata from derived components (parts added via the *Insert Part* or *Mirror* features in the design tree). They are currently processed as independent parts. Support for this feature is planned for future releases.