# Assignment: Adding a Registration API and Admin Client to the Blog

## Introduction

Building upon our blog platform's backend infrastructure, we'll now create an API-based user management system and a console application to interact with it. This assignment introduces API controllers, claims-based authorization, and external API consumption patterns.

## Scenario

Atlas Publishing Co. needs a secure way to manage user accounts with different roles (Admin, Author, Moderator) on their blogging platform. The development team has asked for an API-based solution with a separate console application for account management.

## Requirements

### 1: Web API Development

Create an `AuthAPIController` in the Controllers folder that will handle user registration for different roles:

- Create an API key to use this controller in the `appsettings.json` file. It can be a random string of at least 32 character length. The controller should receive its API key from configuration via its constructor.
- All API endpoints must validate the provided API key parameter.
- Implement the following API endpoints:
  - `/api/auth/registeradmin`: Receive an API key, username (email), and password. Create a new user with Identity Framework and add a claim ("admin", "true").
  - `/api/auth/registerauthor`: Receive an API key, username, password, blog title, subtitle, and description. Create the user with Identity Framework, create the blog associated with the new user id, and add a claim ("author", "true").
  - `/api/auth/registermoderator`: Receive an API key, username, password. Create a new user with Identity Framework and add a claim ("moderator", "true").

### 2: Console Application

We don't want to expose creating admin, author, and moderators with web pages. We will create a console application that uses the API key we created and the `HttpClient` object to create these users.

Create a new .NET Console application named `BlogConsole` that will:

- Store and retrieve the API key from its own `appsettings.json`.
- Present a menu for adding different types of users.
- Use HttpClient to communicate with the Web API.
- Handle API errors appropriately.

### 3: Testing Requirements

1. Use the console application to create:
   - At least one admin account
   - At least one author account with an associated blog
   - At least one moderator account
2. Verify the created data in the SQLite database

## Starter Files

Start with the submission from the previous week's assignment.

## Conclusion

Now that we have a way to register special users, we can move on to the application's user-facing portion.