WARNING: the scripts is not compilable prior to v20 SP1. 
Switch whole PBX to specific hours.
v20 removes support of in office/outof office dial code for PBX.
Here is the sample of RoutingPoint which can implement virtually any vision what does the "switch to out of office hours" means for the specific PBX.
For the multidepartment model, switching whole PBX to spcific hours means that the specific office hours needs to be enforced for all departments.
The logic is simple. Global time zone of PBX is used, and all departments are set to the requested type of office hours until the end of the day defined by Global office hours.
What will happen:
all timebased routing in all department will work as specified for the specified office hours type until the end of the day in Global time zone
Reset of office hours will reset all department to default operation schedule.
DialCodes defined in this sample:
#60 - reset all deparments to default hours operation
#61 - force all deparments in office
#62 - force all deparments out of office
#63 - force all deparments break time
#64 - force all deparments holiday time








