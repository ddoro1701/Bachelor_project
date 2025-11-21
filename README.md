This project is a full-stack web application designed to digitalize and automate the package management process at Wrexham University. The existing workflow is entirely manual and paper-based, resulting in delays, errors, and increased administrative workload. This system replaces that workflow with a modern, automated solution that uses Optical Character Recognition (OCR), cloud services, and email automation to streamline the process.

🚀 Features

Automated OCR Processing
Extracts text from package label images using Microsoft Azure Cognitive Services.

Intelligent Recipient Matching
Matches OCR-extracted names against a Cosmos DB lecturer database using the Levenshtein distance algorithm.

Automated Email Notifications
Sends package arrival notifications via Azure Communication Services—no Outlook required.

Web-Based Interface (React.js)
Allows reception staff to upload images, manage packages, view logs, and update lecturer data.

Secure Cloud Infrastructure
Uses Azure App Services and Azure Cosmos DB with environment-based configuration and GDPR-compliant data handling.

Digital Logtable
Replaces manual paper logs with a searchable and filterable digital record.

🛠️ Tech Stack

Frontend:

React.js

HTML5 / CSS3

Fetch API

Backend:

ASP.NET Core

Entity Framework

ImageSharp (Image preprocessing)

Cloud Services:

Azure Cognitive Services (Vision API – OCR)

Azure Cosmos DB (NoSQL)

Azure App Services

Azure Communication Services (Email)

📁 System Architecture

Frontend communicates with backend via RESTful API endpoints

Backend handles OCR, preprocessing, lecturer matching, and database operations

Data stored in Azure Cosmos DB

Deployed via Azure App Services for continuous availability

📄 Key Functionalities

Upload package label images

Automatic text extraction via OCR

Suggested lecturer email based on similarity matching

One-click email notification

Package management dashboard

Lecturer database management

Digital log table with filtering and history view

🔐 Security and Compliance

GDPR-compliant processing of personal data

Secure authentication and protected configuration

Role-based access control (future-ready architecture)

Encrypted data access and secure environment variables

📚 Project Background

This application was developed as part of a B.Sc. (Hons) Computer Science dissertation project at Wrexham University 

The goal was to modernize an outdated manual workflow and demonstrate practical use of cloud technologies, full-stack architecture, and automation.

📦 Installation & Deployment
Frontend
npm cache clean --force
npm install
npm run build


Move the generated build/ folder into the backend’s wwwroot/ directory for deployment.

Backend
dotnet restore
dotnet build
dotnet run
