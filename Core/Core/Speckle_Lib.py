import clr
import os

dirnameThis = os.path.dirname(__file__)
#sPath = os.path.dirname(dirnameThis)
filename = "%s\\SpeckleCore2.dll" % dirnameThis

if os.path.isfile(filename):
    clr.ClearProfilerData()
    clr.AddReferenceToFileAndPath(filename)

try:    
    from Speckle import *
    print "Imported Speckle library from %s" % filename    
    
except ImportError:
    print ("Speckle ERROR: The module could not be imported, Check the DLL path in your RhinoPythonEditor configs.")
    print (filename)