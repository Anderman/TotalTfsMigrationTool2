# Total TFS Migration Tool

Improved clone over https://totaltfsmigration.codeplex.com used to migrate workitems from one Collection to another Collection.

## Project Description

If you have many (team)projects in a single collection and few teams you should read this [Single team project](http://geekswithblogs.net/Optikal/archive/2013/09/05/153944.aspx). This tool migrate all you projects to a single project with many teams(projects)

###old structure:
  * Default Collection
    *  Project 1
      * Project 1 team
    *  Project 2
      * project 2 team
      
###New structure
  * default collection
    * Projects
      * Project 1
      * Project 2
      * project n
      * team 1
      * team 2
    

The Total TFS Migration Tool is a tool to facilitate Migration from many (team)projects to a single project with many teams(project). Currently the tool support migration of 
  * Work items (inclusief histoy and work item number), 
  * Iterations inclusief dates, 
  * Areas 
  * All Work flows are converted to scrum template. 

## Start migration
  * In 'Team Foundation Server Administration Console' 2015 create a new collection 'DefaultCollectionNew'
  * In Tfs 2015 web interface 'Control Panel - DefaultConnectionNew' Create a project 'Projects'
  * Edit the app.config file and set the source and dest url
    *  ```<add key="SourceUri" value="http://tfs2015:8080/tfs/DefaultCollection" />```
    *  ```<add key="DestUri" value="http://tfs2015:8080/tfs/DefaultCollectionNew/Projects" />```
  * Ensure you have enough rights and run the tool with F5.

Customize the tool and rerun all the migration steps until you are satified eith the result
  
  See the app.config to move a single project 

## improvements
  * Complete history of workitems
  * copy iteration dates
  * move a complete collection
  * Move a single project
  * Work itemID stays the same after migration
  * complete rewrite of the codebase
  * Rerun will only update new items

## Known issue / custumization
Maybe you have to customize the tool to fullfill your needs.
  * migration to another template (Current scrum and agile=>scrum supported)
  * Test are not migrated. You can find an Example how to do this in the original tool

## example of a single project with many team projects

After migration all projects are moved under projects. On this level you have to create your teams

![Single project](https://cloud.githubusercontent.com/assets/1858745/18815808/b6fa43ec-833a-11e6-9230-2ac63c9a9d39.png)

Select the areas(projects) for which your team is responsable

![p2](https://cloud.githubusercontent.com/assets/1858745/18815824/6c8a26b4-833b-11e6-8a28-98bb5e0c8ac0.png)

Your team has information about all projects that they are working on (the product backlog and sprint backlog)
![image](https://cloud.githubusercontent.com/assets/1858745/18815883/36d215ac-833d-11e6-85c8-ab40d4d9558b.png)

