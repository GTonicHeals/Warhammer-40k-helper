# Warhammer 40k: Tactical Dashboard

A specialized Blazor Web Application designed to visualize Warhammer 40,000 10th Edition rosters and rules. This system parses JSON data to create a high-performance, game-ready dashboard with dynamic rule filtering and interactive reference tools.
Core Features
Interactive Roster Viewer

    Dual-Army Support: Parallel processing for roster.json and enemy.json to manage both friendly and opposing forces.

    Automated Stratagem Mapping: Dynamically links unit names to specific Stratagems using keyword-based string parsing.

    Detachment Logic: Filters out illegal Stratagems based on the roster's selected Detachment and Faction ID.

    Theme Switching: Context-aware styling (Orange for Player 1, Blue for Player 2) to maintain visual clarity during gameplay.

Stratagem Management

    Database Integration: Loads a central library of over 1,300 Stratagems from an external JSON source.

    Contextual UI: Automatically groups and color-codes Stratagems by turn availability (Your Turn, Opponent's Turn, or Either Turn).

    Modal Overlay: Detailed rule descriptions are accessible via a popup system to preserve datasheet screen space.

Rules Engine & Quick Reference

    Turn Sequence: Step-by-step documentation of 10th Edition phases.

    Wound Calculator: Logic-driven tool that calculates required wound rolls based on Strength and Toughness inputs.

    Keyword Glossary: Searchable database of Universal Special Rules with real-time text highlighting via Regular Expressions.

Technical Stack

    Framework: ASP.NET Core Blazor (Interactive Server Mode)

    Language: C# 12 / .NET 8

    Data Management: System.Text.Json for high-speed serialization

    Frontend: Bootstrap 5 and Custom CSS3

    Interop: JavaScript Runtime for DOM manipulation and smooth scroll fragments

Installation and Setup
Prerequisites

    .NET 8.0 SDK

Environment Configuration

    Clone the repository to your local machine.

    Place your exported roster files in the following directory:

        wwwroot/roster.json

        wwwroot/enemy.json

    Ensure the rules database is present at:

        wwwroot/data/Stratagems.json

Deployment

Execute the following command in the project root:
Bash

dotnet watch run

The application will be hosted locally, typically at https://localhost:7197.
Data Schema Reference
Stratagem Unit Mapping

The system identifies usable rules by splitting the stratagems string attribute within the units array:
JSON

    {
      "data": {
        "units": [
          {
            "name": "Hellblaster Squad",
            "stratagems": "COMMAND RE-ROLL|GO TO GROUND|FIRE OVERWATCH"
          }
        ]
      }
    }

Development Roadmap

    Implementation of a global search bar for the Roster view.

    Development of a probability calculator (MathHammer) for damage output analysis.

    Migration to browser-based file uploads to replace static JSON file dependencies.

Disclaimer

This project is for personal use and educational purposes. Warhammer 40,000 is a trademark of Games Workshop. This application is not affiliated with or endorsed by Games Workshop.
