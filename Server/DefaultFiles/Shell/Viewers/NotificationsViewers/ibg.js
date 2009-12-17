// This code is released under the LGPL
// See /Docs/license.wchtml -->

{
   load: function(viewerDiv, objectUrl, ibgProxy)
   {
      this.IbgProxy = ibgProxy;

      this.viewerDiv = viewerDiv;
      viewerDiv.innerHTML = '<h1>Loading...</h1>';

      var me = this;

      ibgProxy.GetAll(
         {},
         function(particles)
         {
            viewerDiv.innerHTML = '';

            var addParticle = function(id)
            {

               if (null == id)
                  return;

               if (!particles[id])
                  return;

               var particle = particles[id];

               var element = me.createElement(particle, id);

               viewerDiv.appendChild(element);

               addParticle(particle.n);
            }

            addParticle(0);
         });

      return this;
   },

   createElement: function(particle, id)
   {
      var me = this;
      var element;

      if (0 == particle.c.indexOf("@{"))
      {
         element = new Element(
            "div",
            {
               id: this.getElementId(id)
            });
      }
      else
      {
         element = new Element(
            "input",
            {
               type: "text",
               id: this.getElementId(id),
               style: "font-family: courier"
            });

         element.observe('keyup', me.handleKeyup);
         element.me = me;
      }

      this.setValueOnElement(element, particle);
      element.changetags = {};

      return element;
   },

   handleKeyup : function(event)
   {
      var element = event.element();
      var me = element.me;
      var forceNewElement = false;
      var updateSent = false;

      // If the input was emptied, then delete it
      if (element.value == "" && (event.keyCode == Event.KEY_BACKSPACE || event.keyCode == Event.KEY_DELETE))
      {
         me.deleteNode(element.previous(), element);

         if (element.previous())
            element.previous().focus();

         element.parentNode.removeChild(element);
      }

      // If key was return / enter
      if (13 == event.keyCode)
      {
         var newElement = new Element("div", {});
         newElement.changetags = {};

         newElement.me = me;

         var newNodeId = "id" + Math.floor(Math.random()*99999999999999999);
         newElement.id = me.getElementId(newNodeId);
         newElement.version = (-1 * Infinity);

         newElement.value = "@" + Object.toJSON(
         {
            t: "usertimestamp"
         });

         newElement.innerHTML = "... waiting ...";

         element.parentNode.insertBefore(newElement, element.next());

         me.insertNode(element, newElement);

         // Fake a space so a new one is created
         element = newElement;
         forceNewElement = true;
      }

      // If key was space
      if (32 == event.keyCode || forceNewElement)
      {
         var words;
        
         if (element.value && !forceNewElement)
            words = element.value.split(" ");
         else
            words = [];

         if (words.length > 0)
            element.value = words[0];

         var newNodeId = "id" + Math.floor(Math.random()*99999999999999999); 

         var newElement = new Element(
            "input",
            {
               type: "text",
               size: 1,
               style: "font-family: courier",
               id: me.getElementId(newNodeId)
            });

         newElement.me = me;
         newElement.changetags = {};

         if (words.length > 1)
            newElement.value = words[1];

         element.parentNode.insertBefore(newElement, element.next());

         newElement.size = newElement.value.length + 1;
         newElement.focus();
         newElement.version = 0;

         newElement.observe('keyup', me.handleKeyup);

         me.insertNode(element, newElement);
      }

      // Send an update if the contents of the node changed
      me.updateNode(element);

      element.size = element.value.length + 1;
   },

   changeQueue: [],

   changePending: false,

   getId: function(element)
   {
      return element.id.split("_")[1];
   },

   getElementId: function(id)
   {
      return "input_" + id;
   },

   queueOrRunChange: function(change)
   {
      // run the change or queue
      if (!this.changePending)
         this.runChange(change);
      else
         this.changeQueue.push(change);
   },

   getValueFromElement: function(element)
   {
      if (0 == element.value.indexOf("@{"))
         return element.value;  

      else if (0 == element.value.indexOf("@"))
         return "@" + element.value;

      return element.value;  
   },

   runChange: function(change)
   {
      this.changePending = true;
      var me = this;

      var runNextChange = function()
      {
         // Process any additional changes that are sitting on the queue
         if (me.changeQueue.length > 0)
         {
            var nextChange = me.changeQueue.shift();
            me.runChange(nextChange);
         }
         else
            me.changePending = false;
      };

      var handleError = function(transport)
      {
         runNextChange();

         if (transport.responseText)
            alert(transport.responseText);
         else
            alert(transport.responseText);
      };

      try
      {
         if ("update" == change.action)
            this.IbgProxy.updateNode(
               {
                  id: this.getId(change.element),
                  version: change.element.version,
                  contents: this.getValueFromElement(change.element),
                  changetag: change.changetag
               },
               function(updatedNode)
               {
                  change.element.version = updatedNode.v;
                  runNextChange();
               },
               handleError);

         else if ("insert" == change.action)
            this.IbgProxy.insertNode(
               {
                  priorId: this.getId(change.priorElement),
                  priorNodeVersion: change.priorElement.version,
                  priorNodeContents: this.getValueFromElement(change.priorElement),
                  priorChangetag: change.changetag,
                  newNodeContents: this.getValueFromElement(change.newElement),
                  newNodeId: this.getId(change.newElement),
                  newChangetag: change.changetag
               },
               function(updatedNodes)
               {
                  change.priorElement.version = updatedNodes.priorNode.v;
                  change.newElement.version = updatedNodes.newNode.v;

                  // for command-style nodes, the value is changed at the server
                  if (0 == updatedNodes.newNode.c.indexOf("@{"))
                     me.setValueOnElement(change.newElement, updatedNodes.newNode);

                  runNextChange();
               },
               handleError);

         else if ("delete" == change.action)
         {
            this.IbgProxy.deleteNode(
               {
                  priorId: this.getId(change.priorElement),
                  priorNodeVersion: change.priorElement.version,
                  priorNodeContents: this.getValueFromElement(change.priorElement),
                  deletingNodeVersion: change.deletingElement.version,
                  changetag: change.changetag
               },
               function(updatedNode)
               {
                  change.priorElement.version = updatedNode.v;
                  runNextChange();
               },
               handleError);
         }

         else
            alert(change.action + " is not a known change type");
      }
      catch (exception)
      {
         alert("Exception when sending update to the server:\n", exception);
      }
   },

   updateNode: function(element)
   {
      // Ignore this request if there is already a like pending change in the queue
      var merged = false;
      this.changeQueue.each(function(change)
      {
         if (!merged)
            merged = change.tryMerge(element);
      });

      if (merged)
         return;

      // Create the change
      var change =
      {
         action: "update",
         element: element,
         changetag: "tag" + Math.floor(Math.random()*99999999999),

         tryMerge: function(element)
         {
            return this.element == element;
         }
      };

      if (!element.changetags)
         element.changetags = {};
      element.changetags[change.changetag] = true;

      this.queueOrRunChange(change);
   },

   insertNode: function(priorElement, newElement)
   {
      // Create the change
      var change =
      {
         action: "insert",
         priorElement: priorElement,
         newElement: newElement,
         changetag: "tag" + Math.floor(Math.random()*99999999999),

         tryMerge: function(element)
         {
            // Insert will always carry with it changes to either node, so there's no need to queue an update
            return (this.newElement == newElement) || (this.priorElement == element);
         }
      };

      if (!priorElement.changetags)
         priorElement.changetags = {};
      priorElement.changetags[change.changetag] = true;

      if (!newElement.changetags)
         newElement.changetags = {};
      newElement.changetags[change.changetag] = true;

      this.queueOrRunChange(change);
   },

   deleteNode: function(priorElement, deletingElement)
   {
      // Create the change
      var change =
      {
         action: "delete",
         priorElement: priorElement,
         deletingElement: deletingElement,
         changetag: "tag" + Math.floor(Math.random()*99999999999),

         tryMerge: function(element)
         {
            // Insert will always carry with it changes to either node, so there's no need to queue an update
            return this.priorElement == element;
         }
      };

      if (!priorElement.changetags)
         priorElement.changetags = {};
      priorElement.changetags[change.changetag] = true;

      this.queueOrRunChange(change);
   },

   handleIncomingNotification: function(notification)
   {
      if (null == notification.changeData)
      {
         // Sometimes change data is null.  This is the case when a permission is added
         return;
      }

      var changeData;

      try
      {
         changeData = eval('(' + notification.changeData + ')');
      }
      catch (exception)
      {
         // swallow the exception for now because some change data can't be evaled
         //alert("Exception when getting an incoming change: " + exception + "\n" + Object.toJSON(notification));
         return;
      }

      if (null == changeData)
         alert("Null change data:\n" + Object.toJSON(notification));

      // Handle the different kinds of changes that can occur
      if (changeData.action == "update")
         this.update(changeData.name, eval('(' + changeData.value + ')'));
      else if (changeData.action == "add")
         this.update(changeData.name, eval('(' + changeData.value + ')'));
      else if (changeData.action == "delete")
         this.doDelete(changeData.name);
   },

   setValueOnElement: function(element, node)
   {
      element.version = node.v;

      if (0 == node.c.indexOf("@@"))
         element.value = node.c.substring(1);

      else if (0 == node.c.indexOf("@{"))
         try
         {
            element.value = node.c;
   
            var command = eval('(' + node.c.substring(1) + ')');

            if ("usertimestamp" == command.t)
               element.innerHTML = command.u.escapeHTML() + ": (" + new Date(command.d) + ")";

            else
               element.innerHTML = node.c.escapeHTML();
         }
         catch (exception)
         {
            var message = "error: " + exception;
            element.innerHTML = message.escapeHTML();
         }

      else
      {
         element.value = node.c;
         element.size = node.c.length + 1;
      }
   },

   deletedIds: {},

   update: function(id, updateEvent)
   {
      var element = $(this.getElementId(id));

      // If the element isn't on the screen, add it
      if (!element)
      {
         // If this is a stale notification for a deleted node, then ignore it
         if (this.deletedIds[id])
            return;

         element = this.createElement(updateEvent, id)

         if (null != updateEvent.n)
         {
            var nextElement = $(this.getElementId(updateEvent.n));
            if (nextElement)
               this.viewerDiv.insertBefore(element, nextElement);
         }
         else
            this.viewerDiv.appendChild(element);
      }
      else
      {
         if (!element.version)
            element.version = (-1 * Infinity);

         // If this change originated from here, update the changetag, else update the element
         if (true == element.changetags[updateEvent.t])
            element.changetags[updateEvent.t] = null;

         // Else if this change originated elsewhere and it's a newer version then what's displayed
         else if (element.version < updateEvent.v)
         {
            this.setValueOnElement(element, updateEvent);
            element.next = updateEvent.n;

            var nextElement = $(this.getElementId(updateEvent.n));
            if (nextElement)
               this.viewerDiv.insertBefore(nextElement, element.nextSibling);
         }
      }
   },

   // Argh...  Safari borks if I call this delete...
   doDelete: function(id)
   {
      this.deletedIds[id] = true;

      var element = $(this.getElementId(id));
      if (element)
         element.remove();
   }
}