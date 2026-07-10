im working on docs and would you to create the docs for me related to the project before we apply anything. reference the .repos/payload/docs as this is something similar to payload cms but will be agent driven. using existing agents like hermes/codex/cursor/opencode and whatever else will be added later on if need be.

> do not guess, ask the relevant questions if you need help but first make sure to think logically and what would be the best before you ask. 
> do not apply anything in this session other than the docs
> docs should not be framework/programming language spec

## docs needed

- admin
- auth
- access control (use payload as the example to how this should look)
- agents
- architecture
    - overview
    - routing
- plugin service (requires a page with a list of services to toggle/enable. not all services are services that require enabling like the form service, user service, seo service, auth service)
    - custom plugin creation
    - email
        - resend
    - ecommerce
    - cloudflare r2
    - google merchant
- source control
- database
- hooks
- pipeline

## rules

- the file and reference names for the agent needs to be documented as well (like what the user/agent should name the page to be able to overide the existing page). this should be a skill for a agent to use and follow when working on the project like overiding the ui or creating a new service. when new plugins are created this needs to be updated (this is a rule for the agent that will be updating the docs after another agent has made sure all has been implemented)
- the default page overwritten is not removed and is the fallback when the user removes their custom admin ui.
- node.js and docker needs to be added to the server if it does not exist and skip the instalation and continues. (there needs to be a loading instance showing for the user)
- all apis need to get a mcp added to that request.
- the website and the admin dash should be added and run on docker on the server to start/restart the app using docker

## requirements 

- the user runs a command to add the CompanyHQ to the server
    - use "-c" for the company name when making the request to add a new company
    - Add the option to add the company from the local machine and the user adds server credentials like The server IP, the server username and password, or the configuration direction. And the name that should be used inside of that config file to add the company to that. like "-s" , "-u", "-p" , "sn" (server name to be used from the config file on the local machine to connect to the server using a key instead of username and password (case sensitive))
- the user is prompted to add info about the company
- the server gets the below when the user ads this to the server. (this allows even if the user adds more than one to the same server that it would then still be isolated)
    - CompanyQH/{CompanyName}/
    - CompanyQH/{CompanyName}/docker-compose (this is for all self hosted tools used for the project/company)
    - CompanyQH/{CompanyName}/.env
- the user must be asked for additional info about the company like description, target market
- this is a agent driven cms system and would need codex/opencode/hermes (company level and enclosed in a docker instance).
- the user should be able to add staff with their own role based access
- there should be a explorer to be able to manage the admin ui (user can overide the ui of the admin page using the name directly and then), add services/plugins to the company (ontop of the built in/provided services). when the user clicks on the page the user should be able to see the ui and then have a edit code option to view the code of the ui itself. the below is how the explorer should look
    # Admin
        - Pages
        - Services
            - {ServiceName}/Pages/
            - {ServiceName}/Settings/
            - {ServiceName}/Service/
            - {ServiceName}/Models/
    # Projects (projects are not added by default, this is added when needed and should be able to add more than one app (like driver/client apps) where the driver sees the relevant info to them only. this is added to the server directly as well as each project is saved to github and pulled using github main. agents when working on the projects work in worktrees and then pulled in when approved)
        - Components (general components sharable by the projects)
        - {Website}
            - Pages
            - Components
        - {Mobile App}
            - Pages
            - Components
        - {Desktop App}
            - Pages
            - Components
- the pipeline would allow me to view my tasks/as a owner all tasks to them and the agent tasks/there would be a marketing pipeline.