UserInputIVR is the sample which simulates "PIN IVR"
The list of PINs are declared inside the script.
userinput -> destination
When caller enters valid (defined) PIN, the call will be routed to the specified destination
In case of noinput the destinations for empty PIN will be used
In case of incorrect (undefined) PIN the delivery will fail and prompt will be repeated and caller should enter another PIN.
Script allow three input attempts, then disconnects the call.
It could be yet another destination defined in the script.
