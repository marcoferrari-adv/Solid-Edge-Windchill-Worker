# **Soldid Edge Worker**

# Table of Contents
1. [Pre Requisite](#Pre-Requisite)
2. [Installation](#Installation)
3. [Supported Post Publih Formats](#Supported-Post-Publih-Formats)


## Pre Requisite

- [ ] Install and configure Birlasoft OOTB Solid Edge Worker
- [ ] Install .Net framework 4.5 or later version
- [ ] Run psvchange_config.exe from creo view adapter folder and configure the software to a specific folder.
      Flag only "create psvchangebatch" option

## Installation
- [ ] Copy the compiled executable and required dll to bin folder of solid edge worker setup folder
- [ ] Edit SEdgeToPV.exe.config setting required keys (appSettings)
  1. SolidEdgeHome: the home path of Solid Edge Installation (e.g. C:\Program Files\Solid Edge ST10)
  2. IntermediateFormat: the intermediate format for 3D conversione, allowed values are STEP/JT, the related format2pv native PTC software must be installed
  3. DebugFlag: output debug information for conversion jobs, allowerd values are true/false
- [ ] Copy psvchangebatch from setup directory to bin\PVSCHANGE folder inside Solid Edge Worker setup folder (e.g C:\ptc\sedge_setup\bin) if PSVCHANGE folder doesn't exist create it
- [ ] Configure logging path in file SEdgeToPV.exe.config on file appender

## Supported Post Publih Formats

### 3D Models
1. STP
2. IGS
3. PARADOLID
4. PDF
5. SAT
6. STL
7. JT

### 2D Models
1. PDF
2. DWG
3. DXF
