(function(){const w=document.createElement("link").relList;if(w&&w.supports&&w.supports("modulepreload"))return;for(const j of document.querySelectorAll('link[rel="modulepreload"]'))Q(j);new MutationObserver(j=>{for(const x of j)if(x.type==="childList")for(const X of x.addedNodes)X.tagName==="LINK"&&X.rel==="modulepreload"&&Q(X)}).observe(document,{childList:!0,subtree:!0});function h(j){const x={};return j.integrity&&(x.integrity=j.integrity),j.referrerPolicy&&(x.referrerPolicy=j.referrerPolicy),j.crossOrigin==="use-credentials"?x.credentials="include":j.crossOrigin==="anonymous"?x.credentials="omit":x.credentials="same-origin",x}function Q(j){if(j.ep)return;j.ep=!0;const x=h(j);fetch(j.href,x)}})();var ji={exports:{}},ms={},Gi={exports:{}},Ie={};/**
 * @license React
 * react.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var od;function zp(){if(od)return Ie;od=1;var d=Symbol.for("react.element"),w=Symbol.for("react.portal"),h=Symbol.for("react.fragment"),Q=Symbol.for("react.strict_mode"),j=Symbol.for("react.profiler"),x=Symbol.for("react.provider"),X=Symbol.for("react.context"),de=Symbol.for("react.forward_ref"),te=Symbol.for("react.suspense"),ve=Symbol.for("react.memo"),ke=Symbol.for("react.lazy"),he=Symbol.iterator;function Ce(y){return y===null||typeof y!="object"?null:(y=he&&y[he]||y["@@iterator"],typeof y=="function"?y:null)}var W={isMounted:function(){return!1},enqueueForceUpdate:function(){},enqueueReplaceState:function(){},enqueueSetState:function(){}},k=Object.assign,be={};function Ae(y,E,ne){this.props=y,this.context=E,this.refs=be,this.updater=ne||W}Ae.prototype.isReactComponent={},Ae.prototype.setState=function(y,E){if(typeof y!="object"&&typeof y!="function"&&y!=null)throw Error("setState(...): takes an object of state variables to update or a function which returns an object of state variables.");this.updater.enqueueSetState(this,y,E,"setState")},Ae.prototype.forceUpdate=function(y){this.updater.enqueueForceUpdate(this,y,"forceUpdate")};function Oe(){}Oe.prototype=Ae.prototype;function kt(y,E,ne){this.props=y,this.context=E,this.refs=be,this.updater=ne||W}var at=kt.prototype=new Oe;at.constructor=kt,k(at,Ae.prototype),at.isPureReactComponent=!0;var O=Array.isArray,V=Object.prototype.hasOwnProperty,pe={current:null},me={key:!0,ref:!0,__self:!0,__source:!0};function Re(y,E,ne){var ge,ae={},Ee=null,Se=null;if(E!=null)for(ge in E.ref!==void 0&&(Se=E.ref),E.key!==void 0&&(Ee=""+E.key),E)V.call(E,ge)&&!me.hasOwnProperty(ge)&&(ae[ge]=E[ge]);var Le=arguments.length-2;if(Le===1)ae.children=ne;else if(1<Le){for(var Ve=Array(Le),Pt=0;Pt<Le;Pt++)Ve[Pt]=arguments[Pt+2];ae.children=Ve}if(y&&y.defaultProps)for(ge in Le=y.defaultProps,Le)ae[ge]===void 0&&(ae[ge]=Le[ge]);return{$$typeof:d,type:y,key:Ee,ref:Se,props:ae,_owner:pe.current}}function Ge(y,E){return{$$typeof:d,type:y.type,key:E,ref:y.ref,props:y.props,_owner:y._owner}}function Pe(y){return typeof y=="object"&&y!==null&&y.$$typeof===d}function We(y){var E={"=":"=0",":":"=2"};return"$"+y.replace(/[=:]/g,function(ne){return E[ne]})}var ct=/\/+/g;function Z(y,E){return typeof y=="object"&&y!==null&&y.key!=null?We(""+y.key):E.toString(36)}function L(y,E,ne,ge,ae){var Ee=typeof y;(Ee==="undefined"||Ee==="boolean")&&(y=null);var Se=!1;if(y===null)Se=!0;else switch(Ee){case"string":case"number":Se=!0;break;case"object":switch(y.$$typeof){case d:case w:Se=!0}}if(Se)return Se=y,ae=ae(Se),y=ge===""?"."+Z(Se,0):ge,O(ae)?(ne="",y!=null&&(ne=y.replace(ct,"$&/")+"/"),L(ae,E,ne,"",function(Pt){return Pt})):ae!=null&&(Pe(ae)&&(ae=Ge(ae,ne+(!ae.key||Se&&Se.key===ae.key?"":(""+ae.key).replace(ct,"$&/")+"/")+y)),E.push(ae)),1;if(Se=0,ge=ge===""?".":ge+":",O(y))for(var Le=0;Le<y.length;Le++){Ee=y[Le];var Ve=ge+Z(Ee,Le);Se+=L(Ee,E,ne,Ve,ae)}else if(Ve=Ce(y),typeof Ve=="function")for(y=Ve.call(y),Le=0;!(Ee=y.next()).done;)Ee=Ee.value,Ve=ge+Z(Ee,Le++),Se+=L(Ee,E,ne,Ve,ae);else if(Ee==="object")throw E=String(y),Error("Objects are not valid as a React child (found: "+(E==="[object Object]"?"object with keys {"+Object.keys(y).join(", ")+"}":E)+"). If you meant to render a collection of children, use an array instead.");return Se}function we(y,E,ne){if(y==null)return y;var ge=[],ae=0;return L(y,ge,"","",function(Ee){return E.call(ne,Ee,ae++)}),ge}function Be(y){if(y._status===-1){var E=y._result;E=E(),E.then(function(ne){(y._status===0||y._status===-1)&&(y._status=1,y._result=ne)},function(ne){(y._status===0||y._status===-1)&&(y._status=2,y._result=ne)}),y._status===-1&&(y._status=0,y._result=E)}if(y._status===1)return y._result.default;throw y._result}var Fe={current:null},B={transition:null},se={ReactCurrentDispatcher:Fe,ReactCurrentBatchConfig:B,ReactCurrentOwner:pe};function M(){throw Error("act(...) is not supported in production builds of React.")}return Ie.Children={map:we,forEach:function(y,E,ne){we(y,function(){E.apply(this,arguments)},ne)},count:function(y){var E=0;return we(y,function(){E++}),E},toArray:function(y){return we(y,function(E){return E})||[]},only:function(y){if(!Pe(y))throw Error("React.Children.only expected to receive a single React element child.");return y}},Ie.Component=Ae,Ie.Fragment=h,Ie.Profiler=j,Ie.PureComponent=kt,Ie.StrictMode=Q,Ie.Suspense=te,Ie.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED=se,Ie.act=M,Ie.cloneElement=function(y,E,ne){if(y==null)throw Error("React.cloneElement(...): The argument must be a React element, but you passed "+y+".");var ge=k({},y.props),ae=y.key,Ee=y.ref,Se=y._owner;if(E!=null){if(E.ref!==void 0&&(Ee=E.ref,Se=pe.current),E.key!==void 0&&(ae=""+E.key),y.type&&y.type.defaultProps)var Le=y.type.defaultProps;for(Ve in E)V.call(E,Ve)&&!me.hasOwnProperty(Ve)&&(ge[Ve]=E[Ve]===void 0&&Le!==void 0?Le[Ve]:E[Ve])}var Ve=arguments.length-2;if(Ve===1)ge.children=ne;else if(1<Ve){Le=Array(Ve);for(var Pt=0;Pt<Ve;Pt++)Le[Pt]=arguments[Pt+2];ge.children=Le}return{$$typeof:d,type:y.type,key:ae,ref:Ee,props:ge,_owner:Se}},Ie.createContext=function(y){return y={$$typeof:X,_currentValue:y,_currentValue2:y,_threadCount:0,Provider:null,Consumer:null,_defaultValue:null,_globalName:null},y.Provider={$$typeof:x,_context:y},y.Consumer=y},Ie.createElement=Re,Ie.createFactory=function(y){var E=Re.bind(null,y);return E.type=y,E},Ie.createRef=function(){return{current:null}},Ie.forwardRef=function(y){return{$$typeof:de,render:y}},Ie.isValidElement=Pe,Ie.lazy=function(y){return{$$typeof:ke,_payload:{_status:-1,_result:y},_init:Be}},Ie.memo=function(y,E){return{$$typeof:ve,type:y,compare:E===void 0?null:E}},Ie.startTransition=function(y){var E=B.transition;B.transition={};try{y()}finally{B.transition=E}},Ie.unstable_act=M,Ie.useCallback=function(y,E){return Fe.current.useCallback(y,E)},Ie.useContext=function(y){return Fe.current.useContext(y)},Ie.useDebugValue=function(){},Ie.useDeferredValue=function(y){return Fe.current.useDeferredValue(y)},Ie.useEffect=function(y,E){return Fe.current.useEffect(y,E)},Ie.useId=function(){return Fe.current.useId()},Ie.useImperativeHandle=function(y,E,ne){return Fe.current.useImperativeHandle(y,E,ne)},Ie.useInsertionEffect=function(y,E){return Fe.current.useInsertionEffect(y,E)},Ie.useLayoutEffect=function(y,E){return Fe.current.useLayoutEffect(y,E)},Ie.useMemo=function(y,E){return Fe.current.useMemo(y,E)},Ie.useReducer=function(y,E,ne){return Fe.current.useReducer(y,E,ne)},Ie.useRef=function(y){return Fe.current.useRef(y)},Ie.useState=function(y){return Fe.current.useState(y)},Ie.useSyncExternalStore=function(y,E,ne){return Fe.current.useSyncExternalStore(y,E,ne)},Ie.useTransition=function(){return Fe.current.useTransition()},Ie.version="18.3.1",Ie}var id;function Vi(){return id||(id=1,Gi.exports=zp()),Gi.exports}/**
 * @license React
 * react-jsx-runtime.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var ld;function Yp(){if(ld)return ms;ld=1;var d=Vi(),w=Symbol.for("react.element"),h=Symbol.for("react.fragment"),Q=Object.prototype.hasOwnProperty,j=d.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED.ReactCurrentOwner,x={key:!0,ref:!0,__self:!0,__source:!0};function X(de,te,ve){var ke,he={},Ce=null,W=null;ve!==void 0&&(Ce=""+ve),te.key!==void 0&&(Ce=""+te.key),te.ref!==void 0&&(W=te.ref);for(ke in te)Q.call(te,ke)&&!x.hasOwnProperty(ke)&&(he[ke]=te[ke]);if(de&&de.defaultProps)for(ke in te=de.defaultProps,te)he[ke]===void 0&&(he[ke]=te[ke]);return{$$typeof:w,type:de,key:Ce,ref:W,props:he,_owner:j.current}}return ms.Fragment=h,ms.jsx=X,ms.jsxs=X,ms}var cd;function Vp(){return cd||(cd=1,ji.exports=Yp()),ji.exports}var l=Vp(),yo={},Hi={exports:{}},an={},Wi={exports:{}},Ui={};/**
 * @license React
 * scheduler.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var dd;function $p(){return dd||(dd=1,(function(d){function w(B,se){var M=B.length;B.push(se);e:for(;0<M;){var y=M-1>>>1,E=B[y];if(0<j(E,se))B[y]=se,B[M]=E,M=y;else break e}}function h(B){return B.length===0?null:B[0]}function Q(B){if(B.length===0)return null;var se=B[0],M=B.pop();if(M!==se){B[0]=M;e:for(var y=0,E=B.length,ne=E>>>1;y<ne;){var ge=2*(y+1)-1,ae=B[ge],Ee=ge+1,Se=B[Ee];if(0>j(ae,M))Ee<E&&0>j(Se,ae)?(B[y]=Se,B[Ee]=M,y=Ee):(B[y]=ae,B[ge]=M,y=ge);else if(Ee<E&&0>j(Se,M))B[y]=Se,B[Ee]=M,y=Ee;else break e}}return se}function j(B,se){var M=B.sortIndex-se.sortIndex;return M!==0?M:B.id-se.id}if(typeof performance=="object"&&typeof performance.now=="function"){var x=performance;d.unstable_now=function(){return x.now()}}else{var X=Date,de=X.now();d.unstable_now=function(){return X.now()-de}}var te=[],ve=[],ke=1,he=null,Ce=3,W=!1,k=!1,be=!1,Ae=typeof setTimeout=="function"?setTimeout:null,Oe=typeof clearTimeout=="function"?clearTimeout:null,kt=typeof setImmediate<"u"?setImmediate:null;typeof navigator<"u"&&navigator.scheduling!==void 0&&navigator.scheduling.isInputPending!==void 0&&navigator.scheduling.isInputPending.bind(navigator.scheduling);function at(B){for(var se=h(ve);se!==null;){if(se.callback===null)Q(ve);else if(se.startTime<=B)Q(ve),se.sortIndex=se.expirationTime,w(te,se);else break;se=h(ve)}}function O(B){if(be=!1,at(B),!k)if(h(te)!==null)k=!0,Be(V);else{var se=h(ve);se!==null&&Fe(O,se.startTime-B)}}function V(B,se){k=!1,be&&(be=!1,Oe(Re),Re=-1),W=!0;var M=Ce;try{for(at(se),he=h(te);he!==null&&(!(he.expirationTime>se)||B&&!We());){var y=he.callback;if(typeof y=="function"){he.callback=null,Ce=he.priorityLevel;var E=y(he.expirationTime<=se);se=d.unstable_now(),typeof E=="function"?he.callback=E:he===h(te)&&Q(te),at(se)}else Q(te);he=h(te)}if(he!==null)var ne=!0;else{var ge=h(ve);ge!==null&&Fe(O,ge.startTime-se),ne=!1}return ne}finally{he=null,Ce=M,W=!1}}var pe=!1,me=null,Re=-1,Ge=5,Pe=-1;function We(){return!(d.unstable_now()-Pe<Ge)}function ct(){if(me!==null){var B=d.unstable_now();Pe=B;var se=!0;try{se=me(!0,B)}finally{se?Z():(pe=!1,me=null)}}else pe=!1}var Z;if(typeof kt=="function")Z=function(){kt(ct)};else if(typeof MessageChannel<"u"){var L=new MessageChannel,we=L.port2;L.port1.onmessage=ct,Z=function(){we.postMessage(null)}}else Z=function(){Ae(ct,0)};function Be(B){me=B,pe||(pe=!0,Z())}function Fe(B,se){Re=Ae(function(){B(d.unstable_now())},se)}d.unstable_IdlePriority=5,d.unstable_ImmediatePriority=1,d.unstable_LowPriority=4,d.unstable_NormalPriority=3,d.unstable_Profiling=null,d.unstable_UserBlockingPriority=2,d.unstable_cancelCallback=function(B){B.callback=null},d.unstable_continueExecution=function(){k||W||(k=!0,Be(V))},d.unstable_forceFrameRate=function(B){0>B||125<B?console.error("forceFrameRate takes a positive int between 0 and 125, forcing frame rates higher than 125 fps is not supported"):Ge=0<B?Math.floor(1e3/B):5},d.unstable_getCurrentPriorityLevel=function(){return Ce},d.unstable_getFirstCallbackNode=function(){return h(te)},d.unstable_next=function(B){switch(Ce){case 1:case 2:case 3:var se=3;break;default:se=Ce}var M=Ce;Ce=se;try{return B()}finally{Ce=M}},d.unstable_pauseExecution=function(){},d.unstable_requestPaint=function(){},d.unstable_runWithPriority=function(B,se){switch(B){case 1:case 2:case 3:case 4:case 5:break;default:B=3}var M=Ce;Ce=B;try{return se()}finally{Ce=M}},d.unstable_scheduleCallback=function(B,se,M){var y=d.unstable_now();switch(typeof M=="object"&&M!==null?(M=M.delay,M=typeof M=="number"&&0<M?y+M:y):M=y,B){case 1:var E=-1;break;case 2:E=250;break;case 5:E=1073741823;break;case 4:E=1e4;break;default:E=5e3}return E=M+E,B={id:ke++,callback:se,priorityLevel:B,startTime:M,expirationTime:E,sortIndex:-1},M>y?(B.sortIndex=M,w(ve,B),h(te)===null&&B===h(ve)&&(be?(Oe(Re),Re=-1):be=!0,Fe(O,M-y))):(B.sortIndex=E,w(te,B),k||W||(k=!0,Be(V))),B},d.unstable_shouldYield=We,d.unstable_wrapCallback=function(B){var se=Ce;return function(){var M=Ce;Ce=se;try{return B.apply(this,arguments)}finally{Ce=M}}}})(Ui)),Ui}var pd;function Xp(){return pd||(pd=1,Wi.exports=$p()),Wi.exports}/**
 * @license React
 * react-dom.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var ud;function Jp(){if(ud)return an;ud=1;var d=Vi(),w=Xp();function h(e){for(var t="https://reactjs.org/docs/error-decoder.html?invariant="+e,n=1;n<arguments.length;n++)t+="&args[]="+encodeURIComponent(arguments[n]);return"Minified React error #"+e+"; visit "+t+" for the full message or use the non-minified dev environment for full errors and additional helpful warnings."}var Q=new Set,j={};function x(e,t){X(e,t),X(e+"Capture",t)}function X(e,t){for(j[e]=t,e=0;e<t.length;e++)Q.add(t[e])}var de=!(typeof window>"u"||typeof window.document>"u"||typeof window.document.createElement>"u"),te=Object.prototype.hasOwnProperty,ve=/^[:A-Z_a-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD][:A-Z_a-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD\-.0-9\u00B7\u0300-\u036F\u203F-\u2040]*$/,ke={},he={};function Ce(e){return te.call(he,e)?!0:te.call(ke,e)?!1:ve.test(e)?he[e]=!0:(ke[e]=!0,!1)}function W(e,t,n,a){if(n!==null&&n.type===0)return!1;switch(typeof t){case"function":case"symbol":return!0;case"boolean":return a?!1:n!==null?!n.acceptsBooleans:(e=e.toLowerCase().slice(0,5),e!=="data-"&&e!=="aria-");default:return!1}}function k(e,t,n,a){if(t===null||typeof t>"u"||W(e,t,n,a))return!0;if(a)return!1;if(n!==null)switch(n.type){case 3:return!t;case 4:return t===!1;case 5:return isNaN(t);case 6:return isNaN(t)||1>t}return!1}function be(e,t,n,a,r,s,i){this.acceptsBooleans=t===2||t===3||t===4,this.attributeName=a,this.attributeNamespace=r,this.mustUseProperty=n,this.propertyName=e,this.type=t,this.sanitizeURL=s,this.removeEmptyString=i}var Ae={};"children dangerouslySetInnerHTML defaultValue defaultChecked innerHTML suppressContentEditableWarning suppressHydrationWarning style".split(" ").forEach(function(e){Ae[e]=new be(e,0,!1,e,null,!1,!1)}),[["acceptCharset","accept-charset"],["className","class"],["htmlFor","for"],["httpEquiv","http-equiv"]].forEach(function(e){var t=e[0];Ae[t]=new be(t,1,!1,e[1],null,!1,!1)}),["contentEditable","draggable","spellCheck","value"].forEach(function(e){Ae[e]=new be(e,2,!1,e.toLowerCase(),null,!1,!1)}),["autoReverse","externalResourcesRequired","focusable","preserveAlpha"].forEach(function(e){Ae[e]=new be(e,2,!1,e,null,!1,!1)}),"allowFullScreen async autoFocus autoPlay controls default defer disabled disablePictureInPicture disableRemotePlayback formNoValidate hidden loop noModule noValidate open playsInline readOnly required reversed scoped seamless itemScope".split(" ").forEach(function(e){Ae[e]=new be(e,3,!1,e.toLowerCase(),null,!1,!1)}),["checked","multiple","muted","selected"].forEach(function(e){Ae[e]=new be(e,3,!0,e,null,!1,!1)}),["capture","download"].forEach(function(e){Ae[e]=new be(e,4,!1,e,null,!1,!1)}),["cols","rows","size","span"].forEach(function(e){Ae[e]=new be(e,6,!1,e,null,!1,!1)}),["rowSpan","start"].forEach(function(e){Ae[e]=new be(e,5,!1,e.toLowerCase(),null,!1,!1)});var Oe=/[\-:]([a-z])/g;function kt(e){return e[1].toUpperCase()}"accent-height alignment-baseline arabic-form baseline-shift cap-height clip-path clip-rule color-interpolation color-interpolation-filters color-profile color-rendering dominant-baseline enable-background fill-opacity fill-rule flood-color flood-opacity font-family font-size font-size-adjust font-stretch font-style font-variant font-weight glyph-name glyph-orientation-horizontal glyph-orientation-vertical horiz-adv-x horiz-origin-x image-rendering letter-spacing lighting-color marker-end marker-mid marker-start overline-position overline-thickness paint-order panose-1 pointer-events rendering-intent shape-rendering stop-color stop-opacity strikethrough-position strikethrough-thickness stroke-dasharray stroke-dashoffset stroke-linecap stroke-linejoin stroke-miterlimit stroke-opacity stroke-width text-anchor text-decoration text-rendering underline-position underline-thickness unicode-bidi unicode-range units-per-em v-alphabetic v-hanging v-ideographic v-mathematical vector-effect vert-adv-y vert-origin-x vert-origin-y word-spacing writing-mode xmlns:xlink x-height".split(" ").forEach(function(e){var t=e.replace(Oe,kt);Ae[t]=new be(t,1,!1,e,null,!1,!1)}),"xlink:actuate xlink:arcrole xlink:role xlink:show xlink:title xlink:type".split(" ").forEach(function(e){var t=e.replace(Oe,kt);Ae[t]=new be(t,1,!1,e,"http://www.w3.org/1999/xlink",!1,!1)}),["xml:base","xml:lang","xml:space"].forEach(function(e){var t=e.replace(Oe,kt);Ae[t]=new be(t,1,!1,e,"http://www.w3.org/XML/1998/namespace",!1,!1)}),["tabIndex","crossOrigin"].forEach(function(e){Ae[e]=new be(e,1,!1,e.toLowerCase(),null,!1,!1)}),Ae.xlinkHref=new be("xlinkHref",1,!1,"xlink:href","http://www.w3.org/1999/xlink",!0,!1),["src","href","action","formAction"].forEach(function(e){Ae[e]=new be(e,1,!1,e.toLowerCase(),null,!0,!0)});function at(e,t,n,a){var r=Ae.hasOwnProperty(t)?Ae[t]:null;(r!==null?r.type!==0:a||!(2<t.length)||t[0]!=="o"&&t[0]!=="O"||t[1]!=="n"&&t[1]!=="N")&&(k(t,n,r,a)&&(n=null),a||r===null?Ce(t)&&(n===null?e.removeAttribute(t):e.setAttribute(t,""+n)):r.mustUseProperty?e[r.propertyName]=n===null?r.type===3?!1:"":n:(t=r.attributeName,a=r.attributeNamespace,n===null?e.removeAttribute(t):(r=r.type,n=r===3||r===4&&n===!0?"":""+n,a?e.setAttributeNS(a,t,n):e.setAttribute(t,n))))}var O=d.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED,V=Symbol.for("react.element"),pe=Symbol.for("react.portal"),me=Symbol.for("react.fragment"),Re=Symbol.for("react.strict_mode"),Ge=Symbol.for("react.profiler"),Pe=Symbol.for("react.provider"),We=Symbol.for("react.context"),ct=Symbol.for("react.forward_ref"),Z=Symbol.for("react.suspense"),L=Symbol.for("react.suspense_list"),we=Symbol.for("react.memo"),Be=Symbol.for("react.lazy"),Fe=Symbol.for("react.offscreen"),B=Symbol.iterator;function se(e){return e===null||typeof e!="object"?null:(e=B&&e[B]||e["@@iterator"],typeof e=="function"?e:null)}var M=Object.assign,y;function E(e){if(y===void 0)try{throw Error()}catch(n){var t=n.stack.trim().match(/\n( *(at )?)/);y=t&&t[1]||""}return`
`+y+e}var ne=!1;function ge(e,t){if(!e||ne)return"";ne=!0;var n=Error.prepareStackTrace;Error.prepareStackTrace=void 0;try{if(t)if(t=function(){throw Error()},Object.defineProperty(t.prototype,"props",{set:function(){throw Error()}}),typeof Reflect=="object"&&Reflect.construct){try{Reflect.construct(t,[])}catch(b){var a=b}Reflect.construct(e,[],t)}else{try{t.call()}catch(b){a=b}e.call(t.prototype)}else{try{throw Error()}catch(b){a=b}e()}}catch(b){if(b&&a&&typeof b.stack=="string"){for(var r=b.stack.split(`
`),s=a.stack.split(`
`),i=r.length-1,c=s.length-1;1<=i&&0<=c&&r[i]!==s[c];)c--;for(;1<=i&&0<=c;i--,c--)if(r[i]!==s[c]){if(i!==1||c!==1)do if(i--,c--,0>c||r[i]!==s[c]){var u=`
`+r[i].replace(" at new "," at ");return e.displayName&&u.includes("<anonymous>")&&(u=u.replace("<anonymous>",e.displayName)),u}while(1<=i&&0<=c);break}}}finally{ne=!1,Error.prepareStackTrace=n}return(e=e?e.displayName||e.name:"")?E(e):""}function ae(e){switch(e.tag){case 5:return E(e.type);case 16:return E("Lazy");case 13:return E("Suspense");case 19:return E("SuspenseList");case 0:case 2:case 15:return e=ge(e.type,!1),e;case 11:return e=ge(e.type.render,!1),e;case 1:return e=ge(e.type,!0),e;default:return""}}function Ee(e){if(e==null)return null;if(typeof e=="function")return e.displayName||e.name||null;if(typeof e=="string")return e;switch(e){case me:return"Fragment";case pe:return"Portal";case Ge:return"Profiler";case Re:return"StrictMode";case Z:return"Suspense";case L:return"SuspenseList"}if(typeof e=="object")switch(e.$$typeof){case We:return(e.displayName||"Context")+".Consumer";case Pe:return(e._context.displayName||"Context")+".Provider";case ct:var t=e.render;return e=e.displayName,e||(e=t.displayName||t.name||"",e=e!==""?"ForwardRef("+e+")":"ForwardRef"),e;case we:return t=e.displayName||null,t!==null?t:Ee(e.type)||"Memo";case Be:t=e._payload,e=e._init;try{return Ee(e(t))}catch{}}return null}function Se(e){var t=e.type;switch(e.tag){case 24:return"Cache";case 9:return(t.displayName||"Context")+".Consumer";case 10:return(t._context.displayName||"Context")+".Provider";case 18:return"DehydratedFragment";case 11:return e=t.render,e=e.displayName||e.name||"",t.displayName||(e!==""?"ForwardRef("+e+")":"ForwardRef");case 7:return"Fragment";case 5:return t;case 4:return"Portal";case 3:return"Root";case 6:return"Text";case 16:return Ee(t);case 8:return t===Re?"StrictMode":"Mode";case 22:return"Offscreen";case 12:return"Profiler";case 21:return"Scope";case 13:return"Suspense";case 19:return"SuspenseList";case 25:return"TracingMarker";case 1:case 0:case 17:case 2:case 14:case 15:if(typeof t=="function")return t.displayName||t.name||null;if(typeof t=="string")return t}return null}function Le(e){switch(typeof e){case"boolean":case"number":case"string":case"undefined":return e;case"object":return e;default:return""}}function Ve(e){var t=e.type;return(e=e.nodeName)&&e.toLowerCase()==="input"&&(t==="checkbox"||t==="radio")}function Pt(e){var t=Ve(e)?"checked":"value",n=Object.getOwnPropertyDescriptor(e.constructor.prototype,t),a=""+e[t];if(!e.hasOwnProperty(t)&&typeof n<"u"&&typeof n.get=="function"&&typeof n.set=="function"){var r=n.get,s=n.set;return Object.defineProperty(e,t,{configurable:!0,get:function(){return r.call(this)},set:function(i){a=""+i,s.call(this,i)}}),Object.defineProperty(e,t,{enumerable:n.enumerable}),{getValue:function(){return a},setValue:function(i){a=""+i},stopTracking:function(){e._valueTracker=null,delete e[t]}}}}function Gn(e){e._valueTracker||(e._valueTracker=Pt(e))}function Rr(e){if(!e)return!1;var t=e._valueTracker;if(!t)return!0;var n=t.getValue(),a="";return e&&(a=Ve(e)?e.checked?"true":"false":e.value),e=a,e!==n?(t.setValue(e),!0):!1}function Hn(e){if(e=e||(typeof document<"u"?document:void 0),typeof e>"u")return null;try{return e.activeElement||e.body}catch{return e.body}}function xr(e,t){var n=t.checked;return M({},t,{defaultChecked:void 0,defaultValue:void 0,value:void 0,checked:n??e._wrapperState.initialChecked})}function fs(e,t){var n=t.defaultValue==null?"":t.defaultValue,a=t.checked!=null?t.checked:t.defaultChecked;n=Le(t.value!=null?t.value:n),e._wrapperState={initialChecked:a,initialValue:n,controlled:t.type==="checkbox"||t.type==="radio"?t.checked!=null:t.value!=null}}function Ja(e,t){t=t.checked,t!=null&&at(e,"checked",t,!1)}function _a(e,t){Ja(e,t);var n=Le(t.value),a=t.type;if(n!=null)a==="number"?(n===0&&e.value===""||e.value!=n)&&(e.value=""+n):e.value!==""+n&&(e.value=""+n);else if(a==="submit"||a==="reset"){e.removeAttribute("value");return}t.hasOwnProperty("value")?Pr(e,t.type,n):t.hasOwnProperty("defaultValue")&&Pr(e,t.type,Le(t.defaultValue)),t.checked==null&&t.defaultChecked!=null&&(e.defaultChecked=!!t.defaultChecked)}function Aa(e,t,n){if(t.hasOwnProperty("value")||t.hasOwnProperty("defaultValue")){var a=t.type;if(!(a!=="submit"&&a!=="reset"||t.value!==void 0&&t.value!==null))return;t=""+e._wrapperState.initialValue,n||t===e.value||(e.value=t),e.defaultValue=t}n=e.name,n!==""&&(e.name=""),e.defaultChecked=!!e._wrapperState.initialChecked,n!==""&&(e.name=n)}function Pr(e,t,n){(t!=="number"||Hn(e.ownerDocument)!==e)&&(n==null?e.defaultValue=""+e._wrapperState.initialValue:e.defaultValue!==""+n&&(e.defaultValue=""+n))}var Ia=Array.isArray;function ra(e,t,n,a){if(e=e.options,t){t={};for(var r=0;r<n.length;r++)t["$"+n[r]]=!0;for(n=0;n<e.length;n++)r=t.hasOwnProperty("$"+e[n].value),e[n].selected!==r&&(e[n].selected=r),r&&a&&(e[n].defaultSelected=!0)}else{for(n=""+Le(n),t=null,r=0;r<e.length;r++){if(e[r].value===n){e[r].selected=!0,a&&(e[r].defaultSelected=!0);return}t!==null||e[r].disabled||(t=e[r])}t!==null&&(t.selected=!0)}}function Lt(e,t){if(t.dangerouslySetInnerHTML!=null)throw Error(h(91));return M({},t,{value:void 0,defaultValue:void 0,children:""+e._wrapperState.initialValue})}function dn(e,t){var n=t.value;if(n==null){if(n=t.children,t=t.defaultValue,n!=null){if(t!=null)throw Error(h(92));if(Ia(n)){if(1<n.length)throw Error(h(93));n=n[0]}t=n}t==null&&(t=""),n=t}e._wrapperState={initialValue:Le(n)}}function vn(e,t){var n=Le(t.value),a=Le(t.defaultValue);n!=null&&(n=""+n,n!==e.value&&(e.value=n),t.defaultValue==null&&e.defaultValue!==n&&(e.defaultValue=n)),a!=null&&(e.defaultValue=""+a)}function ce(e){var t=e.textContent;t===e._wrapperState.initialValue&&t!==""&&t!==null&&(e.value=t)}function Za(e){switch(e){case"svg":return"http://www.w3.org/2000/svg";case"math":return"http://www.w3.org/1998/Math/MathML";default:return"http://www.w3.org/1999/xhtml"}}function rn(e,t){return e==null||e==="http://www.w3.org/1999/xhtml"?Za(t):e==="http://www.w3.org/2000/svg"&&t==="foreignObject"?"http://www.w3.org/1999/xhtml":e}var La,Mr=(function(e){return typeof MSApp<"u"&&MSApp.execUnsafeLocalFunction?function(t,n,a,r){MSApp.execUnsafeLocalFunction(function(){return e(t,n,a,r)})}:e})(function(e,t){if(e.namespaceURI!=="http://www.w3.org/2000/svg"||"innerHTML"in e)e.innerHTML=t;else{for(La=La||document.createElement("div"),La.innerHTML="<svg>"+t.valueOf().toString()+"</svg>",t=La.firstChild;e.firstChild;)e.removeChild(e.firstChild);for(;t.firstChild;)e.appendChild(t.firstChild)}});function Qe(e,t){if(t){var n=e.firstChild;if(n&&n===e.lastChild&&n.nodeType===3){n.nodeValue=t;return}}e.textContent=t}var Wt={animationIterationCount:!0,aspectRatio:!0,borderImageOutset:!0,borderImageSlice:!0,borderImageWidth:!0,boxFlex:!0,boxFlexGroup:!0,boxOrdinalGroup:!0,columnCount:!0,columns:!0,flex:!0,flexGrow:!0,flexPositive:!0,flexShrink:!0,flexNegative:!0,flexOrder:!0,gridArea:!0,gridRow:!0,gridRowEnd:!0,gridRowSpan:!0,gridRowStart:!0,gridColumn:!0,gridColumnEnd:!0,gridColumnSpan:!0,gridColumnStart:!0,fontWeight:!0,lineClamp:!0,lineHeight:!0,opacity:!0,order:!0,orphans:!0,tabSize:!0,widows:!0,zIndex:!0,zoom:!0,fillOpacity:!0,floodOpacity:!0,stopOpacity:!0,strokeDasharray:!0,strokeDashoffset:!0,strokeMiterlimit:!0,strokeOpacity:!0,strokeWidth:!0},fo=["Webkit","ms","Moz","O"];Object.keys(Wt).forEach(function(e){fo.forEach(function(t){t=t+e.charAt(0).toUpperCase()+e.substring(1),Wt[t]=Wt[e]})});function Dr(e,t,n){return t==null||typeof t=="boolean"||t===""?"":n||typeof t!="number"||t===0||Wt.hasOwnProperty(e)&&Wt[e]?(""+t).trim():t+"px"}function vs(e,t){e=e.style;for(var n in t)if(t.hasOwnProperty(n)){var a=n.indexOf("--")===0,r=Dr(n,t[n],a);n==="float"&&(n="cssFloat"),a?e.setProperty(n,r):e[n]=r}}var Nn=M({menuitem:!0},{area:!0,base:!0,br:!0,col:!0,embed:!0,hr:!0,img:!0,input:!0,keygen:!0,link:!0,meta:!0,param:!0,source:!0,track:!0,wbr:!0});function er(e,t){if(t){if(Nn[e]&&(t.children!=null||t.dangerouslySetInnerHTML!=null))throw Error(h(137,e));if(t.dangerouslySetInnerHTML!=null){if(t.children!=null)throw Error(h(60));if(typeof t.dangerouslySetInnerHTML!="object"||!("__html"in t.dangerouslySetInnerHTML))throw Error(h(61))}if(t.style!=null&&typeof t.style!="object")throw Error(h(62))}}function tr(e,t){if(e.indexOf("-")===-1)return typeof t.is=="string";switch(e){case"annotation-xml":case"color-profile":case"font-face":case"font-face-src":case"font-face-uri":case"font-face-format":case"font-face-name":case"missing-glyph":return!1;default:return!0}}var Tt=null;function Or(e){return e=e.target||e.srcElement||window,e.correspondingUseElement&&(e=e.correspondingUseElement),e.nodeType===3?e.parentNode:e}var Br=null,sa=null,Rn=null;function xn(e){if(e=Jr(e)){if(typeof Br!="function")throw Error(h(280));var t=e.stateNode;t&&(t=Ns(t),Br(e.stateNode,e.type,t))}}function bs(e){sa?Rn?Rn.push(e):Rn=[e]:sa=e}function Ct(){if(sa){var e=sa,t=Rn;if(Rn=sa=null,xn(e),t)for(e=0;e<t.length;e++)xn(t[e])}}function Fr(e,t){return e(t)}function gt(){}var nr=!1;function Kr(e,t,n){if(nr)return e(t,n);nr=!0;try{return Fr(e,t,n)}finally{nr=!1,(sa!==null||Rn!==null)&&(gt(),Ct())}}function oa(e,t){var n=e.stateNode;if(n===null)return null;var a=Ns(n);if(a===null)return null;n=a[t];e:switch(t){case"onClick":case"onClickCapture":case"onDoubleClick":case"onDoubleClickCapture":case"onMouseDown":case"onMouseDownCapture":case"onMouseMove":case"onMouseMoveCapture":case"onMouseUp":case"onMouseUpCapture":case"onMouseEnter":(a=!a.disabled)||(e=e.type,a=!(e==="button"||e==="input"||e==="select"||e==="textarea")),e=!a;break e;default:e=!1}if(e)return null;if(n&&typeof n!="function")throw Error(h(231,t,typeof n));return n}var ar=!1;if(de)try{var ia={};Object.defineProperty(ia,"passive",{get:function(){ar=!0}}),window.addEventListener("test",ia,ia),window.removeEventListener("test",ia,ia)}catch{ar=!1}function Na(e,t,n,a,r,s,i,c,u){var b=Array.prototype.slice.call(arguments,3);try{t.apply(n,b)}catch(A){this.onError(A)}}var Ut=!1,Pn=null,Ra=!1,xa=null,Pa={onError:function(e){Ut=!0,Pn=e}};function rr(e,t,n,a,r,s,i,c,u){Ut=!1,Pn=null,Na.apply(Pa,arguments)}function Ma(e,t,n,a,r,s,i,c,u){if(rr.apply(this,arguments),Ut){if(Ut){var b=Pn;Ut=!1,Pn=null}else throw Error(h(198));Ra||(Ra=!0,xa=b)}}function pn(e){var t=e,n=e;if(e.alternate)for(;t.return;)t=t.return;else{e=t;do t=e,(t.flags&4098)!==0&&(n=t.return),e=t.return;while(e)}return t.tag===3?n:null}function Da(e){if(e.tag===13){var t=e.memoizedState;if(t===null&&(e=e.alternate,e!==null&&(t=e.memoizedState)),t!==null)return t.dehydrated}return null}function ws(e){if(pn(e)!==e)throw Error(h(188))}function jr(e){var t=e.alternate;if(!t){if(t=pn(e),t===null)throw Error(h(188));return t!==e?null:e}for(var n=e,a=t;;){var r=n.return;if(r===null)break;var s=r.alternate;if(s===null){if(a=r.return,a!==null){n=a;continue}break}if(r.child===s.child){for(s=r.child;s;){if(s===n)return ws(r),e;if(s===a)return ws(r),t;s=s.sibling}throw Error(h(188))}if(n.return!==a.return)n=r,a=s;else{for(var i=!1,c=r.child;c;){if(c===n){i=!0,n=r,a=s;break}if(c===a){i=!0,a=r,n=s;break}c=c.sibling}if(!i){for(c=s.child;c;){if(c===n){i=!0,n=s,a=r;break}if(c===a){i=!0,a=s,n=r;break}c=c.sibling}if(!i)throw Error(h(189))}}if(n.alternate!==a)throw Error(h(190))}if(n.tag!==3)throw Error(h(188));return n.stateNode.current===n?e:t}function la(e){return e=jr(e),e!==null?o(e):null}function o(e){if(e.tag===5||e.tag===6)return e;for(e=e.child;e!==null;){var t=o(e);if(t!==null)return t;e=e.sibling}return null}var g=w.unstable_scheduleCallback,S=w.unstable_cancelCallback,T=w.unstable_shouldYield,C=w.unstable_requestPaint,p=w.unstable_now,N=w.unstable_getCurrentPriorityLevel,R=w.unstable_ImmediatePriority,G=w.unstable_UserBlockingPriority,D=w.unstable_NormalPriority,z=w.unstable_LowPriority,F=w.unstable_IdlePriority,K=null,re=null;function xe(e){if(re&&typeof re.onCommitFiberRoot=="function")try{re.onCommitFiberRoot(K,e,void 0,(e.current.flags&128)===128)}catch{}}var ue=Math.clz32?Math.clz32:Ne,Me=Math.log,Te=Math.LN2;function Ne(e){return e>>>=0,e===0?32:31-(Me(e)/Te|0)|0}var Ke=64,oe=4194304;function U(e){switch(e&-e){case 1:return 1;case 2:return 2;case 4:return 4;case 8:return 8;case 16:return 16;case 32:return 32;case 64:case 128:case 256:case 512:case 1024:case 2048:case 4096:case 8192:case 16384:case 32768:case 65536:case 131072:case 262144:case 524288:case 1048576:case 2097152:return e&4194240;case 4194304:case 8388608:case 16777216:case 33554432:case 67108864:return e&130023424;case 134217728:return 134217728;case 268435456:return 268435456;case 536870912:return 536870912;case 1073741824:return 1073741824;default:return e}}function ee(e,t){var n=e.pendingLanes;if(n===0)return 0;var a=0,r=e.suspendedLanes,s=e.pingedLanes,i=n&268435455;if(i!==0){var c=i&~r;c!==0?a=U(c):(s&=i,s!==0&&(a=U(s)))}else i=n&~r,i!==0?a=U(i):s!==0&&(a=U(s));if(a===0)return 0;if(t!==0&&t!==a&&(t&r)===0&&(r=a&-a,s=t&-t,r>=s||r===16&&(s&4194240)!==0))return t;if((a&4)!==0&&(a|=n&16),t=e.entangledLanes,t!==0)for(e=e.entanglements,t&=a;0<t;)n=31-ue(t),r=1<<n,a|=e[n],t&=~r;return a}function He(e,t){switch(e){case 1:case 2:case 4:return t+250;case 8:case 16:case 32:case 64:case 128:case 256:case 512:case 1024:case 2048:case 4096:case 8192:case 16384:case 32768:case 65536:case 131072:case 262144:case 524288:case 1048576:case 2097152:return t+5e3;case 4194304:case 8388608:case 16777216:case 33554432:case 67108864:return-1;case 134217728:case 268435456:case 536870912:case 1073741824:return-1;default:return-1}}function Ze(e,t){for(var n=e.suspendedLanes,a=e.pingedLanes,r=e.expirationTimes,s=e.pendingLanes;0<s;){var i=31-ue(s),c=1<<i,u=r[i];u===-1?((c&n)===0||(c&a)!==0)&&(r[i]=He(c,t)):u<=t&&(e.expiredLanes|=c),s&=~c}}function et(e){return e=e.pendingLanes&-1073741825,e!==0?e:e&1073741824?1073741824:0}function Ue(){var e=Ke;return Ke<<=1,(Ke&4194240)===0&&(Ke=64),e}function qe(e){for(var t=[],n=0;31>n;n++)t.push(e);return t}function $e(e,t,n){e.pendingLanes|=t,t!==536870912&&(e.suspendedLanes=0,e.pingedLanes=0),e=e.eventTimes,t=31-ue(t),e[t]=n}function tt(e,t){var n=e.pendingLanes&~t;e.pendingLanes=t,e.suspendedLanes=0,e.pingedLanes=0,e.expiredLanes&=t,e.mutableReadLanes&=t,e.entangledLanes&=t,t=e.entanglements;var a=e.eventTimes;for(e=e.expirationTimes;0<n;){var r=31-ue(n),s=1<<r;t[r]=0,a[r]=-1,e[r]=-1,n&=~s}}function ot(e,t){var n=e.entangledLanes|=t;for(e=e.entanglements;n;){var a=31-ue(n),r=1<<a;r&t|e[a]&t&&(e[a]|=t),n&=~r}}var ye=0;function ze(e){return e&=-e,1<e?4<e?(e&268435455)!==0?16:536870912:4:1}var bt,Mt,sn,Dt,yt,mt=!1,Ye=[],Ot=null,Et=null,Xe=null,bn=new Map,Nt=new Map,ft=[],Xt="mousedown mouseup touchcancel touchend touchstart auxclick dblclick pointercancel pointerdown pointerup dragend dragstart drop compositionend compositionstart keydown keypress keyup input textInput copy cut paste click change contextmenu reset submit".split(" ");function qt(e,t){switch(e){case"focusin":case"focusout":Ot=null;break;case"dragenter":case"dragleave":Et=null;break;case"mouseover":case"mouseout":Xe=null;break;case"pointerover":case"pointerout":bn.delete(t.pointerId);break;case"gotpointercapture":case"lostpointercapture":Nt.delete(t.pointerId)}}function Bt(e,t,n,a,r,s){return e===null||e.nativeEvent!==s?(e={blockedOn:t,domEventName:n,eventSystemFlags:a,nativeEvent:s,targetContainers:[r]},t!==null&&(t=Jr(t),t!==null&&Mt(t)),e):(e.eventSystemFlags|=a,t=e.targetContainers,r!==null&&t.indexOf(r)===-1&&t.push(r),e)}function wn(e,t,n,a,r){switch(t){case"focusin":return Ot=Bt(Ot,e,t,n,a,r),!0;case"dragenter":return Et=Bt(Et,e,t,n,a,r),!0;case"mouseover":return Xe=Bt(Xe,e,t,n,a,r),!0;case"pointerover":var s=r.pointerId;return bn.set(s,Bt(bn.get(s)||null,e,t,n,a,r)),!0;case"gotpointercapture":return s=r.pointerId,Nt.set(s,Bt(Nt.get(s)||null,e,t,n,a,r)),!0}return!1}function Qt(e){var t=ja(e.target);if(t!==null){var n=pn(t);if(n!==null){if(t=n.tag,t===13){if(t=Da(n),t!==null){e.blockedOn=t,yt(e.priority,function(){sn(n)});return}}else if(t===3&&n.stateNode.current.memoizedState.isDehydrated){e.blockedOn=n.tag===3?n.stateNode.containerInfo:null;return}}}e.blockedOn=null}function zt(e){if(e.blockedOn!==null)return!1;for(var t=e.targetContainers;0<t.length;){var n=ca(e.domEventName,e.eventSystemFlags,t[0],e.nativeEvent);if(n===null){n=e.nativeEvent;var a=new n.constructor(n.type,n);Tt=a,n.target.dispatchEvent(a),Tt=null}else return t=Jr(n),t!==null&&Mt(t),e.blockedOn=n,!1;t.shift()}return!0}function Wn(e,t,n){zt(e)&&n.delete(t)}function Je(){mt=!1,Ot!==null&&zt(Ot)&&(Ot=null),Et!==null&&zt(Et)&&(Et=null),Xe!==null&&zt(Xe)&&(Xe=null),bn.forEach(Wn),Nt.forEach(Wn)}function Yt(e,t){e.blockedOn===t&&(e.blockedOn=null,mt||(mt=!0,w.unstable_scheduleCallback(w.unstable_NormalPriority,Je)))}function Sn(e){function t(r){return Yt(r,e)}if(0<Ye.length){Yt(Ye[0],e);for(var n=1;n<Ye.length;n++){var a=Ye[n];a.blockedOn===e&&(a.blockedOn=null)}}for(Ot!==null&&Yt(Ot,e),Et!==null&&Yt(Et,e),Xe!==null&&Yt(Xe,e),bn.forEach(t),Nt.forEach(t),n=0;n<ft.length;n++)a=ft[n],a.blockedOn===e&&(a.blockedOn=null);for(;0<ft.length&&(n=ft[0],n.blockedOn===null);)Qt(n),n.blockedOn===null&&ft.shift()}var Un=O.ReactCurrentBatchConfig,qn=!0;function sr(e,t,n,a){var r=ye,s=Un.transition;Un.transition=null;try{ye=1,Oa(e,t,n,a)}finally{ye=r,Un.transition=s}}function or(e,t,n,a){var r=ye,s=Un.transition;Un.transition=null;try{ye=4,Oa(e,t,n,a)}finally{ye=r,Un.transition=s}}function Oa(e,t,n,a){if(qn){var r=ca(e,t,n,a);if(r===null)No(e,t,a,_e,n),qt(e,a);else if(wn(r,e,t,n,a))a.stopPropagation();else if(qt(e,a),t&4&&-1<Xt.indexOf(e)){for(;r!==null;){var s=Jr(r);if(s!==null&&bt(s),s=ca(e,t,n,a),s===null&&No(e,t,a,_e,n),s===r)break;r=s}r!==null&&a.stopPropagation()}else No(e,t,a,null,n)}}var _e=null;function ca(e,t,n,a){if(_e=null,e=Or(a),e=ja(e),e!==null)if(t=pn(e),t===null)e=null;else if(n=t.tag,n===13){if(e=Da(t),e!==null)return e;e=null}else if(n===3){if(t.stateNode.current.memoizedState.isDehydrated)return t.tag===3?t.stateNode.containerInfo:null;e=null}else t!==e&&(e=null);return _e=e,null}function Ss(e){switch(e){case"cancel":case"click":case"close":case"contextmenu":case"copy":case"cut":case"auxclick":case"dblclick":case"dragend":case"dragstart":case"drop":case"focusin":case"focusout":case"input":case"invalid":case"keydown":case"keypress":case"keyup":case"mousedown":case"mouseup":case"paste":case"pause":case"play":case"pointercancel":case"pointerdown":case"pointerup":case"ratechange":case"reset":case"resize":case"seeked":case"submit":case"touchcancel":case"touchend":case"touchstart":case"volumechange":case"change":case"selectionchange":case"textInput":case"compositionstart":case"compositionend":case"compositionupdate":case"beforeblur":case"afterblur":case"beforeinput":case"blur":case"fullscreenchange":case"focus":case"hashchange":case"popstate":case"select":case"selectstart":return 1;case"drag":case"dragenter":case"dragexit":case"dragleave":case"dragover":case"mousemove":case"mouseout":case"mouseover":case"pointermove":case"pointerout":case"pointerover":case"scroll":case"toggle":case"touchmove":case"wheel":case"mouseenter":case"mouseleave":case"pointerenter":case"pointerleave":return 4;case"message":switch(N()){case R:return 1;case G:return 4;case D:case z:return 16;case F:return 536870912;default:return 16}default:return 16}}var kn=null,ir=null,Ba=null;function Fa(){if(Ba)return Ba;var e,t=ir,n=t.length,a,r="value"in kn?kn.value:kn.textContent,s=r.length;for(e=0;e<n&&t[e]===r[e];e++);var i=n-e;for(a=1;a<=i&&t[n-a]===r[s-a];a++);return Ba=r.slice(e,1<a?1-a:void 0)}function Ka(e){var t=e.keyCode;return"charCode"in e?(e=e.charCode,e===0&&t===13&&(e=13)):e=t,e===10&&(e=13),32<=e||e===13?e:0}function da(){return!0}function lr(){return!1}function Ft(e){function t(n,a,r,s,i){this._reactName=n,this._targetInst=r,this.type=a,this.nativeEvent=s,this.target=i,this.currentTarget=null;for(var c in e)e.hasOwnProperty(c)&&(n=e[c],this[c]=n?n(s):s[c]);return this.isDefaultPrevented=(s.defaultPrevented!=null?s.defaultPrevented:s.returnValue===!1)?da:lr,this.isPropagationStopped=lr,this}return M(t.prototype,{preventDefault:function(){this.defaultPrevented=!0;var n=this.nativeEvent;n&&(n.preventDefault?n.preventDefault():typeof n.returnValue!="unknown"&&(n.returnValue=!1),this.isDefaultPrevented=da)},stopPropagation:function(){var n=this.nativeEvent;n&&(n.stopPropagation?n.stopPropagation():typeof n.cancelBubble!="unknown"&&(n.cancelBubble=!0),this.isPropagationStopped=da)},persist:function(){},isPersistent:da}),t}var Qn={eventPhase:0,bubbles:0,cancelable:0,timeStamp:function(e){return e.timeStamp||Date.now()},defaultPrevented:0,isTrusted:0},cr=Ft(Qn),zn=M({},Qn,{view:0,detail:0}),Tn=Ft(zn),Mn,Dn,On,Yn=M({},zn,{screenX:0,screenY:0,clientX:0,clientY:0,pageX:0,pageY:0,ctrlKey:0,shiftKey:0,altKey:0,metaKey:0,getModifierState:bo,button:0,buttons:0,relatedTarget:function(e){return e.relatedTarget===void 0?e.fromElement===e.srcElement?e.toElement:e.fromElement:e.relatedTarget},movementX:function(e){return"movementX"in e?e.movementX:(e!==On&&(On&&e.type==="mousemove"?(Mn=e.screenX-On.screenX,Dn=e.screenY-On.screenY):Dn=Mn=0,On=e),Mn)},movementY:function(e){return"movementY"in e?e.movementY:Dn}}),pa=Ft(Yn),ks=M({},Yn,{dataTransfer:0}),Gr=Ft(ks),dr=M({},zn,{relatedTarget:0}),Hr=Ft(dr),vo=M({},Qn,{animationName:0,elapsedTime:0,pseudoElement:0}),Ed=Ft(vo),_d=M({},Qn,{clipboardData:function(e){return"clipboardData"in e?e.clipboardData:window.clipboardData}}),Ad=Ft(_d),Id=M({},Qn,{data:0}),$i=Ft(Id),Ld={Esc:"Escape",Spacebar:" ",Left:"ArrowLeft",Up:"ArrowUp",Right:"ArrowRight",Down:"ArrowDown",Del:"Delete",Win:"OS",Menu:"ContextMenu",Apps:"ContextMenu",Scroll:"ScrollLock",MozPrintableKey:"Unidentified"},Nd={8:"Backspace",9:"Tab",12:"Clear",13:"Enter",16:"Shift",17:"Control",18:"Alt",19:"Pause",20:"CapsLock",27:"Escape",32:" ",33:"PageUp",34:"PageDown",35:"End",36:"Home",37:"ArrowLeft",38:"ArrowUp",39:"ArrowRight",40:"ArrowDown",45:"Insert",46:"Delete",112:"F1",113:"F2",114:"F3",115:"F4",116:"F5",117:"F6",118:"F7",119:"F8",120:"F9",121:"F10",122:"F11",123:"F12",144:"NumLock",145:"ScrollLock",224:"Meta"},Rd={Alt:"altKey",Control:"ctrlKey",Meta:"metaKey",Shift:"shiftKey"};function xd(e){var t=this.nativeEvent;return t.getModifierState?t.getModifierState(e):(e=Rd[e])?!!t[e]:!1}function bo(){return xd}var Pd=M({},zn,{key:function(e){if(e.key){var t=Ld[e.key]||e.key;if(t!=="Unidentified")return t}return e.type==="keypress"?(e=Ka(e),e===13?"Enter":String.fromCharCode(e)):e.type==="keydown"||e.type==="keyup"?Nd[e.keyCode]||"Unidentified":""},code:0,location:0,ctrlKey:0,shiftKey:0,altKey:0,metaKey:0,repeat:0,locale:0,getModifierState:bo,charCode:function(e){return e.type==="keypress"?Ka(e):0},keyCode:function(e){return e.type==="keydown"||e.type==="keyup"?e.keyCode:0},which:function(e){return e.type==="keypress"?Ka(e):e.type==="keydown"||e.type==="keyup"?e.keyCode:0}}),Md=Ft(Pd),Dd=M({},Yn,{pointerId:0,width:0,height:0,pressure:0,tangentialPressure:0,tiltX:0,tiltY:0,twist:0,pointerType:0,isPrimary:0}),Xi=Ft(Dd),Od=M({},zn,{touches:0,targetTouches:0,changedTouches:0,altKey:0,metaKey:0,ctrlKey:0,shiftKey:0,getModifierState:bo}),Bd=Ft(Od),Fd=M({},Qn,{propertyName:0,elapsedTime:0,pseudoElement:0}),Kd=Ft(Fd),jd=M({},Yn,{deltaX:function(e){return"deltaX"in e?e.deltaX:"wheelDeltaX"in e?-e.wheelDeltaX:0},deltaY:function(e){return"deltaY"in e?e.deltaY:"wheelDeltaY"in e?-e.wheelDeltaY:"wheelDelta"in e?-e.wheelDelta:0},deltaZ:0,deltaMode:0}),Gd=Ft(jd),Hd=[9,13,27,32],wo=de&&"CompositionEvent"in window,Wr=null;de&&"documentMode"in document&&(Wr=document.documentMode);var Wd=de&&"TextEvent"in window&&!Wr,Ji=de&&(!wo||Wr&&8<Wr&&11>=Wr),Zi=" ",el=!1;function tl(e,t){switch(e){case"keyup":return Hd.indexOf(t.keyCode)!==-1;case"keydown":return t.keyCode!==229;case"keypress":case"mousedown":case"focusout":return!0;default:return!1}}function nl(e){return e=e.detail,typeof e=="object"&&"data"in e?e.data:null}var pr=!1;function Ud(e,t){switch(e){case"compositionend":return nl(t);case"keypress":return t.which!==32?null:(el=!0,Zi);case"textInput":return e=t.data,e===Zi&&el?null:e;default:return null}}function qd(e,t){if(pr)return e==="compositionend"||!wo&&tl(e,t)?(e=Fa(),Ba=ir=kn=null,pr=!1,e):null;switch(e){case"paste":return null;case"keypress":if(!(t.ctrlKey||t.altKey||t.metaKey)||t.ctrlKey&&t.altKey){if(t.char&&1<t.char.length)return t.char;if(t.which)return String.fromCharCode(t.which)}return null;case"compositionend":return Ji&&t.locale!=="ko"?null:t.data;default:return null}}var Qd={color:!0,date:!0,datetime:!0,"datetime-local":!0,email:!0,month:!0,number:!0,password:!0,range:!0,search:!0,tel:!0,text:!0,time:!0,url:!0,week:!0};function al(e){var t=e&&e.nodeName&&e.nodeName.toLowerCase();return t==="input"?!!Qd[e.type]:t==="textarea"}function rl(e,t,n,a){bs(a),t=As(t,"onChange"),0<t.length&&(n=new cr("onChange","change",null,n,a),e.push({event:n,listeners:t}))}var Ur=null,qr=null;function zd(e){kl(e,0)}function Ts(e){var t=yr(e);if(Rr(t))return e}function Yd(e,t){if(e==="change")return t}var sl=!1;if(de){var So;if(de){var ko="oninput"in document;if(!ko){var ol=document.createElement("div");ol.setAttribute("oninput","return;"),ko=typeof ol.oninput=="function"}So=ko}else So=!1;sl=So&&(!document.documentMode||9<document.documentMode)}function il(){Ur&&(Ur.detachEvent("onpropertychange",ll),qr=Ur=null)}function ll(e){if(e.propertyName==="value"&&Ts(qr)){var t=[];rl(t,qr,e,Or(e)),Kr(zd,t)}}function Vd(e,t,n){e==="focusin"?(il(),Ur=t,qr=n,Ur.attachEvent("onpropertychange",ll)):e==="focusout"&&il()}function $d(e){if(e==="selectionchange"||e==="keyup"||e==="keydown")return Ts(qr)}function Xd(e,t){if(e==="click")return Ts(t)}function Jd(e,t){if(e==="input"||e==="change")return Ts(t)}function Zd(e,t){return e===t&&(e!==0||1/e===1/t)||e!==e&&t!==t}var Cn=typeof Object.is=="function"?Object.is:Zd;function Qr(e,t){if(Cn(e,t))return!0;if(typeof e!="object"||e===null||typeof t!="object"||t===null)return!1;var n=Object.keys(e),a=Object.keys(t);if(n.length!==a.length)return!1;for(a=0;a<n.length;a++){var r=n[a];if(!te.call(t,r)||!Cn(e[r],t[r]))return!1}return!0}function cl(e){for(;e&&e.firstChild;)e=e.firstChild;return e}function dl(e,t){var n=cl(e);e=0;for(var a;n;){if(n.nodeType===3){if(a=e+n.textContent.length,e<=t&&a>=t)return{node:n,offset:t-e};e=a}e:{for(;n;){if(n.nextSibling){n=n.nextSibling;break e}n=n.parentNode}n=void 0}n=cl(n)}}function pl(e,t){return e&&t?e===t?!0:e&&e.nodeType===3?!1:t&&t.nodeType===3?pl(e,t.parentNode):"contains"in e?e.contains(t):e.compareDocumentPosition?!!(e.compareDocumentPosition(t)&16):!1:!1}function ul(){for(var e=window,t=Hn();t instanceof e.HTMLIFrameElement;){try{var n=typeof t.contentWindow.location.href=="string"}catch{n=!1}if(n)e=t.contentWindow;else break;t=Hn(e.document)}return t}function To(e){var t=e&&e.nodeName&&e.nodeName.toLowerCase();return t&&(t==="input"&&(e.type==="text"||e.type==="search"||e.type==="tel"||e.type==="url"||e.type==="password")||t==="textarea"||e.contentEditable==="true")}function ep(e){var t=ul(),n=e.focusedElem,a=e.selectionRange;if(t!==n&&n&&n.ownerDocument&&pl(n.ownerDocument.documentElement,n)){if(a!==null&&To(n)){if(t=a.start,e=a.end,e===void 0&&(e=t),"selectionStart"in n)n.selectionStart=t,n.selectionEnd=Math.min(e,n.value.length);else if(e=(t=n.ownerDocument||document)&&t.defaultView||window,e.getSelection){e=e.getSelection();var r=n.textContent.length,s=Math.min(a.start,r);a=a.end===void 0?s:Math.min(a.end,r),!e.extend&&s>a&&(r=a,a=s,s=r),r=dl(n,s);var i=dl(n,a);r&&i&&(e.rangeCount!==1||e.anchorNode!==r.node||e.anchorOffset!==r.offset||e.focusNode!==i.node||e.focusOffset!==i.offset)&&(t=t.createRange(),t.setStart(r.node,r.offset),e.removeAllRanges(),s>a?(e.addRange(t),e.extend(i.node,i.offset)):(t.setEnd(i.node,i.offset),e.addRange(t)))}}for(t=[],e=n;e=e.parentNode;)e.nodeType===1&&t.push({element:e,left:e.scrollLeft,top:e.scrollTop});for(typeof n.focus=="function"&&n.focus(),n=0;n<t.length;n++)e=t[n],e.element.scrollLeft=e.left,e.element.scrollTop=e.top}}var tp=de&&"documentMode"in document&&11>=document.documentMode,ur=null,Co=null,zr=null,Eo=!1;function ml(e,t,n){var a=n.window===n?n.document:n.nodeType===9?n:n.ownerDocument;Eo||ur==null||ur!==Hn(a)||(a=ur,"selectionStart"in a&&To(a)?a={start:a.selectionStart,end:a.selectionEnd}:(a=(a.ownerDocument&&a.ownerDocument.defaultView||window).getSelection(),a={anchorNode:a.anchorNode,anchorOffset:a.anchorOffset,focusNode:a.focusNode,focusOffset:a.focusOffset}),zr&&Qr(zr,a)||(zr=a,a=As(Co,"onSelect"),0<a.length&&(t=new cr("onSelect","select",null,t,n),e.push({event:t,listeners:a}),t.target=ur)))}function Cs(e,t){var n={};return n[e.toLowerCase()]=t.toLowerCase(),n["Webkit"+e]="webkit"+t,n["Moz"+e]="moz"+t,n}var mr={animationend:Cs("Animation","AnimationEnd"),animationiteration:Cs("Animation","AnimationIteration"),animationstart:Cs("Animation","AnimationStart"),transitionend:Cs("Transition","TransitionEnd")},_o={},hl={};de&&(hl=document.createElement("div").style,"AnimationEvent"in window||(delete mr.animationend.animation,delete mr.animationiteration.animation,delete mr.animationstart.animation),"TransitionEvent"in window||delete mr.transitionend.transition);function Es(e){if(_o[e])return _o[e];if(!mr[e])return e;var t=mr[e],n;for(n in t)if(t.hasOwnProperty(n)&&n in hl)return _o[e]=t[n];return e}var gl=Es("animationend"),yl=Es("animationiteration"),fl=Es("animationstart"),vl=Es("transitionend"),bl=new Map,wl="abort auxClick cancel canPlay canPlayThrough click close contextMenu copy cut drag dragEnd dragEnter dragExit dragLeave dragOver dragStart drop durationChange emptied encrypted ended error gotPointerCapture input invalid keyDown keyPress keyUp load loadedData loadedMetadata loadStart lostPointerCapture mouseDown mouseMove mouseOut mouseOver mouseUp paste pause play playing pointerCancel pointerDown pointerMove pointerOut pointerOver pointerUp progress rateChange reset resize seeked seeking stalled submit suspend timeUpdate touchCancel touchEnd touchStart volumeChange scroll toggle touchMove waiting wheel".split(" ");function ua(e,t){bl.set(e,t),x(t,[e])}for(var Ao=0;Ao<wl.length;Ao++){var Io=wl[Ao],np=Io.toLowerCase(),ap=Io[0].toUpperCase()+Io.slice(1);ua(np,"on"+ap)}ua(gl,"onAnimationEnd"),ua(yl,"onAnimationIteration"),ua(fl,"onAnimationStart"),ua("dblclick","onDoubleClick"),ua("focusin","onFocus"),ua("focusout","onBlur"),ua(vl,"onTransitionEnd"),X("onMouseEnter",["mouseout","mouseover"]),X("onMouseLeave",["mouseout","mouseover"]),X("onPointerEnter",["pointerout","pointerover"]),X("onPointerLeave",["pointerout","pointerover"]),x("onChange","change click focusin focusout input keydown keyup selectionchange".split(" ")),x("onSelect","focusout contextmenu dragend focusin keydown keyup mousedown mouseup selectionchange".split(" ")),x("onBeforeInput",["compositionend","keypress","textInput","paste"]),x("onCompositionEnd","compositionend focusout keydown keypress keyup mousedown".split(" ")),x("onCompositionStart","compositionstart focusout keydown keypress keyup mousedown".split(" ")),x("onCompositionUpdate","compositionupdate focusout keydown keypress keyup mousedown".split(" "));var Yr="abort canplay canplaythrough durationchange emptied encrypted ended error loadeddata loadedmetadata loadstart pause play playing progress ratechange resize seeked seeking stalled suspend timeupdate volumechange waiting".split(" "),rp=new Set("cancel close invalid load scroll toggle".split(" ").concat(Yr));function Sl(e,t,n){var a=e.type||"unknown-event";e.currentTarget=n,Ma(a,t,void 0,e),e.currentTarget=null}function kl(e,t){t=(t&4)!==0;for(var n=0;n<e.length;n++){var a=e[n],r=a.event;a=a.listeners;e:{var s=void 0;if(t)for(var i=a.length-1;0<=i;i--){var c=a[i],u=c.instance,b=c.currentTarget;if(c=c.listener,u!==s&&r.isPropagationStopped())break e;Sl(r,c,b),s=u}else for(i=0;i<a.length;i++){if(c=a[i],u=c.instance,b=c.currentTarget,c=c.listener,u!==s&&r.isPropagationStopped())break e;Sl(r,c,b),s=u}}}if(Ra)throw e=xa,Ra=!1,xa=null,e}function rt(e,t){var n=t[Oo];n===void 0&&(n=t[Oo]=new Set);var a=e+"__bubble";n.has(a)||(Tl(t,e,2,!1),n.add(a))}function Lo(e,t,n){var a=0;t&&(a|=4),Tl(n,e,a,t)}var _s="_reactListening"+Math.random().toString(36).slice(2);function Vr(e){if(!e[_s]){e[_s]=!0,Q.forEach(function(n){n!=="selectionchange"&&(rp.has(n)||Lo(n,!1,e),Lo(n,!0,e))});var t=e.nodeType===9?e:e.ownerDocument;t===null||t[_s]||(t[_s]=!0,Lo("selectionchange",!1,t))}}function Tl(e,t,n,a){switch(Ss(t)){case 1:var r=sr;break;case 4:r=or;break;default:r=Oa}n=r.bind(null,t,n,e),r=void 0,!ar||t!=="touchstart"&&t!=="touchmove"&&t!=="wheel"||(r=!0),a?r!==void 0?e.addEventListener(t,n,{capture:!0,passive:r}):e.addEventListener(t,n,!0):r!==void 0?e.addEventListener(t,n,{passive:r}):e.addEventListener(t,n,!1)}function No(e,t,n,a,r){var s=a;if((t&1)===0&&(t&2)===0&&a!==null)e:for(;;){if(a===null)return;var i=a.tag;if(i===3||i===4){var c=a.stateNode.containerInfo;if(c===r||c.nodeType===8&&c.parentNode===r)break;if(i===4)for(i=a.return;i!==null;){var u=i.tag;if((u===3||u===4)&&(u=i.stateNode.containerInfo,u===r||u.nodeType===8&&u.parentNode===r))return;i=i.return}for(;c!==null;){if(i=ja(c),i===null)return;if(u=i.tag,u===5||u===6){a=s=i;continue e}c=c.parentNode}}a=a.return}Kr(function(){var b=s,A=Or(n),I=[];e:{var _=bl.get(e);if(_!==void 0){var H=cr,Y=e;switch(e){case"keypress":if(Ka(n)===0)break e;case"keydown":case"keyup":H=Md;break;case"focusin":Y="focus",H=Hr;break;case"focusout":Y="blur",H=Hr;break;case"beforeblur":case"afterblur":H=Hr;break;case"click":if(n.button===2)break e;case"auxclick":case"dblclick":case"mousedown":case"mousemove":case"mouseup":case"mouseout":case"mouseover":case"contextmenu":H=pa;break;case"drag":case"dragend":case"dragenter":case"dragexit":case"dragleave":case"dragover":case"dragstart":case"drop":H=Gr;break;case"touchcancel":case"touchend":case"touchmove":case"touchstart":H=Bd;break;case gl:case yl:case fl:H=Ed;break;case vl:H=Kd;break;case"scroll":H=Tn;break;case"wheel":H=Gd;break;case"copy":case"cut":case"paste":H=Ad;break;case"gotpointercapture":case"lostpointercapture":case"pointercancel":case"pointerdown":case"pointermove":case"pointerout":case"pointerover":case"pointerup":H=Xi}var $=(t&4)!==0,ht=!$&&e==="scroll",f=$?_!==null?_+"Capture":null:_;$=[];for(var m=b,v;m!==null;){v=m;var P=v.stateNode;if(v.tag===5&&P!==null&&(v=P,f!==null&&(P=oa(m,f),P!=null&&$.push($r(m,P,v)))),ht)break;m=m.return}0<$.length&&(_=new H(_,Y,null,n,A),I.push({event:_,listeners:$}))}}if((t&7)===0){e:{if(_=e==="mouseover"||e==="pointerover",H=e==="mouseout"||e==="pointerout",_&&n!==Tt&&(Y=n.relatedTarget||n.fromElement)&&(ja(Y)||Y[Vn]))break e;if((H||_)&&(_=A.window===A?A:(_=A.ownerDocument)?_.defaultView||_.parentWindow:window,H?(Y=n.relatedTarget||n.toElement,H=b,Y=Y?ja(Y):null,Y!==null&&(ht=pn(Y),Y!==ht||Y.tag!==5&&Y.tag!==6)&&(Y=null)):(H=null,Y=b),H!==Y)){if($=pa,P="onMouseLeave",f="onMouseEnter",m="mouse",(e==="pointerout"||e==="pointerover")&&($=Xi,P="onPointerLeave",f="onPointerEnter",m="pointer"),ht=H==null?_:yr(H),v=Y==null?_:yr(Y),_=new $(P,m+"leave",H,n,A),_.target=ht,_.relatedTarget=v,P=null,ja(A)===b&&($=new $(f,m+"enter",Y,n,A),$.target=v,$.relatedTarget=ht,P=$),ht=P,H&&Y)t:{for($=H,f=Y,m=0,v=$;v;v=hr(v))m++;for(v=0,P=f;P;P=hr(P))v++;for(;0<m-v;)$=hr($),m--;for(;0<v-m;)f=hr(f),v--;for(;m--;){if($===f||f!==null&&$===f.alternate)break t;$=hr($),f=hr(f)}$=null}else $=null;H!==null&&Cl(I,_,H,$,!1),Y!==null&&ht!==null&&Cl(I,ht,Y,$,!0)}}e:{if(_=b?yr(b):window,H=_.nodeName&&_.nodeName.toLowerCase(),H==="select"||H==="input"&&_.type==="file")var J=Yd;else if(al(_))if(sl)J=Jd;else{J=$d;var ie=Vd}else(H=_.nodeName)&&H.toLowerCase()==="input"&&(_.type==="checkbox"||_.type==="radio")&&(J=Xd);if(J&&(J=J(e,b))){rl(I,J,n,A);break e}ie&&ie(e,_,b),e==="focusout"&&(ie=_._wrapperState)&&ie.controlled&&_.type==="number"&&Pr(_,"number",_.value)}switch(ie=b?yr(b):window,e){case"focusin":(al(ie)||ie.contentEditable==="true")&&(ur=ie,Co=b,zr=null);break;case"focusout":zr=Co=ur=null;break;case"mousedown":Eo=!0;break;case"contextmenu":case"mouseup":case"dragend":Eo=!1,ml(I,n,A);break;case"selectionchange":if(tp)break;case"keydown":case"keyup":ml(I,n,A)}var le;if(wo)e:{switch(e){case"compositionstart":var fe="onCompositionStart";break e;case"compositionend":fe="onCompositionEnd";break e;case"compositionupdate":fe="onCompositionUpdate";break e}fe=void 0}else pr?tl(e,n)&&(fe="onCompositionEnd"):e==="keydown"&&n.keyCode===229&&(fe="onCompositionStart");fe&&(Ji&&n.locale!=="ko"&&(pr||fe!=="onCompositionStart"?fe==="onCompositionEnd"&&pr&&(le=Fa()):(kn=A,ir="value"in kn?kn.value:kn.textContent,pr=!0)),ie=As(b,fe),0<ie.length&&(fe=new $i(fe,e,null,n,A),I.push({event:fe,listeners:ie}),le?fe.data=le:(le=nl(n),le!==null&&(fe.data=le)))),(le=Wd?Ud(e,n):qd(e,n))&&(b=As(b,"onBeforeInput"),0<b.length&&(A=new $i("onBeforeInput","beforeinput",null,n,A),I.push({event:A,listeners:b}),A.data=le))}kl(I,t)})}function $r(e,t,n){return{instance:e,listener:t,currentTarget:n}}function As(e,t){for(var n=t+"Capture",a=[];e!==null;){var r=e,s=r.stateNode;r.tag===5&&s!==null&&(r=s,s=oa(e,n),s!=null&&a.unshift($r(e,s,r)),s=oa(e,t),s!=null&&a.push($r(e,s,r))),e=e.return}return a}function hr(e){if(e===null)return null;do e=e.return;while(e&&e.tag!==5);return e||null}function Cl(e,t,n,a,r){for(var s=t._reactName,i=[];n!==null&&n!==a;){var c=n,u=c.alternate,b=c.stateNode;if(u!==null&&u===a)break;c.tag===5&&b!==null&&(c=b,r?(u=oa(n,s),u!=null&&i.unshift($r(n,u,c))):r||(u=oa(n,s),u!=null&&i.push($r(n,u,c)))),n=n.return}i.length!==0&&e.push({event:t,listeners:i})}var sp=/\r\n?/g,op=/\u0000|\uFFFD/g;function El(e){return(typeof e=="string"?e:""+e).replace(sp,`
`).replace(op,"")}function Is(e,t,n){if(t=El(t),El(e)!==t&&n)throw Error(h(425))}function Ls(){}var Ro=null,xo=null;function Po(e,t){return e==="textarea"||e==="noscript"||typeof t.children=="string"||typeof t.children=="number"||typeof t.dangerouslySetInnerHTML=="object"&&t.dangerouslySetInnerHTML!==null&&t.dangerouslySetInnerHTML.__html!=null}var Mo=typeof setTimeout=="function"?setTimeout:void 0,ip=typeof clearTimeout=="function"?clearTimeout:void 0,_l=typeof Promise=="function"?Promise:void 0,lp=typeof queueMicrotask=="function"?queueMicrotask:typeof _l<"u"?function(e){return _l.resolve(null).then(e).catch(cp)}:Mo;function cp(e){setTimeout(function(){throw e})}function Do(e,t){var n=t,a=0;do{var r=n.nextSibling;if(e.removeChild(n),r&&r.nodeType===8)if(n=r.data,n==="/$"){if(a===0){e.removeChild(r),Sn(t);return}a--}else n!=="$"&&n!=="$?"&&n!=="$!"||a++;n=r}while(n);Sn(t)}function ma(e){for(;e!=null;e=e.nextSibling){var t=e.nodeType;if(t===1||t===3)break;if(t===8){if(t=e.data,t==="$"||t==="$!"||t==="$?")break;if(t==="/$")return null}}return e}function Al(e){e=e.previousSibling;for(var t=0;e;){if(e.nodeType===8){var n=e.data;if(n==="$"||n==="$!"||n==="$?"){if(t===0)return e;t--}else n==="/$"&&t++}e=e.previousSibling}return null}var gr=Math.random().toString(36).slice(2),Bn="__reactFiber$"+gr,Xr="__reactProps$"+gr,Vn="__reactContainer$"+gr,Oo="__reactEvents$"+gr,dp="__reactListeners$"+gr,pp="__reactHandles$"+gr;function ja(e){var t=e[Bn];if(t)return t;for(var n=e.parentNode;n;){if(t=n[Vn]||n[Bn]){if(n=t.alternate,t.child!==null||n!==null&&n.child!==null)for(e=Al(e);e!==null;){if(n=e[Bn])return n;e=Al(e)}return t}e=n,n=e.parentNode}return null}function Jr(e){return e=e[Bn]||e[Vn],!e||e.tag!==5&&e.tag!==6&&e.tag!==13&&e.tag!==3?null:e}function yr(e){if(e.tag===5||e.tag===6)return e.stateNode;throw Error(h(33))}function Ns(e){return e[Xr]||null}var Bo=[],fr=-1;function ha(e){return{current:e}}function st(e){0>fr||(e.current=Bo[fr],Bo[fr]=null,fr--)}function nt(e,t){fr++,Bo[fr]=e.current,e.current=t}var ga={},Kt=ha(ga),Jt=ha(!1),Ga=ga;function vr(e,t){var n=e.type.contextTypes;if(!n)return ga;var a=e.stateNode;if(a&&a.__reactInternalMemoizedUnmaskedChildContext===t)return a.__reactInternalMemoizedMaskedChildContext;var r={},s;for(s in n)r[s]=t[s];return a&&(e=e.stateNode,e.__reactInternalMemoizedUnmaskedChildContext=t,e.__reactInternalMemoizedMaskedChildContext=r),r}function Zt(e){return e=e.childContextTypes,e!=null}function Rs(){st(Jt),st(Kt)}function Il(e,t,n){if(Kt.current!==ga)throw Error(h(168));nt(Kt,t),nt(Jt,n)}function Ll(e,t,n){var a=e.stateNode;if(t=t.childContextTypes,typeof a.getChildContext!="function")return n;a=a.getChildContext();for(var r in a)if(!(r in t))throw Error(h(108,Se(e)||"Unknown",r));return M({},n,a)}function xs(e){return e=(e=e.stateNode)&&e.__reactInternalMemoizedMergedChildContext||ga,Ga=Kt.current,nt(Kt,e),nt(Jt,Jt.current),!0}function Nl(e,t,n){var a=e.stateNode;if(!a)throw Error(h(169));n?(e=Ll(e,t,Ga),a.__reactInternalMemoizedMergedChildContext=e,st(Jt),st(Kt),nt(Kt,e)):st(Jt),nt(Jt,n)}var $n=null,Ps=!1,Fo=!1;function Rl(e){$n===null?$n=[e]:$n.push(e)}function up(e){Ps=!0,Rl(e)}function ya(){if(!Fo&&$n!==null){Fo=!0;var e=0,t=ye;try{var n=$n;for(ye=1;e<n.length;e++){var a=n[e];do a=a(!0);while(a!==null)}$n=null,Ps=!1}catch(r){throw $n!==null&&($n=$n.slice(e+1)),g(R,ya),r}finally{ye=t,Fo=!1}}return null}var br=[],wr=0,Ms=null,Ds=0,un=[],mn=0,Ha=null,Xn=1,Jn="";function Wa(e,t){br[wr++]=Ds,br[wr++]=Ms,Ms=e,Ds=t}function xl(e,t,n){un[mn++]=Xn,un[mn++]=Jn,un[mn++]=Ha,Ha=e;var a=Xn;e=Jn;var r=32-ue(a)-1;a&=~(1<<r),n+=1;var s=32-ue(t)+r;if(30<s){var i=r-r%5;s=(a&(1<<i)-1).toString(32),a>>=i,r-=i,Xn=1<<32-ue(t)+r|n<<r|a,Jn=s+e}else Xn=1<<s|n<<r|a,Jn=e}function Ko(e){e.return!==null&&(Wa(e,1),xl(e,1,0))}function jo(e){for(;e===Ms;)Ms=br[--wr],br[wr]=null,Ds=br[--wr],br[wr]=null;for(;e===Ha;)Ha=un[--mn],un[mn]=null,Jn=un[--mn],un[mn]=null,Xn=un[--mn],un[mn]=null}var on=null,ln=null,it=!1,En=null;function Pl(e,t){var n=fn(5,null,null,0);n.elementType="DELETED",n.stateNode=t,n.return=e,t=e.deletions,t===null?(e.deletions=[n],e.flags|=16):t.push(n)}function Ml(e,t){switch(e.tag){case 5:var n=e.type;return t=t.nodeType!==1||n.toLowerCase()!==t.nodeName.toLowerCase()?null:t,t!==null?(e.stateNode=t,on=e,ln=ma(t.firstChild),!0):!1;case 6:return t=e.pendingProps===""||t.nodeType!==3?null:t,t!==null?(e.stateNode=t,on=e,ln=null,!0):!1;case 13:return t=t.nodeType!==8?null:t,t!==null?(n=Ha!==null?{id:Xn,overflow:Jn}:null,e.memoizedState={dehydrated:t,treeContext:n,retryLane:1073741824},n=fn(18,null,null,0),n.stateNode=t,n.return=e,e.child=n,on=e,ln=null,!0):!1;default:return!1}}function Go(e){return(e.mode&1)!==0&&(e.flags&128)===0}function Ho(e){if(it){var t=ln;if(t){var n=t;if(!Ml(e,t)){if(Go(e))throw Error(h(418));t=ma(n.nextSibling);var a=on;t&&Ml(e,t)?Pl(a,n):(e.flags=e.flags&-4097|2,it=!1,on=e)}}else{if(Go(e))throw Error(h(418));e.flags=e.flags&-4097|2,it=!1,on=e}}}function Dl(e){for(e=e.return;e!==null&&e.tag!==5&&e.tag!==3&&e.tag!==13;)e=e.return;on=e}function Os(e){if(e!==on)return!1;if(!it)return Dl(e),it=!0,!1;var t;if((t=e.tag!==3)&&!(t=e.tag!==5)&&(t=e.type,t=t!=="head"&&t!=="body"&&!Po(e.type,e.memoizedProps)),t&&(t=ln)){if(Go(e))throw Ol(),Error(h(418));for(;t;)Pl(e,t),t=ma(t.nextSibling)}if(Dl(e),e.tag===13){if(e=e.memoizedState,e=e!==null?e.dehydrated:null,!e)throw Error(h(317));e:{for(e=e.nextSibling,t=0;e;){if(e.nodeType===8){var n=e.data;if(n==="/$"){if(t===0){ln=ma(e.nextSibling);break e}t--}else n!=="$"&&n!=="$!"&&n!=="$?"||t++}e=e.nextSibling}ln=null}}else ln=on?ma(e.stateNode.nextSibling):null;return!0}function Ol(){for(var e=ln;e;)e=ma(e.nextSibling)}function Sr(){ln=on=null,it=!1}function Wo(e){En===null?En=[e]:En.push(e)}var mp=O.ReactCurrentBatchConfig;function Zr(e,t,n){if(e=n.ref,e!==null&&typeof e!="function"&&typeof e!="object"){if(n._owner){if(n=n._owner,n){if(n.tag!==1)throw Error(h(309));var a=n.stateNode}if(!a)throw Error(h(147,e));var r=a,s=""+e;return t!==null&&t.ref!==null&&typeof t.ref=="function"&&t.ref._stringRef===s?t.ref:(t=function(i){var c=r.refs;i===null?delete c[s]:c[s]=i},t._stringRef=s,t)}if(typeof e!="string")throw Error(h(284));if(!n._owner)throw Error(h(290,e))}return e}function Bs(e,t){throw e=Object.prototype.toString.call(t),Error(h(31,e==="[object Object]"?"object with keys {"+Object.keys(t).join(", ")+"}":e))}function Bl(e){var t=e._init;return t(e._payload)}function Fl(e){function t(f,m){if(e){var v=f.deletions;v===null?(f.deletions=[m],f.flags|=16):v.push(m)}}function n(f,m){if(!e)return null;for(;m!==null;)t(f,m),m=m.sibling;return null}function a(f,m){for(f=new Map;m!==null;)m.key!==null?f.set(m.key,m):f.set(m.index,m),m=m.sibling;return f}function r(f,m){return f=Ca(f,m),f.index=0,f.sibling=null,f}function s(f,m,v){return f.index=v,e?(v=f.alternate,v!==null?(v=v.index,v<m?(f.flags|=2,m):v):(f.flags|=2,m)):(f.flags|=1048576,m)}function i(f){return e&&f.alternate===null&&(f.flags|=2),f}function c(f,m,v,P){return m===null||m.tag!==6?(m=Mi(v,f.mode,P),m.return=f,m):(m=r(m,v),m.return=f,m)}function u(f,m,v,P){var J=v.type;return J===me?A(f,m,v.props.children,P,v.key):m!==null&&(m.elementType===J||typeof J=="object"&&J!==null&&J.$$typeof===Be&&Bl(J)===m.type)?(P=r(m,v.props),P.ref=Zr(f,m,v),P.return=f,P):(P=io(v.type,v.key,v.props,null,f.mode,P),P.ref=Zr(f,m,v),P.return=f,P)}function b(f,m,v,P){return m===null||m.tag!==4||m.stateNode.containerInfo!==v.containerInfo||m.stateNode.implementation!==v.implementation?(m=Di(v,f.mode,P),m.return=f,m):(m=r(m,v.children||[]),m.return=f,m)}function A(f,m,v,P,J){return m===null||m.tag!==7?(m=Xa(v,f.mode,P,J),m.return=f,m):(m=r(m,v),m.return=f,m)}function I(f,m,v){if(typeof m=="string"&&m!==""||typeof m=="number")return m=Mi(""+m,f.mode,v),m.return=f,m;if(typeof m=="object"&&m!==null){switch(m.$$typeof){case V:return v=io(m.type,m.key,m.props,null,f.mode,v),v.ref=Zr(f,null,m),v.return=f,v;case pe:return m=Di(m,f.mode,v),m.return=f,m;case Be:var P=m._init;return I(f,P(m._payload),v)}if(Ia(m)||se(m))return m=Xa(m,f.mode,v,null),m.return=f,m;Bs(f,m)}return null}function _(f,m,v,P){var J=m!==null?m.key:null;if(typeof v=="string"&&v!==""||typeof v=="number")return J!==null?null:c(f,m,""+v,P);if(typeof v=="object"&&v!==null){switch(v.$$typeof){case V:return v.key===J?u(f,m,v,P):null;case pe:return v.key===J?b(f,m,v,P):null;case Be:return J=v._init,_(f,m,J(v._payload),P)}if(Ia(v)||se(v))return J!==null?null:A(f,m,v,P,null);Bs(f,v)}return null}function H(f,m,v,P,J){if(typeof P=="string"&&P!==""||typeof P=="number")return f=f.get(v)||null,c(m,f,""+P,J);if(typeof P=="object"&&P!==null){switch(P.$$typeof){case V:return f=f.get(P.key===null?v:P.key)||null,u(m,f,P,J);case pe:return f=f.get(P.key===null?v:P.key)||null,b(m,f,P,J);case Be:var ie=P._init;return H(f,m,v,ie(P._payload),J)}if(Ia(P)||se(P))return f=f.get(v)||null,A(m,f,P,J,null);Bs(m,P)}return null}function Y(f,m,v,P){for(var J=null,ie=null,le=m,fe=m=0,It=null;le!==null&&fe<v.length;fe++){le.index>fe?(It=le,le=null):It=le.sibling;var je=_(f,le,v[fe],P);if(je===null){le===null&&(le=It);break}e&&le&&je.alternate===null&&t(f,le),m=s(je,m,fe),ie===null?J=je:ie.sibling=je,ie=je,le=It}if(fe===v.length)return n(f,le),it&&Wa(f,fe),J;if(le===null){for(;fe<v.length;fe++)le=I(f,v[fe],P),le!==null&&(m=s(le,m,fe),ie===null?J=le:ie.sibling=le,ie=le);return it&&Wa(f,fe),J}for(le=a(f,le);fe<v.length;fe++)It=H(le,f,fe,v[fe],P),It!==null&&(e&&It.alternate!==null&&le.delete(It.key===null?fe:It.key),m=s(It,m,fe),ie===null?J=It:ie.sibling=It,ie=It);return e&&le.forEach(function(Ea){return t(f,Ea)}),it&&Wa(f,fe),J}function $(f,m,v,P){var J=se(v);if(typeof J!="function")throw Error(h(150));if(v=J.call(v),v==null)throw Error(h(151));for(var ie=J=null,le=m,fe=m=0,It=null,je=v.next();le!==null&&!je.done;fe++,je=v.next()){le.index>fe?(It=le,le=null):It=le.sibling;var Ea=_(f,le,je.value,P);if(Ea===null){le===null&&(le=It);break}e&&le&&Ea.alternate===null&&t(f,le),m=s(Ea,m,fe),ie===null?J=Ea:ie.sibling=Ea,ie=Ea,le=It}if(je.done)return n(f,le),it&&Wa(f,fe),J;if(le===null){for(;!je.done;fe++,je=v.next())je=I(f,je.value,P),je!==null&&(m=s(je,m,fe),ie===null?J=je:ie.sibling=je,ie=je);return it&&Wa(f,fe),J}for(le=a(f,le);!je.done;fe++,je=v.next())je=H(le,f,fe,je.value,P),je!==null&&(e&&je.alternate!==null&&le.delete(je.key===null?fe:je.key),m=s(je,m,fe),ie===null?J=je:ie.sibling=je,ie=je);return e&&le.forEach(function(Qp){return t(f,Qp)}),it&&Wa(f,fe),J}function ht(f,m,v,P){if(typeof v=="object"&&v!==null&&v.type===me&&v.key===null&&(v=v.props.children),typeof v=="object"&&v!==null){switch(v.$$typeof){case V:e:{for(var J=v.key,ie=m;ie!==null;){if(ie.key===J){if(J=v.type,J===me){if(ie.tag===7){n(f,ie.sibling),m=r(ie,v.props.children),m.return=f,f=m;break e}}else if(ie.elementType===J||typeof J=="object"&&J!==null&&J.$$typeof===Be&&Bl(J)===ie.type){n(f,ie.sibling),m=r(ie,v.props),m.ref=Zr(f,ie,v),m.return=f,f=m;break e}n(f,ie);break}else t(f,ie);ie=ie.sibling}v.type===me?(m=Xa(v.props.children,f.mode,P,v.key),m.return=f,f=m):(P=io(v.type,v.key,v.props,null,f.mode,P),P.ref=Zr(f,m,v),P.return=f,f=P)}return i(f);case pe:e:{for(ie=v.key;m!==null;){if(m.key===ie)if(m.tag===4&&m.stateNode.containerInfo===v.containerInfo&&m.stateNode.implementation===v.implementation){n(f,m.sibling),m=r(m,v.children||[]),m.return=f,f=m;break e}else{n(f,m);break}else t(f,m);m=m.sibling}m=Di(v,f.mode,P),m.return=f,f=m}return i(f);case Be:return ie=v._init,ht(f,m,ie(v._payload),P)}if(Ia(v))return Y(f,m,v,P);if(se(v))return $(f,m,v,P);Bs(f,v)}return typeof v=="string"&&v!==""||typeof v=="number"?(v=""+v,m!==null&&m.tag===6?(n(f,m.sibling),m=r(m,v),m.return=f,f=m):(n(f,m),m=Mi(v,f.mode,P),m.return=f,f=m),i(f)):n(f,m)}return ht}var kr=Fl(!0),Kl=Fl(!1),Fs=ha(null),Ks=null,Tr=null,Uo=null;function qo(){Uo=Tr=Ks=null}function Qo(e){var t=Fs.current;st(Fs),e._currentValue=t}function zo(e,t,n){for(;e!==null;){var a=e.alternate;if((e.childLanes&t)!==t?(e.childLanes|=t,a!==null&&(a.childLanes|=t)):a!==null&&(a.childLanes&t)!==t&&(a.childLanes|=t),e===n)break;e=e.return}}function Cr(e,t){Ks=e,Uo=Tr=null,e=e.dependencies,e!==null&&e.firstContext!==null&&((e.lanes&t)!==0&&(en=!0),e.firstContext=null)}function hn(e){var t=e._currentValue;if(Uo!==e)if(e={context:e,memoizedValue:t,next:null},Tr===null){if(Ks===null)throw Error(h(308));Tr=e,Ks.dependencies={lanes:0,firstContext:e}}else Tr=Tr.next=e;return t}var Ua=null;function Yo(e){Ua===null?Ua=[e]:Ua.push(e)}function jl(e,t,n,a){var r=t.interleaved;return r===null?(n.next=n,Yo(t)):(n.next=r.next,r.next=n),t.interleaved=n,Zn(e,a)}function Zn(e,t){e.lanes|=t;var n=e.alternate;for(n!==null&&(n.lanes|=t),n=e,e=e.return;e!==null;)e.childLanes|=t,n=e.alternate,n!==null&&(n.childLanes|=t),n=e,e=e.return;return n.tag===3?n.stateNode:null}var fa=!1;function Vo(e){e.updateQueue={baseState:e.memoizedState,firstBaseUpdate:null,lastBaseUpdate:null,shared:{pending:null,interleaved:null,lanes:0},effects:null}}function Gl(e,t){e=e.updateQueue,t.updateQueue===e&&(t.updateQueue={baseState:e.baseState,firstBaseUpdate:e.firstBaseUpdate,lastBaseUpdate:e.lastBaseUpdate,shared:e.shared,effects:e.effects})}function ea(e,t){return{eventTime:e,lane:t,tag:0,payload:null,callback:null,next:null}}function va(e,t,n){var a=e.updateQueue;if(a===null)return null;if(a=a.shared,(De&2)!==0){var r=a.pending;return r===null?t.next=t:(t.next=r.next,r.next=t),a.pending=t,Zn(e,n)}return r=a.interleaved,r===null?(t.next=t,Yo(a)):(t.next=r.next,r.next=t),a.interleaved=t,Zn(e,n)}function js(e,t,n){if(t=t.updateQueue,t!==null&&(t=t.shared,(n&4194240)!==0)){var a=t.lanes;a&=e.pendingLanes,n|=a,t.lanes=n,ot(e,n)}}function Hl(e,t){var n=e.updateQueue,a=e.alternate;if(a!==null&&(a=a.updateQueue,n===a)){var r=null,s=null;if(n=n.firstBaseUpdate,n!==null){do{var i={eventTime:n.eventTime,lane:n.lane,tag:n.tag,payload:n.payload,callback:n.callback,next:null};s===null?r=s=i:s=s.next=i,n=n.next}while(n!==null);s===null?r=s=t:s=s.next=t}else r=s=t;n={baseState:a.baseState,firstBaseUpdate:r,lastBaseUpdate:s,shared:a.shared,effects:a.effects},e.updateQueue=n;return}e=n.lastBaseUpdate,e===null?n.firstBaseUpdate=t:e.next=t,n.lastBaseUpdate=t}function Gs(e,t,n,a){var r=e.updateQueue;fa=!1;var s=r.firstBaseUpdate,i=r.lastBaseUpdate,c=r.shared.pending;if(c!==null){r.shared.pending=null;var u=c,b=u.next;u.next=null,i===null?s=b:i.next=b,i=u;var A=e.alternate;A!==null&&(A=A.updateQueue,c=A.lastBaseUpdate,c!==i&&(c===null?A.firstBaseUpdate=b:c.next=b,A.lastBaseUpdate=u))}if(s!==null){var I=r.baseState;i=0,A=b=u=null,c=s;do{var _=c.lane,H=c.eventTime;if((a&_)===_){A!==null&&(A=A.next={eventTime:H,lane:0,tag:c.tag,payload:c.payload,callback:c.callback,next:null});e:{var Y=e,$=c;switch(_=t,H=n,$.tag){case 1:if(Y=$.payload,typeof Y=="function"){I=Y.call(H,I,_);break e}I=Y;break e;case 3:Y.flags=Y.flags&-65537|128;case 0:if(Y=$.payload,_=typeof Y=="function"?Y.call(H,I,_):Y,_==null)break e;I=M({},I,_);break e;case 2:fa=!0}}c.callback!==null&&c.lane!==0&&(e.flags|=64,_=r.effects,_===null?r.effects=[c]:_.push(c))}else H={eventTime:H,lane:_,tag:c.tag,payload:c.payload,callback:c.callback,next:null},A===null?(b=A=H,u=I):A=A.next=H,i|=_;if(c=c.next,c===null){if(c=r.shared.pending,c===null)break;_=c,c=_.next,_.next=null,r.lastBaseUpdate=_,r.shared.pending=null}}while(!0);if(A===null&&(u=I),r.baseState=u,r.firstBaseUpdate=b,r.lastBaseUpdate=A,t=r.shared.interleaved,t!==null){r=t;do i|=r.lane,r=r.next;while(r!==t)}else s===null&&(r.shared.lanes=0);za|=i,e.lanes=i,e.memoizedState=I}}function Wl(e,t,n){if(e=t.effects,t.effects=null,e!==null)for(t=0;t<e.length;t++){var a=e[t],r=a.callback;if(r!==null){if(a.callback=null,a=n,typeof r!="function")throw Error(h(191,r));r.call(a)}}}var es={},Fn=ha(es),ts=ha(es),ns=ha(es);function qa(e){if(e===es)throw Error(h(174));return e}function $o(e,t){switch(nt(ns,t),nt(ts,e),nt(Fn,es),e=t.nodeType,e){case 9:case 11:t=(t=t.documentElement)?t.namespaceURI:rn(null,"");break;default:e=e===8?t.parentNode:t,t=e.namespaceURI||null,e=e.tagName,t=rn(t,e)}st(Fn),nt(Fn,t)}function Er(){st(Fn),st(ts),st(ns)}function Ul(e){qa(ns.current);var t=qa(Fn.current),n=rn(t,e.type);t!==n&&(nt(ts,e),nt(Fn,n))}function Xo(e){ts.current===e&&(st(Fn),st(ts))}var dt=ha(0);function Hs(e){for(var t=e;t!==null;){if(t.tag===13){var n=t.memoizedState;if(n!==null&&(n=n.dehydrated,n===null||n.data==="$?"||n.data==="$!"))return t}else if(t.tag===19&&t.memoizedProps.revealOrder!==void 0){if((t.flags&128)!==0)return t}else if(t.child!==null){t.child.return=t,t=t.child;continue}if(t===e)break;for(;t.sibling===null;){if(t.return===null||t.return===e)return null;t=t.return}t.sibling.return=t.return,t=t.sibling}return null}var Jo=[];function Zo(){for(var e=0;e<Jo.length;e++)Jo[e]._workInProgressVersionPrimary=null;Jo.length=0}var Ws=O.ReactCurrentDispatcher,ei=O.ReactCurrentBatchConfig,Qa=0,pt=null,wt=null,_t=null,Us=!1,as=!1,rs=0,hp=0;function jt(){throw Error(h(321))}function ti(e,t){if(t===null)return!1;for(var n=0;n<t.length&&n<e.length;n++)if(!Cn(e[n],t[n]))return!1;return!0}function ni(e,t,n,a,r,s){if(Qa=s,pt=t,t.memoizedState=null,t.updateQueue=null,t.lanes=0,Ws.current=e===null||e.memoizedState===null?vp:bp,e=n(a,r),as){s=0;do{if(as=!1,rs=0,25<=s)throw Error(h(301));s+=1,_t=wt=null,t.updateQueue=null,Ws.current=wp,e=n(a,r)}while(as)}if(Ws.current=zs,t=wt!==null&&wt.next!==null,Qa=0,_t=wt=pt=null,Us=!1,t)throw Error(h(300));return e}function ai(){var e=rs!==0;return rs=0,e}function Kn(){var e={memoizedState:null,baseState:null,baseQueue:null,queue:null,next:null};return _t===null?pt.memoizedState=_t=e:_t=_t.next=e,_t}function gn(){if(wt===null){var e=pt.alternate;e=e!==null?e.memoizedState:null}else e=wt.next;var t=_t===null?pt.memoizedState:_t.next;if(t!==null)_t=t,wt=e;else{if(e===null)throw Error(h(310));wt=e,e={memoizedState:wt.memoizedState,baseState:wt.baseState,baseQueue:wt.baseQueue,queue:wt.queue,next:null},_t===null?pt.memoizedState=_t=e:_t=_t.next=e}return _t}function ss(e,t){return typeof t=="function"?t(e):t}function ri(e){var t=gn(),n=t.queue;if(n===null)throw Error(h(311));n.lastRenderedReducer=e;var a=wt,r=a.baseQueue,s=n.pending;if(s!==null){if(r!==null){var i=r.next;r.next=s.next,s.next=i}a.baseQueue=r=s,n.pending=null}if(r!==null){s=r.next,a=a.baseState;var c=i=null,u=null,b=s;do{var A=b.lane;if((Qa&A)===A)u!==null&&(u=u.next={lane:0,action:b.action,hasEagerState:b.hasEagerState,eagerState:b.eagerState,next:null}),a=b.hasEagerState?b.eagerState:e(a,b.action);else{var I={lane:A,action:b.action,hasEagerState:b.hasEagerState,eagerState:b.eagerState,next:null};u===null?(c=u=I,i=a):u=u.next=I,pt.lanes|=A,za|=A}b=b.next}while(b!==null&&b!==s);u===null?i=a:u.next=c,Cn(a,t.memoizedState)||(en=!0),t.memoizedState=a,t.baseState=i,t.baseQueue=u,n.lastRenderedState=a}if(e=n.interleaved,e!==null){r=e;do s=r.lane,pt.lanes|=s,za|=s,r=r.next;while(r!==e)}else r===null&&(n.lanes=0);return[t.memoizedState,n.dispatch]}function si(e){var t=gn(),n=t.queue;if(n===null)throw Error(h(311));n.lastRenderedReducer=e;var a=n.dispatch,r=n.pending,s=t.memoizedState;if(r!==null){n.pending=null;var i=r=r.next;do s=e(s,i.action),i=i.next;while(i!==r);Cn(s,t.memoizedState)||(en=!0),t.memoizedState=s,t.baseQueue===null&&(t.baseState=s),n.lastRenderedState=s}return[s,a]}function ql(){}function Ql(e,t){var n=pt,a=gn(),r=t(),s=!Cn(a.memoizedState,r);if(s&&(a.memoizedState=r,en=!0),a=a.queue,oi(Vl.bind(null,n,a,e),[e]),a.getSnapshot!==t||s||_t!==null&&_t.memoizedState.tag&1){if(n.flags|=2048,os(9,Yl.bind(null,n,a,r,t),void 0,null),At===null)throw Error(h(349));(Qa&30)!==0||zl(n,t,r)}return r}function zl(e,t,n){e.flags|=16384,e={getSnapshot:t,value:n},t=pt.updateQueue,t===null?(t={lastEffect:null,stores:null},pt.updateQueue=t,t.stores=[e]):(n=t.stores,n===null?t.stores=[e]:n.push(e))}function Yl(e,t,n,a){t.value=n,t.getSnapshot=a,$l(t)&&Xl(e)}function Vl(e,t,n){return n(function(){$l(t)&&Xl(e)})}function $l(e){var t=e.getSnapshot;e=e.value;try{var n=t();return!Cn(e,n)}catch{return!0}}function Xl(e){var t=Zn(e,1);t!==null&&Ln(t,e,1,-1)}function Jl(e){var t=Kn();return typeof e=="function"&&(e=e()),t.memoizedState=t.baseState=e,e={pending:null,interleaved:null,lanes:0,dispatch:null,lastRenderedReducer:ss,lastRenderedState:e},t.queue=e,e=e.dispatch=fp.bind(null,pt,e),[t.memoizedState,e]}function os(e,t,n,a){return e={tag:e,create:t,destroy:n,deps:a,next:null},t=pt.updateQueue,t===null?(t={lastEffect:null,stores:null},pt.updateQueue=t,t.lastEffect=e.next=e):(n=t.lastEffect,n===null?t.lastEffect=e.next=e:(a=n.next,n.next=e,e.next=a,t.lastEffect=e)),e}function Zl(){return gn().memoizedState}function qs(e,t,n,a){var r=Kn();pt.flags|=e,r.memoizedState=os(1|t,n,void 0,a===void 0?null:a)}function Qs(e,t,n,a){var r=gn();a=a===void 0?null:a;var s=void 0;if(wt!==null){var i=wt.memoizedState;if(s=i.destroy,a!==null&&ti(a,i.deps)){r.memoizedState=os(t,n,s,a);return}}pt.flags|=e,r.memoizedState=os(1|t,n,s,a)}function ec(e,t){return qs(8390656,8,e,t)}function oi(e,t){return Qs(2048,8,e,t)}function tc(e,t){return Qs(4,2,e,t)}function nc(e,t){return Qs(4,4,e,t)}function ac(e,t){if(typeof t=="function")return e=e(),t(e),function(){t(null)};if(t!=null)return e=e(),t.current=e,function(){t.current=null}}function rc(e,t,n){return n=n!=null?n.concat([e]):null,Qs(4,4,ac.bind(null,t,e),n)}function ii(){}function sc(e,t){var n=gn();t=t===void 0?null:t;var a=n.memoizedState;return a!==null&&t!==null&&ti(t,a[1])?a[0]:(n.memoizedState=[e,t],e)}function oc(e,t){var n=gn();t=t===void 0?null:t;var a=n.memoizedState;return a!==null&&t!==null&&ti(t,a[1])?a[0]:(e=e(),n.memoizedState=[e,t],e)}function ic(e,t,n){return(Qa&21)===0?(e.baseState&&(e.baseState=!1,en=!0),e.memoizedState=n):(Cn(n,t)||(n=Ue(),pt.lanes|=n,za|=n,e.baseState=!0),t)}function gp(e,t){var n=ye;ye=n!==0&&4>n?n:4,e(!0);var a=ei.transition;ei.transition={};try{e(!1),t()}finally{ye=n,ei.transition=a}}function lc(){return gn().memoizedState}function yp(e,t,n){var a=ka(e);if(n={lane:a,action:n,hasEagerState:!1,eagerState:null,next:null},cc(e))dc(t,n);else if(n=jl(e,t,n,a),n!==null){var r=$t();Ln(n,e,a,r),pc(n,t,a)}}function fp(e,t,n){var a=ka(e),r={lane:a,action:n,hasEagerState:!1,eagerState:null,next:null};if(cc(e))dc(t,r);else{var s=e.alternate;if(e.lanes===0&&(s===null||s.lanes===0)&&(s=t.lastRenderedReducer,s!==null))try{var i=t.lastRenderedState,c=s(i,n);if(r.hasEagerState=!0,r.eagerState=c,Cn(c,i)){var u=t.interleaved;u===null?(r.next=r,Yo(t)):(r.next=u.next,u.next=r),t.interleaved=r;return}}catch{}finally{}n=jl(e,t,r,a),n!==null&&(r=$t(),Ln(n,e,a,r),pc(n,t,a))}}function cc(e){var t=e.alternate;return e===pt||t!==null&&t===pt}function dc(e,t){as=Us=!0;var n=e.pending;n===null?t.next=t:(t.next=n.next,n.next=t),e.pending=t}function pc(e,t,n){if((n&4194240)!==0){var a=t.lanes;a&=e.pendingLanes,n|=a,t.lanes=n,ot(e,n)}}var zs={readContext:hn,useCallback:jt,useContext:jt,useEffect:jt,useImperativeHandle:jt,useInsertionEffect:jt,useLayoutEffect:jt,useMemo:jt,useReducer:jt,useRef:jt,useState:jt,useDebugValue:jt,useDeferredValue:jt,useTransition:jt,useMutableSource:jt,useSyncExternalStore:jt,useId:jt,unstable_isNewReconciler:!1},vp={readContext:hn,useCallback:function(e,t){return Kn().memoizedState=[e,t===void 0?null:t],e},useContext:hn,useEffect:ec,useImperativeHandle:function(e,t,n){return n=n!=null?n.concat([e]):null,qs(4194308,4,ac.bind(null,t,e),n)},useLayoutEffect:function(e,t){return qs(4194308,4,e,t)},useInsertionEffect:function(e,t){return qs(4,2,e,t)},useMemo:function(e,t){var n=Kn();return t=t===void 0?null:t,e=e(),n.memoizedState=[e,t],e},useReducer:function(e,t,n){var a=Kn();return t=n!==void 0?n(t):t,a.memoizedState=a.baseState=t,e={pending:null,interleaved:null,lanes:0,dispatch:null,lastRenderedReducer:e,lastRenderedState:t},a.queue=e,e=e.dispatch=yp.bind(null,pt,e),[a.memoizedState,e]},useRef:function(e){var t=Kn();return e={current:e},t.memoizedState=e},useState:Jl,useDebugValue:ii,useDeferredValue:function(e){return Kn().memoizedState=e},useTransition:function(){var e=Jl(!1),t=e[0];return e=gp.bind(null,e[1]),Kn().memoizedState=e,[t,e]},useMutableSource:function(){},useSyncExternalStore:function(e,t,n){var a=pt,r=Kn();if(it){if(n===void 0)throw Error(h(407));n=n()}else{if(n=t(),At===null)throw Error(h(349));(Qa&30)!==0||zl(a,t,n)}r.memoizedState=n;var s={value:n,getSnapshot:t};return r.queue=s,ec(Vl.bind(null,a,s,e),[e]),a.flags|=2048,os(9,Yl.bind(null,a,s,n,t),void 0,null),n},useId:function(){var e=Kn(),t=At.identifierPrefix;if(it){var n=Jn,a=Xn;n=(a&~(1<<32-ue(a)-1)).toString(32)+n,t=":"+t+"R"+n,n=rs++,0<n&&(t+="H"+n.toString(32)),t+=":"}else n=hp++,t=":"+t+"r"+n.toString(32)+":";return e.memoizedState=t},unstable_isNewReconciler:!1},bp={readContext:hn,useCallback:sc,useContext:hn,useEffect:oi,useImperativeHandle:rc,useInsertionEffect:tc,useLayoutEffect:nc,useMemo:oc,useReducer:ri,useRef:Zl,useState:function(){return ri(ss)},useDebugValue:ii,useDeferredValue:function(e){var t=gn();return ic(t,wt.memoizedState,e)},useTransition:function(){var e=ri(ss)[0],t=gn().memoizedState;return[e,t]},useMutableSource:ql,useSyncExternalStore:Ql,useId:lc,unstable_isNewReconciler:!1},wp={readContext:hn,useCallback:sc,useContext:hn,useEffect:oi,useImperativeHandle:rc,useInsertionEffect:tc,useLayoutEffect:nc,useMemo:oc,useReducer:si,useRef:Zl,useState:function(){return si(ss)},useDebugValue:ii,useDeferredValue:function(e){var t=gn();return wt===null?t.memoizedState=e:ic(t,wt.memoizedState,e)},useTransition:function(){var e=si(ss)[0],t=gn().memoizedState;return[e,t]},useMutableSource:ql,useSyncExternalStore:Ql,useId:lc,unstable_isNewReconciler:!1};function _n(e,t){if(e&&e.defaultProps){t=M({},t),e=e.defaultProps;for(var n in e)t[n]===void 0&&(t[n]=e[n]);return t}return t}function li(e,t,n,a){t=e.memoizedState,n=n(a,t),n=n==null?t:M({},t,n),e.memoizedState=n,e.lanes===0&&(e.updateQueue.baseState=n)}var Ys={isMounted:function(e){return(e=e._reactInternals)?pn(e)===e:!1},enqueueSetState:function(e,t,n){e=e._reactInternals;var a=$t(),r=ka(e),s=ea(a,r);s.payload=t,n!=null&&(s.callback=n),t=va(e,s,r),t!==null&&(Ln(t,e,r,a),js(t,e,r))},enqueueReplaceState:function(e,t,n){e=e._reactInternals;var a=$t(),r=ka(e),s=ea(a,r);s.tag=1,s.payload=t,n!=null&&(s.callback=n),t=va(e,s,r),t!==null&&(Ln(t,e,r,a),js(t,e,r))},enqueueForceUpdate:function(e,t){e=e._reactInternals;var n=$t(),a=ka(e),r=ea(n,a);r.tag=2,t!=null&&(r.callback=t),t=va(e,r,a),t!==null&&(Ln(t,e,a,n),js(t,e,a))}};function uc(e,t,n,a,r,s,i){return e=e.stateNode,typeof e.shouldComponentUpdate=="function"?e.shouldComponentUpdate(a,s,i):t.prototype&&t.prototype.isPureReactComponent?!Qr(n,a)||!Qr(r,s):!0}function mc(e,t,n){var a=!1,r=ga,s=t.contextType;return typeof s=="object"&&s!==null?s=hn(s):(r=Zt(t)?Ga:Kt.current,a=t.contextTypes,s=(a=a!=null)?vr(e,r):ga),t=new t(n,s),e.memoizedState=t.state!==null&&t.state!==void 0?t.state:null,t.updater=Ys,e.stateNode=t,t._reactInternals=e,a&&(e=e.stateNode,e.__reactInternalMemoizedUnmaskedChildContext=r,e.__reactInternalMemoizedMaskedChildContext=s),t}function hc(e,t,n,a){e=t.state,typeof t.componentWillReceiveProps=="function"&&t.componentWillReceiveProps(n,a),typeof t.UNSAFE_componentWillReceiveProps=="function"&&t.UNSAFE_componentWillReceiveProps(n,a),t.state!==e&&Ys.enqueueReplaceState(t,t.state,null)}function ci(e,t,n,a){var r=e.stateNode;r.props=n,r.state=e.memoizedState,r.refs={},Vo(e);var s=t.contextType;typeof s=="object"&&s!==null?r.context=hn(s):(s=Zt(t)?Ga:Kt.current,r.context=vr(e,s)),r.state=e.memoizedState,s=t.getDerivedStateFromProps,typeof s=="function"&&(li(e,t,s,n),r.state=e.memoizedState),typeof t.getDerivedStateFromProps=="function"||typeof r.getSnapshotBeforeUpdate=="function"||typeof r.UNSAFE_componentWillMount!="function"&&typeof r.componentWillMount!="function"||(t=r.state,typeof r.componentWillMount=="function"&&r.componentWillMount(),typeof r.UNSAFE_componentWillMount=="function"&&r.UNSAFE_componentWillMount(),t!==r.state&&Ys.enqueueReplaceState(r,r.state,null),Gs(e,n,r,a),r.state=e.memoizedState),typeof r.componentDidMount=="function"&&(e.flags|=4194308)}function _r(e,t){try{var n="",a=t;do n+=ae(a),a=a.return;while(a);var r=n}catch(s){r=`
Error generating stack: `+s.message+`
`+s.stack}return{value:e,source:t,stack:r,digest:null}}function di(e,t,n){return{value:e,source:null,stack:n??null,digest:t??null}}function pi(e,t){try{console.error(t.value)}catch(n){setTimeout(function(){throw n})}}var Sp=typeof WeakMap=="function"?WeakMap:Map;function gc(e,t,n){n=ea(-1,n),n.tag=3,n.payload={element:null};var a=t.value;return n.callback=function(){to||(to=!0,_i=a),pi(e,t)},n}function yc(e,t,n){n=ea(-1,n),n.tag=3;var a=e.type.getDerivedStateFromError;if(typeof a=="function"){var r=t.value;n.payload=function(){return a(r)},n.callback=function(){pi(e,t)}}var s=e.stateNode;return s!==null&&typeof s.componentDidCatch=="function"&&(n.callback=function(){pi(e,t),typeof a!="function"&&(wa===null?wa=new Set([this]):wa.add(this));var i=t.stack;this.componentDidCatch(t.value,{componentStack:i!==null?i:""})}),n}function fc(e,t,n){var a=e.pingCache;if(a===null){a=e.pingCache=new Sp;var r=new Set;a.set(t,r)}else r=a.get(t),r===void 0&&(r=new Set,a.set(t,r));r.has(n)||(r.add(n),e=Dp.bind(null,e,t,n),t.then(e,e))}function vc(e){do{var t;if((t=e.tag===13)&&(t=e.memoizedState,t=t!==null?t.dehydrated!==null:!0),t)return e;e=e.return}while(e!==null);return null}function bc(e,t,n,a,r){return(e.mode&1)===0?(e===t?e.flags|=65536:(e.flags|=128,n.flags|=131072,n.flags&=-52805,n.tag===1&&(n.alternate===null?n.tag=17:(t=ea(-1,1),t.tag=2,va(n,t,1))),n.lanes|=1),e):(e.flags|=65536,e.lanes=r,e)}var kp=O.ReactCurrentOwner,en=!1;function Vt(e,t,n,a){t.child=e===null?Kl(t,null,n,a):kr(t,e.child,n,a)}function wc(e,t,n,a,r){n=n.render;var s=t.ref;return Cr(t,r),a=ni(e,t,n,a,s,r),n=ai(),e!==null&&!en?(t.updateQueue=e.updateQueue,t.flags&=-2053,e.lanes&=~r,ta(e,t,r)):(it&&n&&Ko(t),t.flags|=1,Vt(e,t,a,r),t.child)}function Sc(e,t,n,a,r){if(e===null){var s=n.type;return typeof s=="function"&&!Pi(s)&&s.defaultProps===void 0&&n.compare===null&&n.defaultProps===void 0?(t.tag=15,t.type=s,kc(e,t,s,a,r)):(e=io(n.type,null,a,t,t.mode,r),e.ref=t.ref,e.return=t,t.child=e)}if(s=e.child,(e.lanes&r)===0){var i=s.memoizedProps;if(n=n.compare,n=n!==null?n:Qr,n(i,a)&&e.ref===t.ref)return ta(e,t,r)}return t.flags|=1,e=Ca(s,a),e.ref=t.ref,e.return=t,t.child=e}function kc(e,t,n,a,r){if(e!==null){var s=e.memoizedProps;if(Qr(s,a)&&e.ref===t.ref)if(en=!1,t.pendingProps=a=s,(e.lanes&r)!==0)(e.flags&131072)!==0&&(en=!0);else return t.lanes=e.lanes,ta(e,t,r)}return ui(e,t,n,a,r)}function Tc(e,t,n){var a=t.pendingProps,r=a.children,s=e!==null?e.memoizedState:null;if(a.mode==="hidden")if((t.mode&1)===0)t.memoizedState={baseLanes:0,cachePool:null,transitions:null},nt(Ir,cn),cn|=n;else{if((n&1073741824)===0)return e=s!==null?s.baseLanes|n:n,t.lanes=t.childLanes=1073741824,t.memoizedState={baseLanes:e,cachePool:null,transitions:null},t.updateQueue=null,nt(Ir,cn),cn|=e,null;t.memoizedState={baseLanes:0,cachePool:null,transitions:null},a=s!==null?s.baseLanes:n,nt(Ir,cn),cn|=a}else s!==null?(a=s.baseLanes|n,t.memoizedState=null):a=n,nt(Ir,cn),cn|=a;return Vt(e,t,r,n),t.child}function Cc(e,t){var n=t.ref;(e===null&&n!==null||e!==null&&e.ref!==n)&&(t.flags|=512,t.flags|=2097152)}function ui(e,t,n,a,r){var s=Zt(n)?Ga:Kt.current;return s=vr(t,s),Cr(t,r),n=ni(e,t,n,a,s,r),a=ai(),e!==null&&!en?(t.updateQueue=e.updateQueue,t.flags&=-2053,e.lanes&=~r,ta(e,t,r)):(it&&a&&Ko(t),t.flags|=1,Vt(e,t,n,r),t.child)}function Ec(e,t,n,a,r){if(Zt(n)){var s=!0;xs(t)}else s=!1;if(Cr(t,r),t.stateNode===null)$s(e,t),mc(t,n,a),ci(t,n,a,r),a=!0;else if(e===null){var i=t.stateNode,c=t.memoizedProps;i.props=c;var u=i.context,b=n.contextType;typeof b=="object"&&b!==null?b=hn(b):(b=Zt(n)?Ga:Kt.current,b=vr(t,b));var A=n.getDerivedStateFromProps,I=typeof A=="function"||typeof i.getSnapshotBeforeUpdate=="function";I||typeof i.UNSAFE_componentWillReceiveProps!="function"&&typeof i.componentWillReceiveProps!="function"||(c!==a||u!==b)&&hc(t,i,a,b),fa=!1;var _=t.memoizedState;i.state=_,Gs(t,a,i,r),u=t.memoizedState,c!==a||_!==u||Jt.current||fa?(typeof A=="function"&&(li(t,n,A,a),u=t.memoizedState),(c=fa||uc(t,n,c,a,_,u,b))?(I||typeof i.UNSAFE_componentWillMount!="function"&&typeof i.componentWillMount!="function"||(typeof i.componentWillMount=="function"&&i.componentWillMount(),typeof i.UNSAFE_componentWillMount=="function"&&i.UNSAFE_componentWillMount()),typeof i.componentDidMount=="function"&&(t.flags|=4194308)):(typeof i.componentDidMount=="function"&&(t.flags|=4194308),t.memoizedProps=a,t.memoizedState=u),i.props=a,i.state=u,i.context=b,a=c):(typeof i.componentDidMount=="function"&&(t.flags|=4194308),a=!1)}else{i=t.stateNode,Gl(e,t),c=t.memoizedProps,b=t.type===t.elementType?c:_n(t.type,c),i.props=b,I=t.pendingProps,_=i.context,u=n.contextType,typeof u=="object"&&u!==null?u=hn(u):(u=Zt(n)?Ga:Kt.current,u=vr(t,u));var H=n.getDerivedStateFromProps;(A=typeof H=="function"||typeof i.getSnapshotBeforeUpdate=="function")||typeof i.UNSAFE_componentWillReceiveProps!="function"&&typeof i.componentWillReceiveProps!="function"||(c!==I||_!==u)&&hc(t,i,a,u),fa=!1,_=t.memoizedState,i.state=_,Gs(t,a,i,r);var Y=t.memoizedState;c!==I||_!==Y||Jt.current||fa?(typeof H=="function"&&(li(t,n,H,a),Y=t.memoizedState),(b=fa||uc(t,n,b,a,_,Y,u)||!1)?(A||typeof i.UNSAFE_componentWillUpdate!="function"&&typeof i.componentWillUpdate!="function"||(typeof i.componentWillUpdate=="function"&&i.componentWillUpdate(a,Y,u),typeof i.UNSAFE_componentWillUpdate=="function"&&i.UNSAFE_componentWillUpdate(a,Y,u)),typeof i.componentDidUpdate=="function"&&(t.flags|=4),typeof i.getSnapshotBeforeUpdate=="function"&&(t.flags|=1024)):(typeof i.componentDidUpdate!="function"||c===e.memoizedProps&&_===e.memoizedState||(t.flags|=4),typeof i.getSnapshotBeforeUpdate!="function"||c===e.memoizedProps&&_===e.memoizedState||(t.flags|=1024),t.memoizedProps=a,t.memoizedState=Y),i.props=a,i.state=Y,i.context=u,a=b):(typeof i.componentDidUpdate!="function"||c===e.memoizedProps&&_===e.memoizedState||(t.flags|=4),typeof i.getSnapshotBeforeUpdate!="function"||c===e.memoizedProps&&_===e.memoizedState||(t.flags|=1024),a=!1)}return mi(e,t,n,a,s,r)}function mi(e,t,n,a,r,s){Cc(e,t);var i=(t.flags&128)!==0;if(!a&&!i)return r&&Nl(t,n,!1),ta(e,t,s);a=t.stateNode,kp.current=t;var c=i&&typeof n.getDerivedStateFromError!="function"?null:a.render();return t.flags|=1,e!==null&&i?(t.child=kr(t,e.child,null,s),t.child=kr(t,null,c,s)):Vt(e,t,c,s),t.memoizedState=a.state,r&&Nl(t,n,!0),t.child}function _c(e){var t=e.stateNode;t.pendingContext?Il(e,t.pendingContext,t.pendingContext!==t.context):t.context&&Il(e,t.context,!1),$o(e,t.containerInfo)}function Ac(e,t,n,a,r){return Sr(),Wo(r),t.flags|=256,Vt(e,t,n,a),t.child}var hi={dehydrated:null,treeContext:null,retryLane:0};function gi(e){return{baseLanes:e,cachePool:null,transitions:null}}function Ic(e,t,n){var a=t.pendingProps,r=dt.current,s=!1,i=(t.flags&128)!==0,c;if((c=i)||(c=e!==null&&e.memoizedState===null?!1:(r&2)!==0),c?(s=!0,t.flags&=-129):(e===null||e.memoizedState!==null)&&(r|=1),nt(dt,r&1),e===null)return Ho(t),e=t.memoizedState,e!==null&&(e=e.dehydrated,e!==null)?((t.mode&1)===0?t.lanes=1:e.data==="$!"?t.lanes=8:t.lanes=1073741824,null):(i=a.children,e=a.fallback,s?(a=t.mode,s=t.child,i={mode:"hidden",children:i},(a&1)===0&&s!==null?(s.childLanes=0,s.pendingProps=i):s=lo(i,a,0,null),e=Xa(e,a,n,null),s.return=t,e.return=t,s.sibling=e,t.child=s,t.child.memoizedState=gi(n),t.memoizedState=hi,e):yi(t,i));if(r=e.memoizedState,r!==null&&(c=r.dehydrated,c!==null))return Tp(e,t,i,a,c,r,n);if(s){s=a.fallback,i=t.mode,r=e.child,c=r.sibling;var u={mode:"hidden",children:a.children};return(i&1)===0&&t.child!==r?(a=t.child,a.childLanes=0,a.pendingProps=u,t.deletions=null):(a=Ca(r,u),a.subtreeFlags=r.subtreeFlags&14680064),c!==null?s=Ca(c,s):(s=Xa(s,i,n,null),s.flags|=2),s.return=t,a.return=t,a.sibling=s,t.child=a,a=s,s=t.child,i=e.child.memoizedState,i=i===null?gi(n):{baseLanes:i.baseLanes|n,cachePool:null,transitions:i.transitions},s.memoizedState=i,s.childLanes=e.childLanes&~n,t.memoizedState=hi,a}return s=e.child,e=s.sibling,a=Ca(s,{mode:"visible",children:a.children}),(t.mode&1)===0&&(a.lanes=n),a.return=t,a.sibling=null,e!==null&&(n=t.deletions,n===null?(t.deletions=[e],t.flags|=16):n.push(e)),t.child=a,t.memoizedState=null,a}function yi(e,t){return t=lo({mode:"visible",children:t},e.mode,0,null),t.return=e,e.child=t}function Vs(e,t,n,a){return a!==null&&Wo(a),kr(t,e.child,null,n),e=yi(t,t.pendingProps.children),e.flags|=2,t.memoizedState=null,e}function Tp(e,t,n,a,r,s,i){if(n)return t.flags&256?(t.flags&=-257,a=di(Error(h(422))),Vs(e,t,i,a)):t.memoizedState!==null?(t.child=e.child,t.flags|=128,null):(s=a.fallback,r=t.mode,a=lo({mode:"visible",children:a.children},r,0,null),s=Xa(s,r,i,null),s.flags|=2,a.return=t,s.return=t,a.sibling=s,t.child=a,(t.mode&1)!==0&&kr(t,e.child,null,i),t.child.memoizedState=gi(i),t.memoizedState=hi,s);if((t.mode&1)===0)return Vs(e,t,i,null);if(r.data==="$!"){if(a=r.nextSibling&&r.nextSibling.dataset,a)var c=a.dgst;return a=c,s=Error(h(419)),a=di(s,a,void 0),Vs(e,t,i,a)}if(c=(i&e.childLanes)!==0,en||c){if(a=At,a!==null){switch(i&-i){case 4:r=2;break;case 16:r=8;break;case 64:case 128:case 256:case 512:case 1024:case 2048:case 4096:case 8192:case 16384:case 32768:case 65536:case 131072:case 262144:case 524288:case 1048576:case 2097152:case 4194304:case 8388608:case 16777216:case 33554432:case 67108864:r=32;break;case 536870912:r=268435456;break;default:r=0}r=(r&(a.suspendedLanes|i))!==0?0:r,r!==0&&r!==s.retryLane&&(s.retryLane=r,Zn(e,r),Ln(a,e,r,-1))}return xi(),a=di(Error(h(421))),Vs(e,t,i,a)}return r.data==="$?"?(t.flags|=128,t.child=e.child,t=Op.bind(null,e),r._reactRetry=t,null):(e=s.treeContext,ln=ma(r.nextSibling),on=t,it=!0,En=null,e!==null&&(un[mn++]=Xn,un[mn++]=Jn,un[mn++]=Ha,Xn=e.id,Jn=e.overflow,Ha=t),t=yi(t,a.children),t.flags|=4096,t)}function Lc(e,t,n){e.lanes|=t;var a=e.alternate;a!==null&&(a.lanes|=t),zo(e.return,t,n)}function fi(e,t,n,a,r){var s=e.memoizedState;s===null?e.memoizedState={isBackwards:t,rendering:null,renderingStartTime:0,last:a,tail:n,tailMode:r}:(s.isBackwards=t,s.rendering=null,s.renderingStartTime=0,s.last=a,s.tail=n,s.tailMode=r)}function Nc(e,t,n){var a=t.pendingProps,r=a.revealOrder,s=a.tail;if(Vt(e,t,a.children,n),a=dt.current,(a&2)!==0)a=a&1|2,t.flags|=128;else{if(e!==null&&(e.flags&128)!==0)e:for(e=t.child;e!==null;){if(e.tag===13)e.memoizedState!==null&&Lc(e,n,t);else if(e.tag===19)Lc(e,n,t);else if(e.child!==null){e.child.return=e,e=e.child;continue}if(e===t)break e;for(;e.sibling===null;){if(e.return===null||e.return===t)break e;e=e.return}e.sibling.return=e.return,e=e.sibling}a&=1}if(nt(dt,a),(t.mode&1)===0)t.memoizedState=null;else switch(r){case"forwards":for(n=t.child,r=null;n!==null;)e=n.alternate,e!==null&&Hs(e)===null&&(r=n),n=n.sibling;n=r,n===null?(r=t.child,t.child=null):(r=n.sibling,n.sibling=null),fi(t,!1,r,n,s);break;case"backwards":for(n=null,r=t.child,t.child=null;r!==null;){if(e=r.alternate,e!==null&&Hs(e)===null){t.child=r;break}e=r.sibling,r.sibling=n,n=r,r=e}fi(t,!0,n,null,s);break;case"together":fi(t,!1,null,null,void 0);break;default:t.memoizedState=null}return t.child}function $s(e,t){(t.mode&1)===0&&e!==null&&(e.alternate=null,t.alternate=null,t.flags|=2)}function ta(e,t,n){if(e!==null&&(t.dependencies=e.dependencies),za|=t.lanes,(n&t.childLanes)===0)return null;if(e!==null&&t.child!==e.child)throw Error(h(153));if(t.child!==null){for(e=t.child,n=Ca(e,e.pendingProps),t.child=n,n.return=t;e.sibling!==null;)e=e.sibling,n=n.sibling=Ca(e,e.pendingProps),n.return=t;n.sibling=null}return t.child}function Cp(e,t,n){switch(t.tag){case 3:_c(t),Sr();break;case 5:Ul(t);break;case 1:Zt(t.type)&&xs(t);break;case 4:$o(t,t.stateNode.containerInfo);break;case 10:var a=t.type._context,r=t.memoizedProps.value;nt(Fs,a._currentValue),a._currentValue=r;break;case 13:if(a=t.memoizedState,a!==null)return a.dehydrated!==null?(nt(dt,dt.current&1),t.flags|=128,null):(n&t.child.childLanes)!==0?Ic(e,t,n):(nt(dt,dt.current&1),e=ta(e,t,n),e!==null?e.sibling:null);nt(dt,dt.current&1);break;case 19:if(a=(n&t.childLanes)!==0,(e.flags&128)!==0){if(a)return Nc(e,t,n);t.flags|=128}if(r=t.memoizedState,r!==null&&(r.rendering=null,r.tail=null,r.lastEffect=null),nt(dt,dt.current),a)break;return null;case 22:case 23:return t.lanes=0,Tc(e,t,n)}return ta(e,t,n)}var Rc,vi,xc,Pc;Rc=function(e,t){for(var n=t.child;n!==null;){if(n.tag===5||n.tag===6)e.appendChild(n.stateNode);else if(n.tag!==4&&n.child!==null){n.child.return=n,n=n.child;continue}if(n===t)break;for(;n.sibling===null;){if(n.return===null||n.return===t)return;n=n.return}n.sibling.return=n.return,n=n.sibling}},vi=function(){},xc=function(e,t,n,a){var r=e.memoizedProps;if(r!==a){e=t.stateNode,qa(Fn.current);var s=null;switch(n){case"input":r=xr(e,r),a=xr(e,a),s=[];break;case"select":r=M({},r,{value:void 0}),a=M({},a,{value:void 0}),s=[];break;case"textarea":r=Lt(e,r),a=Lt(e,a),s=[];break;default:typeof r.onClick!="function"&&typeof a.onClick=="function"&&(e.onclick=Ls)}er(n,a);var i;n=null;for(b in r)if(!a.hasOwnProperty(b)&&r.hasOwnProperty(b)&&r[b]!=null)if(b==="style"){var c=r[b];for(i in c)c.hasOwnProperty(i)&&(n||(n={}),n[i]="")}else b!=="dangerouslySetInnerHTML"&&b!=="children"&&b!=="suppressContentEditableWarning"&&b!=="suppressHydrationWarning"&&b!=="autoFocus"&&(j.hasOwnProperty(b)?s||(s=[]):(s=s||[]).push(b,null));for(b in a){var u=a[b];if(c=r!=null?r[b]:void 0,a.hasOwnProperty(b)&&u!==c&&(u!=null||c!=null))if(b==="style")if(c){for(i in c)!c.hasOwnProperty(i)||u&&u.hasOwnProperty(i)||(n||(n={}),n[i]="");for(i in u)u.hasOwnProperty(i)&&c[i]!==u[i]&&(n||(n={}),n[i]=u[i])}else n||(s||(s=[]),s.push(b,n)),n=u;else b==="dangerouslySetInnerHTML"?(u=u?u.__html:void 0,c=c?c.__html:void 0,u!=null&&c!==u&&(s=s||[]).push(b,u)):b==="children"?typeof u!="string"&&typeof u!="number"||(s=s||[]).push(b,""+u):b!=="suppressContentEditableWarning"&&b!=="suppressHydrationWarning"&&(j.hasOwnProperty(b)?(u!=null&&b==="onScroll"&&rt("scroll",e),s||c===u||(s=[])):(s=s||[]).push(b,u))}n&&(s=s||[]).push("style",n);var b=s;(t.updateQueue=b)&&(t.flags|=4)}},Pc=function(e,t,n,a){n!==a&&(t.flags|=4)};function is(e,t){if(!it)switch(e.tailMode){case"hidden":t=e.tail;for(var n=null;t!==null;)t.alternate!==null&&(n=t),t=t.sibling;n===null?e.tail=null:n.sibling=null;break;case"collapsed":n=e.tail;for(var a=null;n!==null;)n.alternate!==null&&(a=n),n=n.sibling;a===null?t||e.tail===null?e.tail=null:e.tail.sibling=null:a.sibling=null}}function Gt(e){var t=e.alternate!==null&&e.alternate.child===e.child,n=0,a=0;if(t)for(var r=e.child;r!==null;)n|=r.lanes|r.childLanes,a|=r.subtreeFlags&14680064,a|=r.flags&14680064,r.return=e,r=r.sibling;else for(r=e.child;r!==null;)n|=r.lanes|r.childLanes,a|=r.subtreeFlags,a|=r.flags,r.return=e,r=r.sibling;return e.subtreeFlags|=a,e.childLanes=n,t}function Ep(e,t,n){var a=t.pendingProps;switch(jo(t),t.tag){case 2:case 16:case 15:case 0:case 11:case 7:case 8:case 12:case 9:case 14:return Gt(t),null;case 1:return Zt(t.type)&&Rs(),Gt(t),null;case 3:return a=t.stateNode,Er(),st(Jt),st(Kt),Zo(),a.pendingContext&&(a.context=a.pendingContext,a.pendingContext=null),(e===null||e.child===null)&&(Os(t)?t.flags|=4:e===null||e.memoizedState.isDehydrated&&(t.flags&256)===0||(t.flags|=1024,En!==null&&(Li(En),En=null))),vi(e,t),Gt(t),null;case 5:Xo(t);var r=qa(ns.current);if(n=t.type,e!==null&&t.stateNode!=null)xc(e,t,n,a,r),e.ref!==t.ref&&(t.flags|=512,t.flags|=2097152);else{if(!a){if(t.stateNode===null)throw Error(h(166));return Gt(t),null}if(e=qa(Fn.current),Os(t)){a=t.stateNode,n=t.type;var s=t.memoizedProps;switch(a[Bn]=t,a[Xr]=s,e=(t.mode&1)!==0,n){case"dialog":rt("cancel",a),rt("close",a);break;case"iframe":case"object":case"embed":rt("load",a);break;case"video":case"audio":for(r=0;r<Yr.length;r++)rt(Yr[r],a);break;case"source":rt("error",a);break;case"img":case"image":case"link":rt("error",a),rt("load",a);break;case"details":rt("toggle",a);break;case"input":fs(a,s),rt("invalid",a);break;case"select":a._wrapperState={wasMultiple:!!s.multiple},rt("invalid",a);break;case"textarea":dn(a,s),rt("invalid",a)}er(n,s),r=null;for(var i in s)if(s.hasOwnProperty(i)){var c=s[i];i==="children"?typeof c=="string"?a.textContent!==c&&(s.suppressHydrationWarning!==!0&&Is(a.textContent,c,e),r=["children",c]):typeof c=="number"&&a.textContent!==""+c&&(s.suppressHydrationWarning!==!0&&Is(a.textContent,c,e),r=["children",""+c]):j.hasOwnProperty(i)&&c!=null&&i==="onScroll"&&rt("scroll",a)}switch(n){case"input":Gn(a),Aa(a,s,!0);break;case"textarea":Gn(a),ce(a);break;case"select":case"option":break;default:typeof s.onClick=="function"&&(a.onclick=Ls)}a=r,t.updateQueue=a,a!==null&&(t.flags|=4)}else{i=r.nodeType===9?r:r.ownerDocument,e==="http://www.w3.org/1999/xhtml"&&(e=Za(n)),e==="http://www.w3.org/1999/xhtml"?n==="script"?(e=i.createElement("div"),e.innerHTML="<script><\/script>",e=e.removeChild(e.firstChild)):typeof a.is=="string"?e=i.createElement(n,{is:a.is}):(e=i.createElement(n),n==="select"&&(i=e,a.multiple?i.multiple=!0:a.size&&(i.size=a.size))):e=i.createElementNS(e,n),e[Bn]=t,e[Xr]=a,Rc(e,t,!1,!1),t.stateNode=e;e:{switch(i=tr(n,a),n){case"dialog":rt("cancel",e),rt("close",e),r=a;break;case"iframe":case"object":case"embed":rt("load",e),r=a;break;case"video":case"audio":for(r=0;r<Yr.length;r++)rt(Yr[r],e);r=a;break;case"source":rt("error",e),r=a;break;case"img":case"image":case"link":rt("error",e),rt("load",e),r=a;break;case"details":rt("toggle",e),r=a;break;case"input":fs(e,a),r=xr(e,a),rt("invalid",e);break;case"option":r=a;break;case"select":e._wrapperState={wasMultiple:!!a.multiple},r=M({},a,{value:void 0}),rt("invalid",e);break;case"textarea":dn(e,a),r=Lt(e,a),rt("invalid",e);break;default:r=a}er(n,r),c=r;for(s in c)if(c.hasOwnProperty(s)){var u=c[s];s==="style"?vs(e,u):s==="dangerouslySetInnerHTML"?(u=u?u.__html:void 0,u!=null&&Mr(e,u)):s==="children"?typeof u=="string"?(n!=="textarea"||u!=="")&&Qe(e,u):typeof u=="number"&&Qe(e,""+u):s!=="suppressContentEditableWarning"&&s!=="suppressHydrationWarning"&&s!=="autoFocus"&&(j.hasOwnProperty(s)?u!=null&&s==="onScroll"&&rt("scroll",e):u!=null&&at(e,s,u,i))}switch(n){case"input":Gn(e),Aa(e,a,!1);break;case"textarea":Gn(e),ce(e);break;case"option":a.value!=null&&e.setAttribute("value",""+Le(a.value));break;case"select":e.multiple=!!a.multiple,s=a.value,s!=null?ra(e,!!a.multiple,s,!1):a.defaultValue!=null&&ra(e,!!a.multiple,a.defaultValue,!0);break;default:typeof r.onClick=="function"&&(e.onclick=Ls)}switch(n){case"button":case"input":case"select":case"textarea":a=!!a.autoFocus;break e;case"img":a=!0;break e;default:a=!1}}a&&(t.flags|=4)}t.ref!==null&&(t.flags|=512,t.flags|=2097152)}return Gt(t),null;case 6:if(e&&t.stateNode!=null)Pc(e,t,e.memoizedProps,a);else{if(typeof a!="string"&&t.stateNode===null)throw Error(h(166));if(n=qa(ns.current),qa(Fn.current),Os(t)){if(a=t.stateNode,n=t.memoizedProps,a[Bn]=t,(s=a.nodeValue!==n)&&(e=on,e!==null))switch(e.tag){case 3:Is(a.nodeValue,n,(e.mode&1)!==0);break;case 5:e.memoizedProps.suppressHydrationWarning!==!0&&Is(a.nodeValue,n,(e.mode&1)!==0)}s&&(t.flags|=4)}else a=(n.nodeType===9?n:n.ownerDocument).createTextNode(a),a[Bn]=t,t.stateNode=a}return Gt(t),null;case 13:if(st(dt),a=t.memoizedState,e===null||e.memoizedState!==null&&e.memoizedState.dehydrated!==null){if(it&&ln!==null&&(t.mode&1)!==0&&(t.flags&128)===0)Ol(),Sr(),t.flags|=98560,s=!1;else if(s=Os(t),a!==null&&a.dehydrated!==null){if(e===null){if(!s)throw Error(h(318));if(s=t.memoizedState,s=s!==null?s.dehydrated:null,!s)throw Error(h(317));s[Bn]=t}else Sr(),(t.flags&128)===0&&(t.memoizedState=null),t.flags|=4;Gt(t),s=!1}else En!==null&&(Li(En),En=null),s=!0;if(!s)return t.flags&65536?t:null}return(t.flags&128)!==0?(t.lanes=n,t):(a=a!==null,a!==(e!==null&&e.memoizedState!==null)&&a&&(t.child.flags|=8192,(t.mode&1)!==0&&(e===null||(dt.current&1)!==0?St===0&&(St=3):xi())),t.updateQueue!==null&&(t.flags|=4),Gt(t),null);case 4:return Er(),vi(e,t),e===null&&Vr(t.stateNode.containerInfo),Gt(t),null;case 10:return Qo(t.type._context),Gt(t),null;case 17:return Zt(t.type)&&Rs(),Gt(t),null;case 19:if(st(dt),s=t.memoizedState,s===null)return Gt(t),null;if(a=(t.flags&128)!==0,i=s.rendering,i===null)if(a)is(s,!1);else{if(St!==0||e!==null&&(e.flags&128)!==0)for(e=t.child;e!==null;){if(i=Hs(e),i!==null){for(t.flags|=128,is(s,!1),a=i.updateQueue,a!==null&&(t.updateQueue=a,t.flags|=4),t.subtreeFlags=0,a=n,n=t.child;n!==null;)s=n,e=a,s.flags&=14680066,i=s.alternate,i===null?(s.childLanes=0,s.lanes=e,s.child=null,s.subtreeFlags=0,s.memoizedProps=null,s.memoizedState=null,s.updateQueue=null,s.dependencies=null,s.stateNode=null):(s.childLanes=i.childLanes,s.lanes=i.lanes,s.child=i.child,s.subtreeFlags=0,s.deletions=null,s.memoizedProps=i.memoizedProps,s.memoizedState=i.memoizedState,s.updateQueue=i.updateQueue,s.type=i.type,e=i.dependencies,s.dependencies=e===null?null:{lanes:e.lanes,firstContext:e.firstContext}),n=n.sibling;return nt(dt,dt.current&1|2),t.child}e=e.sibling}s.tail!==null&&p()>Lr&&(t.flags|=128,a=!0,is(s,!1),t.lanes=4194304)}else{if(!a)if(e=Hs(i),e!==null){if(t.flags|=128,a=!0,n=e.updateQueue,n!==null&&(t.updateQueue=n,t.flags|=4),is(s,!0),s.tail===null&&s.tailMode==="hidden"&&!i.alternate&&!it)return Gt(t),null}else 2*p()-s.renderingStartTime>Lr&&n!==1073741824&&(t.flags|=128,a=!0,is(s,!1),t.lanes=4194304);s.isBackwards?(i.sibling=t.child,t.child=i):(n=s.last,n!==null?n.sibling=i:t.child=i,s.last=i)}return s.tail!==null?(t=s.tail,s.rendering=t,s.tail=t.sibling,s.renderingStartTime=p(),t.sibling=null,n=dt.current,nt(dt,a?n&1|2:n&1),t):(Gt(t),null);case 22:case 23:return Ri(),a=t.memoizedState!==null,e!==null&&e.memoizedState!==null!==a&&(t.flags|=8192),a&&(t.mode&1)!==0?(cn&1073741824)!==0&&(Gt(t),t.subtreeFlags&6&&(t.flags|=8192)):Gt(t),null;case 24:return null;case 25:return null}throw Error(h(156,t.tag))}function _p(e,t){switch(jo(t),t.tag){case 1:return Zt(t.type)&&Rs(),e=t.flags,e&65536?(t.flags=e&-65537|128,t):null;case 3:return Er(),st(Jt),st(Kt),Zo(),e=t.flags,(e&65536)!==0&&(e&128)===0?(t.flags=e&-65537|128,t):null;case 5:return Xo(t),null;case 13:if(st(dt),e=t.memoizedState,e!==null&&e.dehydrated!==null){if(t.alternate===null)throw Error(h(340));Sr()}return e=t.flags,e&65536?(t.flags=e&-65537|128,t):null;case 19:return st(dt),null;case 4:return Er(),null;case 10:return Qo(t.type._context),null;case 22:case 23:return Ri(),null;case 24:return null;default:return null}}var Xs=!1,Ht=!1,Ap=typeof WeakSet=="function"?WeakSet:Set,q=null;function Ar(e,t){var n=e.ref;if(n!==null)if(typeof n=="function")try{n(null)}catch(a){ut(e,t,a)}else n.current=null}function bi(e,t,n){try{n()}catch(a){ut(e,t,a)}}var Mc=!1;function Ip(e,t){if(Ro=qn,e=ul(),To(e)){if("selectionStart"in e)var n={start:e.selectionStart,end:e.selectionEnd};else e:{n=(n=e.ownerDocument)&&n.defaultView||window;var a=n.getSelection&&n.getSelection();if(a&&a.rangeCount!==0){n=a.anchorNode;var r=a.anchorOffset,s=a.focusNode;a=a.focusOffset;try{n.nodeType,s.nodeType}catch{n=null;break e}var i=0,c=-1,u=-1,b=0,A=0,I=e,_=null;t:for(;;){for(var H;I!==n||r!==0&&I.nodeType!==3||(c=i+r),I!==s||a!==0&&I.nodeType!==3||(u=i+a),I.nodeType===3&&(i+=I.nodeValue.length),(H=I.firstChild)!==null;)_=I,I=H;for(;;){if(I===e)break t;if(_===n&&++b===r&&(c=i),_===s&&++A===a&&(u=i),(H=I.nextSibling)!==null)break;I=_,_=I.parentNode}I=H}n=c===-1||u===-1?null:{start:c,end:u}}else n=null}n=n||{start:0,end:0}}else n=null;for(xo={focusedElem:e,selectionRange:n},qn=!1,q=t;q!==null;)if(t=q,e=t.child,(t.subtreeFlags&1028)!==0&&e!==null)e.return=t,q=e;else for(;q!==null;){t=q;try{var Y=t.alternate;if((t.flags&1024)!==0)switch(t.tag){case 0:case 11:case 15:break;case 1:if(Y!==null){var $=Y.memoizedProps,ht=Y.memoizedState,f=t.stateNode,m=f.getSnapshotBeforeUpdate(t.elementType===t.type?$:_n(t.type,$),ht);f.__reactInternalSnapshotBeforeUpdate=m}break;case 3:var v=t.stateNode.containerInfo;v.nodeType===1?v.textContent="":v.nodeType===9&&v.documentElement&&v.removeChild(v.documentElement);break;case 5:case 6:case 4:case 17:break;default:throw Error(h(163))}}catch(P){ut(t,t.return,P)}if(e=t.sibling,e!==null){e.return=t.return,q=e;break}q=t.return}return Y=Mc,Mc=!1,Y}function ls(e,t,n){var a=t.updateQueue;if(a=a!==null?a.lastEffect:null,a!==null){var r=a=a.next;do{if((r.tag&e)===e){var s=r.destroy;r.destroy=void 0,s!==void 0&&bi(t,n,s)}r=r.next}while(r!==a)}}function Js(e,t){if(t=t.updateQueue,t=t!==null?t.lastEffect:null,t!==null){var n=t=t.next;do{if((n.tag&e)===e){var a=n.create;n.destroy=a()}n=n.next}while(n!==t)}}function wi(e){var t=e.ref;if(t!==null){var n=e.stateNode;switch(e.tag){case 5:e=n;break;default:e=n}typeof t=="function"?t(e):t.current=e}}function Dc(e){var t=e.alternate;t!==null&&(e.alternate=null,Dc(t)),e.child=null,e.deletions=null,e.sibling=null,e.tag===5&&(t=e.stateNode,t!==null&&(delete t[Bn],delete t[Xr],delete t[Oo],delete t[dp],delete t[pp])),e.stateNode=null,e.return=null,e.dependencies=null,e.memoizedProps=null,e.memoizedState=null,e.pendingProps=null,e.stateNode=null,e.updateQueue=null}function Oc(e){return e.tag===5||e.tag===3||e.tag===4}function Bc(e){e:for(;;){for(;e.sibling===null;){if(e.return===null||Oc(e.return))return null;e=e.return}for(e.sibling.return=e.return,e=e.sibling;e.tag!==5&&e.tag!==6&&e.tag!==18;){if(e.flags&2||e.child===null||e.tag===4)continue e;e.child.return=e,e=e.child}if(!(e.flags&2))return e.stateNode}}function Si(e,t,n){var a=e.tag;if(a===5||a===6)e=e.stateNode,t?n.nodeType===8?n.parentNode.insertBefore(e,t):n.insertBefore(e,t):(n.nodeType===8?(t=n.parentNode,t.insertBefore(e,n)):(t=n,t.appendChild(e)),n=n._reactRootContainer,n!=null||t.onclick!==null||(t.onclick=Ls));else if(a!==4&&(e=e.child,e!==null))for(Si(e,t,n),e=e.sibling;e!==null;)Si(e,t,n),e=e.sibling}function ki(e,t,n){var a=e.tag;if(a===5||a===6)e=e.stateNode,t?n.insertBefore(e,t):n.appendChild(e);else if(a!==4&&(e=e.child,e!==null))for(ki(e,t,n),e=e.sibling;e!==null;)ki(e,t,n),e=e.sibling}var Rt=null,An=!1;function ba(e,t,n){for(n=n.child;n!==null;)Fc(e,t,n),n=n.sibling}function Fc(e,t,n){if(re&&typeof re.onCommitFiberUnmount=="function")try{re.onCommitFiberUnmount(K,n)}catch{}switch(n.tag){case 5:Ht||Ar(n,t);case 6:var a=Rt,r=An;Rt=null,ba(e,t,n),Rt=a,An=r,Rt!==null&&(An?(e=Rt,n=n.stateNode,e.nodeType===8?e.parentNode.removeChild(n):e.removeChild(n)):Rt.removeChild(n.stateNode));break;case 18:Rt!==null&&(An?(e=Rt,n=n.stateNode,e.nodeType===8?Do(e.parentNode,n):e.nodeType===1&&Do(e,n),Sn(e)):Do(Rt,n.stateNode));break;case 4:a=Rt,r=An,Rt=n.stateNode.containerInfo,An=!0,ba(e,t,n),Rt=a,An=r;break;case 0:case 11:case 14:case 15:if(!Ht&&(a=n.updateQueue,a!==null&&(a=a.lastEffect,a!==null))){r=a=a.next;do{var s=r,i=s.destroy;s=s.tag,i!==void 0&&((s&2)!==0||(s&4)!==0)&&bi(n,t,i),r=r.next}while(r!==a)}ba(e,t,n);break;case 1:if(!Ht&&(Ar(n,t),a=n.stateNode,typeof a.componentWillUnmount=="function"))try{a.props=n.memoizedProps,a.state=n.memoizedState,a.componentWillUnmount()}catch(c){ut(n,t,c)}ba(e,t,n);break;case 21:ba(e,t,n);break;case 22:n.mode&1?(Ht=(a=Ht)||n.memoizedState!==null,ba(e,t,n),Ht=a):ba(e,t,n);break;default:ba(e,t,n)}}function Kc(e){var t=e.updateQueue;if(t!==null){e.updateQueue=null;var n=e.stateNode;n===null&&(n=e.stateNode=new Ap),t.forEach(function(a){var r=Bp.bind(null,e,a);n.has(a)||(n.add(a),a.then(r,r))})}}function In(e,t){var n=t.deletions;if(n!==null)for(var a=0;a<n.length;a++){var r=n[a];try{var s=e,i=t,c=i;e:for(;c!==null;){switch(c.tag){case 5:Rt=c.stateNode,An=!1;break e;case 3:Rt=c.stateNode.containerInfo,An=!0;break e;case 4:Rt=c.stateNode.containerInfo,An=!0;break e}c=c.return}if(Rt===null)throw Error(h(160));Fc(s,i,r),Rt=null,An=!1;var u=r.alternate;u!==null&&(u.return=null),r.return=null}catch(b){ut(r,t,b)}}if(t.subtreeFlags&12854)for(t=t.child;t!==null;)jc(t,e),t=t.sibling}function jc(e,t){var n=e.alternate,a=e.flags;switch(e.tag){case 0:case 11:case 14:case 15:if(In(t,e),jn(e),a&4){try{ls(3,e,e.return),Js(3,e)}catch($){ut(e,e.return,$)}try{ls(5,e,e.return)}catch($){ut(e,e.return,$)}}break;case 1:In(t,e),jn(e),a&512&&n!==null&&Ar(n,n.return);break;case 5:if(In(t,e),jn(e),a&512&&n!==null&&Ar(n,n.return),e.flags&32){var r=e.stateNode;try{Qe(r,"")}catch($){ut(e,e.return,$)}}if(a&4&&(r=e.stateNode,r!=null)){var s=e.memoizedProps,i=n!==null?n.memoizedProps:s,c=e.type,u=e.updateQueue;if(e.updateQueue=null,u!==null)try{c==="input"&&s.type==="radio"&&s.name!=null&&Ja(r,s),tr(c,i);var b=tr(c,s);for(i=0;i<u.length;i+=2){var A=u[i],I=u[i+1];A==="style"?vs(r,I):A==="dangerouslySetInnerHTML"?Mr(r,I):A==="children"?Qe(r,I):at(r,A,I,b)}switch(c){case"input":_a(r,s);break;case"textarea":vn(r,s);break;case"select":var _=r._wrapperState.wasMultiple;r._wrapperState.wasMultiple=!!s.multiple;var H=s.value;H!=null?ra(r,!!s.multiple,H,!1):_!==!!s.multiple&&(s.defaultValue!=null?ra(r,!!s.multiple,s.defaultValue,!0):ra(r,!!s.multiple,s.multiple?[]:"",!1))}r[Xr]=s}catch($){ut(e,e.return,$)}}break;case 6:if(In(t,e),jn(e),a&4){if(e.stateNode===null)throw Error(h(162));r=e.stateNode,s=e.memoizedProps;try{r.nodeValue=s}catch($){ut(e,e.return,$)}}break;case 3:if(In(t,e),jn(e),a&4&&n!==null&&n.memoizedState.isDehydrated)try{Sn(t.containerInfo)}catch($){ut(e,e.return,$)}break;case 4:In(t,e),jn(e);break;case 13:In(t,e),jn(e),r=e.child,r.flags&8192&&(s=r.memoizedState!==null,r.stateNode.isHidden=s,!s||r.alternate!==null&&r.alternate.memoizedState!==null||(Ei=p())),a&4&&Kc(e);break;case 22:if(A=n!==null&&n.memoizedState!==null,e.mode&1?(Ht=(b=Ht)||A,In(t,e),Ht=b):In(t,e),jn(e),a&8192){if(b=e.memoizedState!==null,(e.stateNode.isHidden=b)&&!A&&(e.mode&1)!==0)for(q=e,A=e.child;A!==null;){for(I=q=A;q!==null;){switch(_=q,H=_.child,_.tag){case 0:case 11:case 14:case 15:ls(4,_,_.return);break;case 1:Ar(_,_.return);var Y=_.stateNode;if(typeof Y.componentWillUnmount=="function"){a=_,n=_.return;try{t=a,Y.props=t.memoizedProps,Y.state=t.memoizedState,Y.componentWillUnmount()}catch($){ut(a,n,$)}}break;case 5:Ar(_,_.return);break;case 22:if(_.memoizedState!==null){Wc(I);continue}}H!==null?(H.return=_,q=H):Wc(I)}A=A.sibling}e:for(A=null,I=e;;){if(I.tag===5){if(A===null){A=I;try{r=I.stateNode,b?(s=r.style,typeof s.setProperty=="function"?s.setProperty("display","none","important"):s.display="none"):(c=I.stateNode,u=I.memoizedProps.style,i=u!=null&&u.hasOwnProperty("display")?u.display:null,c.style.display=Dr("display",i))}catch($){ut(e,e.return,$)}}}else if(I.tag===6){if(A===null)try{I.stateNode.nodeValue=b?"":I.memoizedProps}catch($){ut(e,e.return,$)}}else if((I.tag!==22&&I.tag!==23||I.memoizedState===null||I===e)&&I.child!==null){I.child.return=I,I=I.child;continue}if(I===e)break e;for(;I.sibling===null;){if(I.return===null||I.return===e)break e;A===I&&(A=null),I=I.return}A===I&&(A=null),I.sibling.return=I.return,I=I.sibling}}break;case 19:In(t,e),jn(e),a&4&&Kc(e);break;case 21:break;default:In(t,e),jn(e)}}function jn(e){var t=e.flags;if(t&2){try{e:{for(var n=e.return;n!==null;){if(Oc(n)){var a=n;break e}n=n.return}throw Error(h(160))}switch(a.tag){case 5:var r=a.stateNode;a.flags&32&&(Qe(r,""),a.flags&=-33);var s=Bc(e);ki(e,s,r);break;case 3:case 4:var i=a.stateNode.containerInfo,c=Bc(e);Si(e,c,i);break;default:throw Error(h(161))}}catch(u){ut(e,e.return,u)}e.flags&=-3}t&4096&&(e.flags&=-4097)}function Lp(e,t,n){q=e,Gc(e)}function Gc(e,t,n){for(var a=(e.mode&1)!==0;q!==null;){var r=q,s=r.child;if(r.tag===22&&a){var i=r.memoizedState!==null||Xs;if(!i){var c=r.alternate,u=c!==null&&c.memoizedState!==null||Ht;c=Xs;var b=Ht;if(Xs=i,(Ht=u)&&!b)for(q=r;q!==null;)i=q,u=i.child,i.tag===22&&i.memoizedState!==null?Uc(r):u!==null?(u.return=i,q=u):Uc(r);for(;s!==null;)q=s,Gc(s),s=s.sibling;q=r,Xs=c,Ht=b}Hc(e)}else(r.subtreeFlags&8772)!==0&&s!==null?(s.return=r,q=s):Hc(e)}}function Hc(e){for(;q!==null;){var t=q;if((t.flags&8772)!==0){var n=t.alternate;try{if((t.flags&8772)!==0)switch(t.tag){case 0:case 11:case 15:Ht||Js(5,t);break;case 1:var a=t.stateNode;if(t.flags&4&&!Ht)if(n===null)a.componentDidMount();else{var r=t.elementType===t.type?n.memoizedProps:_n(t.type,n.memoizedProps);a.componentDidUpdate(r,n.memoizedState,a.__reactInternalSnapshotBeforeUpdate)}var s=t.updateQueue;s!==null&&Wl(t,s,a);break;case 3:var i=t.updateQueue;if(i!==null){if(n=null,t.child!==null)switch(t.child.tag){case 5:n=t.child.stateNode;break;case 1:n=t.child.stateNode}Wl(t,i,n)}break;case 5:var c=t.stateNode;if(n===null&&t.flags&4){n=c;var u=t.memoizedProps;switch(t.type){case"button":case"input":case"select":case"textarea":u.autoFocus&&n.focus();break;case"img":u.src&&(n.src=u.src)}}break;case 6:break;case 4:break;case 12:break;case 13:if(t.memoizedState===null){var b=t.alternate;if(b!==null){var A=b.memoizedState;if(A!==null){var I=A.dehydrated;I!==null&&Sn(I)}}}break;case 19:case 17:case 21:case 22:case 23:case 25:break;default:throw Error(h(163))}Ht||t.flags&512&&wi(t)}catch(_){ut(t,t.return,_)}}if(t===e){q=null;break}if(n=t.sibling,n!==null){n.return=t.return,q=n;break}q=t.return}}function Wc(e){for(;q!==null;){var t=q;if(t===e){q=null;break}var n=t.sibling;if(n!==null){n.return=t.return,q=n;break}q=t.return}}function Uc(e){for(;q!==null;){var t=q;try{switch(t.tag){case 0:case 11:case 15:var n=t.return;try{Js(4,t)}catch(u){ut(t,n,u)}break;case 1:var a=t.stateNode;if(typeof a.componentDidMount=="function"){var r=t.return;try{a.componentDidMount()}catch(u){ut(t,r,u)}}var s=t.return;try{wi(t)}catch(u){ut(t,s,u)}break;case 5:var i=t.return;try{wi(t)}catch(u){ut(t,i,u)}}}catch(u){ut(t,t.return,u)}if(t===e){q=null;break}var c=t.sibling;if(c!==null){c.return=t.return,q=c;break}q=t.return}}var Np=Math.ceil,Zs=O.ReactCurrentDispatcher,Ti=O.ReactCurrentOwner,yn=O.ReactCurrentBatchConfig,De=0,At=null,vt=null,xt=0,cn=0,Ir=ha(0),St=0,cs=null,za=0,eo=0,Ci=0,ds=null,tn=null,Ei=0,Lr=1/0,na=null,to=!1,_i=null,wa=null,no=!1,Sa=null,ao=0,ps=0,Ai=null,ro=-1,so=0;function $t(){return(De&6)!==0?p():ro!==-1?ro:ro=p()}function ka(e){return(e.mode&1)===0?1:(De&2)!==0&&xt!==0?xt&-xt:mp.transition!==null?(so===0&&(so=Ue()),so):(e=ye,e!==0||(e=window.event,e=e===void 0?16:Ss(e.type)),e)}function Ln(e,t,n,a){if(50<ps)throw ps=0,Ai=null,Error(h(185));$e(e,n,a),((De&2)===0||e!==At)&&(e===At&&((De&2)===0&&(eo|=n),St===4&&Ta(e,xt)),nn(e,a),n===1&&De===0&&(t.mode&1)===0&&(Lr=p()+500,Ps&&ya()))}function nn(e,t){var n=e.callbackNode;Ze(e,t);var a=ee(e,e===At?xt:0);if(a===0)n!==null&&S(n),e.callbackNode=null,e.callbackPriority=0;else if(t=a&-a,e.callbackPriority!==t){if(n!=null&&S(n),t===1)e.tag===0?up(Qc.bind(null,e)):Rl(Qc.bind(null,e)),lp(function(){(De&6)===0&&ya()}),n=null;else{switch(ze(a)){case 1:n=R;break;case 4:n=G;break;case 16:n=D;break;case 536870912:n=F;break;default:n=D}n=ed(n,qc.bind(null,e))}e.callbackPriority=t,e.callbackNode=n}}function qc(e,t){if(ro=-1,so=0,(De&6)!==0)throw Error(h(327));var n=e.callbackNode;if(Nr()&&e.callbackNode!==n)return null;var a=ee(e,e===At?xt:0);if(a===0)return null;if((a&30)!==0||(a&e.expiredLanes)!==0||t)t=oo(e,a);else{t=a;var r=De;De|=2;var s=Yc();(At!==e||xt!==t)&&(na=null,Lr=p()+500,Va(e,t));do try{Pp();break}catch(c){zc(e,c)}while(!0);qo(),Zs.current=s,De=r,vt!==null?t=0:(At=null,xt=0,t=St)}if(t!==0){if(t===2&&(r=et(e),r!==0&&(a=r,t=Ii(e,r))),t===1)throw n=cs,Va(e,0),Ta(e,a),nn(e,p()),n;if(t===6)Ta(e,a);else{if(r=e.current.alternate,(a&30)===0&&!Rp(r)&&(t=oo(e,a),t===2&&(s=et(e),s!==0&&(a=s,t=Ii(e,s))),t===1))throw n=cs,Va(e,0),Ta(e,a),nn(e,p()),n;switch(e.finishedWork=r,e.finishedLanes=a,t){case 0:case 1:throw Error(h(345));case 2:$a(e,tn,na);break;case 3:if(Ta(e,a),(a&130023424)===a&&(t=Ei+500-p(),10<t)){if(ee(e,0)!==0)break;if(r=e.suspendedLanes,(r&a)!==a){$t(),e.pingedLanes|=e.suspendedLanes&r;break}e.timeoutHandle=Mo($a.bind(null,e,tn,na),t);break}$a(e,tn,na);break;case 4:if(Ta(e,a),(a&4194240)===a)break;for(t=e.eventTimes,r=-1;0<a;){var i=31-ue(a);s=1<<i,i=t[i],i>r&&(r=i),a&=~s}if(a=r,a=p()-a,a=(120>a?120:480>a?480:1080>a?1080:1920>a?1920:3e3>a?3e3:4320>a?4320:1960*Np(a/1960))-a,10<a){e.timeoutHandle=Mo($a.bind(null,e,tn,na),a);break}$a(e,tn,na);break;case 5:$a(e,tn,na);break;default:throw Error(h(329))}}}return nn(e,p()),e.callbackNode===n?qc.bind(null,e):null}function Ii(e,t){var n=ds;return e.current.memoizedState.isDehydrated&&(Va(e,t).flags|=256),e=oo(e,t),e!==2&&(t=tn,tn=n,t!==null&&Li(t)),e}function Li(e){tn===null?tn=e:tn.push.apply(tn,e)}function Rp(e){for(var t=e;;){if(t.flags&16384){var n=t.updateQueue;if(n!==null&&(n=n.stores,n!==null))for(var a=0;a<n.length;a++){var r=n[a],s=r.getSnapshot;r=r.value;try{if(!Cn(s(),r))return!1}catch{return!1}}}if(n=t.child,t.subtreeFlags&16384&&n!==null)n.return=t,t=n;else{if(t===e)break;for(;t.sibling===null;){if(t.return===null||t.return===e)return!0;t=t.return}t.sibling.return=t.return,t=t.sibling}}return!0}function Ta(e,t){for(t&=~Ci,t&=~eo,e.suspendedLanes|=t,e.pingedLanes&=~t,e=e.expirationTimes;0<t;){var n=31-ue(t),a=1<<n;e[n]=-1,t&=~a}}function Qc(e){if((De&6)!==0)throw Error(h(327));Nr();var t=ee(e,0);if((t&1)===0)return nn(e,p()),null;var n=oo(e,t);if(e.tag!==0&&n===2){var a=et(e);a!==0&&(t=a,n=Ii(e,a))}if(n===1)throw n=cs,Va(e,0),Ta(e,t),nn(e,p()),n;if(n===6)throw Error(h(345));return e.finishedWork=e.current.alternate,e.finishedLanes=t,$a(e,tn,na),nn(e,p()),null}function Ni(e,t){var n=De;De|=1;try{return e(t)}finally{De=n,De===0&&(Lr=p()+500,Ps&&ya())}}function Ya(e){Sa!==null&&Sa.tag===0&&(De&6)===0&&Nr();var t=De;De|=1;var n=yn.transition,a=ye;try{if(yn.transition=null,ye=1,e)return e()}finally{ye=a,yn.transition=n,De=t,(De&6)===0&&ya()}}function Ri(){cn=Ir.current,st(Ir)}function Va(e,t){e.finishedWork=null,e.finishedLanes=0;var n=e.timeoutHandle;if(n!==-1&&(e.timeoutHandle=-1,ip(n)),vt!==null)for(n=vt.return;n!==null;){var a=n;switch(jo(a),a.tag){case 1:a=a.type.childContextTypes,a!=null&&Rs();break;case 3:Er(),st(Jt),st(Kt),Zo();break;case 5:Xo(a);break;case 4:Er();break;case 13:st(dt);break;case 19:st(dt);break;case 10:Qo(a.type._context);break;case 22:case 23:Ri()}n=n.return}if(At=e,vt=e=Ca(e.current,null),xt=cn=t,St=0,cs=null,Ci=eo=za=0,tn=ds=null,Ua!==null){for(t=0;t<Ua.length;t++)if(n=Ua[t],a=n.interleaved,a!==null){n.interleaved=null;var r=a.next,s=n.pending;if(s!==null){var i=s.next;s.next=r,a.next=i}n.pending=a}Ua=null}return e}function zc(e,t){do{var n=vt;try{if(qo(),Ws.current=zs,Us){for(var a=pt.memoizedState;a!==null;){var r=a.queue;r!==null&&(r.pending=null),a=a.next}Us=!1}if(Qa=0,_t=wt=pt=null,as=!1,rs=0,Ti.current=null,n===null||n.return===null){St=1,cs=t,vt=null;break}e:{var s=e,i=n.return,c=n,u=t;if(t=xt,c.flags|=32768,u!==null&&typeof u=="object"&&typeof u.then=="function"){var b=u,A=c,I=A.tag;if((A.mode&1)===0&&(I===0||I===11||I===15)){var _=A.alternate;_?(A.updateQueue=_.updateQueue,A.memoizedState=_.memoizedState,A.lanes=_.lanes):(A.updateQueue=null,A.memoizedState=null)}var H=vc(i);if(H!==null){H.flags&=-257,bc(H,i,c,s,t),H.mode&1&&fc(s,b,t),t=H,u=b;var Y=t.updateQueue;if(Y===null){var $=new Set;$.add(u),t.updateQueue=$}else Y.add(u);break e}else{if((t&1)===0){fc(s,b,t),xi();break e}u=Error(h(426))}}else if(it&&c.mode&1){var ht=vc(i);if(ht!==null){(ht.flags&65536)===0&&(ht.flags|=256),bc(ht,i,c,s,t),Wo(_r(u,c));break e}}s=u=_r(u,c),St!==4&&(St=2),ds===null?ds=[s]:ds.push(s),s=i;do{switch(s.tag){case 3:s.flags|=65536,t&=-t,s.lanes|=t;var f=gc(s,u,t);Hl(s,f);break e;case 1:c=u;var m=s.type,v=s.stateNode;if((s.flags&128)===0&&(typeof m.getDerivedStateFromError=="function"||v!==null&&typeof v.componentDidCatch=="function"&&(wa===null||!wa.has(v)))){s.flags|=65536,t&=-t,s.lanes|=t;var P=yc(s,c,t);Hl(s,P);break e}}s=s.return}while(s!==null)}$c(n)}catch(J){t=J,vt===n&&n!==null&&(vt=n=n.return);continue}break}while(!0)}function Yc(){var e=Zs.current;return Zs.current=zs,e===null?zs:e}function xi(){(St===0||St===3||St===2)&&(St=4),At===null||(za&268435455)===0&&(eo&268435455)===0||Ta(At,xt)}function oo(e,t){var n=De;De|=2;var a=Yc();(At!==e||xt!==t)&&(na=null,Va(e,t));do try{xp();break}catch(r){zc(e,r)}while(!0);if(qo(),De=n,Zs.current=a,vt!==null)throw Error(h(261));return At=null,xt=0,St}function xp(){for(;vt!==null;)Vc(vt)}function Pp(){for(;vt!==null&&!T();)Vc(vt)}function Vc(e){var t=Zc(e.alternate,e,cn);e.memoizedProps=e.pendingProps,t===null?$c(e):vt=t,Ti.current=null}function $c(e){var t=e;do{var n=t.alternate;if(e=t.return,(t.flags&32768)===0){if(n=Ep(n,t,cn),n!==null){vt=n;return}}else{if(n=_p(n,t),n!==null){n.flags&=32767,vt=n;return}if(e!==null)e.flags|=32768,e.subtreeFlags=0,e.deletions=null;else{St=6,vt=null;return}}if(t=t.sibling,t!==null){vt=t;return}vt=t=e}while(t!==null);St===0&&(St=5)}function $a(e,t,n){var a=ye,r=yn.transition;try{yn.transition=null,ye=1,Mp(e,t,n,a)}finally{yn.transition=r,ye=a}return null}function Mp(e,t,n,a){do Nr();while(Sa!==null);if((De&6)!==0)throw Error(h(327));n=e.finishedWork;var r=e.finishedLanes;if(n===null)return null;if(e.finishedWork=null,e.finishedLanes=0,n===e.current)throw Error(h(177));e.callbackNode=null,e.callbackPriority=0;var s=n.lanes|n.childLanes;if(tt(e,s),e===At&&(vt=At=null,xt=0),(n.subtreeFlags&2064)===0&&(n.flags&2064)===0||no||(no=!0,ed(D,function(){return Nr(),null})),s=(n.flags&15990)!==0,(n.subtreeFlags&15990)!==0||s){s=yn.transition,yn.transition=null;var i=ye;ye=1;var c=De;De|=4,Ti.current=null,Ip(e,n),jc(n,e),ep(xo),qn=!!Ro,xo=Ro=null,e.current=n,Lp(n),C(),De=c,ye=i,yn.transition=s}else e.current=n;if(no&&(no=!1,Sa=e,ao=r),s=e.pendingLanes,s===0&&(wa=null),xe(n.stateNode),nn(e,p()),t!==null)for(a=e.onRecoverableError,n=0;n<t.length;n++)r=t[n],a(r.value,{componentStack:r.stack,digest:r.digest});if(to)throw to=!1,e=_i,_i=null,e;return(ao&1)!==0&&e.tag!==0&&Nr(),s=e.pendingLanes,(s&1)!==0?e===Ai?ps++:(ps=0,Ai=e):ps=0,ya(),null}function Nr(){if(Sa!==null){var e=ze(ao),t=yn.transition,n=ye;try{if(yn.transition=null,ye=16>e?16:e,Sa===null)var a=!1;else{if(e=Sa,Sa=null,ao=0,(De&6)!==0)throw Error(h(331));var r=De;for(De|=4,q=e.current;q!==null;){var s=q,i=s.child;if((q.flags&16)!==0){var c=s.deletions;if(c!==null){for(var u=0;u<c.length;u++){var b=c[u];for(q=b;q!==null;){var A=q;switch(A.tag){case 0:case 11:case 15:ls(8,A,s)}var I=A.child;if(I!==null)I.return=A,q=I;else for(;q!==null;){A=q;var _=A.sibling,H=A.return;if(Dc(A),A===b){q=null;break}if(_!==null){_.return=H,q=_;break}q=H}}}var Y=s.alternate;if(Y!==null){var $=Y.child;if($!==null){Y.child=null;do{var ht=$.sibling;$.sibling=null,$=ht}while($!==null)}}q=s}}if((s.subtreeFlags&2064)!==0&&i!==null)i.return=s,q=i;else e:for(;q!==null;){if(s=q,(s.flags&2048)!==0)switch(s.tag){case 0:case 11:case 15:ls(9,s,s.return)}var f=s.sibling;if(f!==null){f.return=s.return,q=f;break e}q=s.return}}var m=e.current;for(q=m;q!==null;){i=q;var v=i.child;if((i.subtreeFlags&2064)!==0&&v!==null)v.return=i,q=v;else e:for(i=m;q!==null;){if(c=q,(c.flags&2048)!==0)try{switch(c.tag){case 0:case 11:case 15:Js(9,c)}}catch(J){ut(c,c.return,J)}if(c===i){q=null;break e}var P=c.sibling;if(P!==null){P.return=c.return,q=P;break e}q=c.return}}if(De=r,ya(),re&&typeof re.onPostCommitFiberRoot=="function")try{re.onPostCommitFiberRoot(K,e)}catch{}a=!0}return a}finally{ye=n,yn.transition=t}}return!1}function Xc(e,t,n){t=_r(n,t),t=gc(e,t,1),e=va(e,t,1),t=$t(),e!==null&&($e(e,1,t),nn(e,t))}function ut(e,t,n){if(e.tag===3)Xc(e,e,n);else for(;t!==null;){if(t.tag===3){Xc(t,e,n);break}else if(t.tag===1){var a=t.stateNode;if(typeof t.type.getDerivedStateFromError=="function"||typeof a.componentDidCatch=="function"&&(wa===null||!wa.has(a))){e=_r(n,e),e=yc(t,e,1),t=va(t,e,1),e=$t(),t!==null&&($e(t,1,e),nn(t,e));break}}t=t.return}}function Dp(e,t,n){var a=e.pingCache;a!==null&&a.delete(t),t=$t(),e.pingedLanes|=e.suspendedLanes&n,At===e&&(xt&n)===n&&(St===4||St===3&&(xt&130023424)===xt&&500>p()-Ei?Va(e,0):Ci|=n),nn(e,t)}function Jc(e,t){t===0&&((e.mode&1)===0?t=1:(t=oe,oe<<=1,(oe&130023424)===0&&(oe=4194304)));var n=$t();e=Zn(e,t),e!==null&&($e(e,t,n),nn(e,n))}function Op(e){var t=e.memoizedState,n=0;t!==null&&(n=t.retryLane),Jc(e,n)}function Bp(e,t){var n=0;switch(e.tag){case 13:var a=e.stateNode,r=e.memoizedState;r!==null&&(n=r.retryLane);break;case 19:a=e.stateNode;break;default:throw Error(h(314))}a!==null&&a.delete(t),Jc(e,n)}var Zc;Zc=function(e,t,n){if(e!==null)if(e.memoizedProps!==t.pendingProps||Jt.current)en=!0;else{if((e.lanes&n)===0&&(t.flags&128)===0)return en=!1,Cp(e,t,n);en=(e.flags&131072)!==0}else en=!1,it&&(t.flags&1048576)!==0&&xl(t,Ds,t.index);switch(t.lanes=0,t.tag){case 2:var a=t.type;$s(e,t),e=t.pendingProps;var r=vr(t,Kt.current);Cr(t,n),r=ni(null,t,a,e,r,n);var s=ai();return t.flags|=1,typeof r=="object"&&r!==null&&typeof r.render=="function"&&r.$$typeof===void 0?(t.tag=1,t.memoizedState=null,t.updateQueue=null,Zt(a)?(s=!0,xs(t)):s=!1,t.memoizedState=r.state!==null&&r.state!==void 0?r.state:null,Vo(t),r.updater=Ys,t.stateNode=r,r._reactInternals=t,ci(t,a,e,n),t=mi(null,t,a,!0,s,n)):(t.tag=0,it&&s&&Ko(t),Vt(null,t,r,n),t=t.child),t;case 16:a=t.elementType;e:{switch($s(e,t),e=t.pendingProps,r=a._init,a=r(a._payload),t.type=a,r=t.tag=Kp(a),e=_n(a,e),r){case 0:t=ui(null,t,a,e,n);break e;case 1:t=Ec(null,t,a,e,n);break e;case 11:t=wc(null,t,a,e,n);break e;case 14:t=Sc(null,t,a,_n(a.type,e),n);break e}throw Error(h(306,a,""))}return t;case 0:return a=t.type,r=t.pendingProps,r=t.elementType===a?r:_n(a,r),ui(e,t,a,r,n);case 1:return a=t.type,r=t.pendingProps,r=t.elementType===a?r:_n(a,r),Ec(e,t,a,r,n);case 3:e:{if(_c(t),e===null)throw Error(h(387));a=t.pendingProps,s=t.memoizedState,r=s.element,Gl(e,t),Gs(t,a,null,n);var i=t.memoizedState;if(a=i.element,s.isDehydrated)if(s={element:a,isDehydrated:!1,cache:i.cache,pendingSuspenseBoundaries:i.pendingSuspenseBoundaries,transitions:i.transitions},t.updateQueue.baseState=s,t.memoizedState=s,t.flags&256){r=_r(Error(h(423)),t),t=Ac(e,t,a,n,r);break e}else if(a!==r){r=_r(Error(h(424)),t),t=Ac(e,t,a,n,r);break e}else for(ln=ma(t.stateNode.containerInfo.firstChild),on=t,it=!0,En=null,n=Kl(t,null,a,n),t.child=n;n;)n.flags=n.flags&-3|4096,n=n.sibling;else{if(Sr(),a===r){t=ta(e,t,n);break e}Vt(e,t,a,n)}t=t.child}return t;case 5:return Ul(t),e===null&&Ho(t),a=t.type,r=t.pendingProps,s=e!==null?e.memoizedProps:null,i=r.children,Po(a,r)?i=null:s!==null&&Po(a,s)&&(t.flags|=32),Cc(e,t),Vt(e,t,i,n),t.child;case 6:return e===null&&Ho(t),null;case 13:return Ic(e,t,n);case 4:return $o(t,t.stateNode.containerInfo),a=t.pendingProps,e===null?t.child=kr(t,null,a,n):Vt(e,t,a,n),t.child;case 11:return a=t.type,r=t.pendingProps,r=t.elementType===a?r:_n(a,r),wc(e,t,a,r,n);case 7:return Vt(e,t,t.pendingProps,n),t.child;case 8:return Vt(e,t,t.pendingProps.children,n),t.child;case 12:return Vt(e,t,t.pendingProps.children,n),t.child;case 10:e:{if(a=t.type._context,r=t.pendingProps,s=t.memoizedProps,i=r.value,nt(Fs,a._currentValue),a._currentValue=i,s!==null)if(Cn(s.value,i)){if(s.children===r.children&&!Jt.current){t=ta(e,t,n);break e}}else for(s=t.child,s!==null&&(s.return=t);s!==null;){var c=s.dependencies;if(c!==null){i=s.child;for(var u=c.firstContext;u!==null;){if(u.context===a){if(s.tag===1){u=ea(-1,n&-n),u.tag=2;var b=s.updateQueue;if(b!==null){b=b.shared;var A=b.pending;A===null?u.next=u:(u.next=A.next,A.next=u),b.pending=u}}s.lanes|=n,u=s.alternate,u!==null&&(u.lanes|=n),zo(s.return,n,t),c.lanes|=n;break}u=u.next}}else if(s.tag===10)i=s.type===t.type?null:s.child;else if(s.tag===18){if(i=s.return,i===null)throw Error(h(341));i.lanes|=n,c=i.alternate,c!==null&&(c.lanes|=n),zo(i,n,t),i=s.sibling}else i=s.child;if(i!==null)i.return=s;else for(i=s;i!==null;){if(i===t){i=null;break}if(s=i.sibling,s!==null){s.return=i.return,i=s;break}i=i.return}s=i}Vt(e,t,r.children,n),t=t.child}return t;case 9:return r=t.type,a=t.pendingProps.children,Cr(t,n),r=hn(r),a=a(r),t.flags|=1,Vt(e,t,a,n),t.child;case 14:return a=t.type,r=_n(a,t.pendingProps),r=_n(a.type,r),Sc(e,t,a,r,n);case 15:return kc(e,t,t.type,t.pendingProps,n);case 17:return a=t.type,r=t.pendingProps,r=t.elementType===a?r:_n(a,r),$s(e,t),t.tag=1,Zt(a)?(e=!0,xs(t)):e=!1,Cr(t,n),mc(t,a,r),ci(t,a,r,n),mi(null,t,a,!0,e,n);case 19:return Nc(e,t,n);case 22:return Tc(e,t,n)}throw Error(h(156,t.tag))};function ed(e,t){return g(e,t)}function Fp(e,t,n,a){this.tag=e,this.key=n,this.sibling=this.child=this.return=this.stateNode=this.type=this.elementType=null,this.index=0,this.ref=null,this.pendingProps=t,this.dependencies=this.memoizedState=this.updateQueue=this.memoizedProps=null,this.mode=a,this.subtreeFlags=this.flags=0,this.deletions=null,this.childLanes=this.lanes=0,this.alternate=null}function fn(e,t,n,a){return new Fp(e,t,n,a)}function Pi(e){return e=e.prototype,!(!e||!e.isReactComponent)}function Kp(e){if(typeof e=="function")return Pi(e)?1:0;if(e!=null){if(e=e.$$typeof,e===ct)return 11;if(e===we)return 14}return 2}function Ca(e,t){var n=e.alternate;return n===null?(n=fn(e.tag,t,e.key,e.mode),n.elementType=e.elementType,n.type=e.type,n.stateNode=e.stateNode,n.alternate=e,e.alternate=n):(n.pendingProps=t,n.type=e.type,n.flags=0,n.subtreeFlags=0,n.deletions=null),n.flags=e.flags&14680064,n.childLanes=e.childLanes,n.lanes=e.lanes,n.child=e.child,n.memoizedProps=e.memoizedProps,n.memoizedState=e.memoizedState,n.updateQueue=e.updateQueue,t=e.dependencies,n.dependencies=t===null?null:{lanes:t.lanes,firstContext:t.firstContext},n.sibling=e.sibling,n.index=e.index,n.ref=e.ref,n}function io(e,t,n,a,r,s){var i=2;if(a=e,typeof e=="function")Pi(e)&&(i=1);else if(typeof e=="string")i=5;else e:switch(e){case me:return Xa(n.children,r,s,t);case Re:i=8,r|=8;break;case Ge:return e=fn(12,n,t,r|2),e.elementType=Ge,e.lanes=s,e;case Z:return e=fn(13,n,t,r),e.elementType=Z,e.lanes=s,e;case L:return e=fn(19,n,t,r),e.elementType=L,e.lanes=s,e;case Fe:return lo(n,r,s,t);default:if(typeof e=="object"&&e!==null)switch(e.$$typeof){case Pe:i=10;break e;case We:i=9;break e;case ct:i=11;break e;case we:i=14;break e;case Be:i=16,a=null;break e}throw Error(h(130,e==null?e:typeof e,""))}return t=fn(i,n,t,r),t.elementType=e,t.type=a,t.lanes=s,t}function Xa(e,t,n,a){return e=fn(7,e,a,t),e.lanes=n,e}function lo(e,t,n,a){return e=fn(22,e,a,t),e.elementType=Fe,e.lanes=n,e.stateNode={isHidden:!1},e}function Mi(e,t,n){return e=fn(6,e,null,t),e.lanes=n,e}function Di(e,t,n){return t=fn(4,e.children!==null?e.children:[],e.key,t),t.lanes=n,t.stateNode={containerInfo:e.containerInfo,pendingChildren:null,implementation:e.implementation},t}function jp(e,t,n,a,r){this.tag=t,this.containerInfo=e,this.finishedWork=this.pingCache=this.current=this.pendingChildren=null,this.timeoutHandle=-1,this.callbackNode=this.pendingContext=this.context=null,this.callbackPriority=0,this.eventTimes=qe(0),this.expirationTimes=qe(-1),this.entangledLanes=this.finishedLanes=this.mutableReadLanes=this.expiredLanes=this.pingedLanes=this.suspendedLanes=this.pendingLanes=0,this.entanglements=qe(0),this.identifierPrefix=a,this.onRecoverableError=r,this.mutableSourceEagerHydrationData=null}function Oi(e,t,n,a,r,s,i,c,u){return e=new jp(e,t,n,c,u),t===1?(t=1,s===!0&&(t|=8)):t=0,s=fn(3,null,null,t),e.current=s,s.stateNode=e,s.memoizedState={element:a,isDehydrated:n,cache:null,transitions:null,pendingSuspenseBoundaries:null},Vo(s),e}function Gp(e,t,n){var a=3<arguments.length&&arguments[3]!==void 0?arguments[3]:null;return{$$typeof:pe,key:a==null?null:""+a,children:e,containerInfo:t,implementation:n}}function td(e){if(!e)return ga;e=e._reactInternals;e:{if(pn(e)!==e||e.tag!==1)throw Error(h(170));var t=e;do{switch(t.tag){case 3:t=t.stateNode.context;break e;case 1:if(Zt(t.type)){t=t.stateNode.__reactInternalMemoizedMergedChildContext;break e}}t=t.return}while(t!==null);throw Error(h(171))}if(e.tag===1){var n=e.type;if(Zt(n))return Ll(e,n,t)}return t}function nd(e,t,n,a,r,s,i,c,u){return e=Oi(n,a,!0,e,r,s,i,c,u),e.context=td(null),n=e.current,a=$t(),r=ka(n),s=ea(a,r),s.callback=t??null,va(n,s,r),e.current.lanes=r,$e(e,r,a),nn(e,a),e}function co(e,t,n,a){var r=t.current,s=$t(),i=ka(r);return n=td(n),t.context===null?t.context=n:t.pendingContext=n,t=ea(s,i),t.payload={element:e},a=a===void 0?null:a,a!==null&&(t.callback=a),e=va(r,t,i),e!==null&&(Ln(e,r,i,s),js(e,r,i)),i}function po(e){if(e=e.current,!e.child)return null;switch(e.child.tag){case 5:return e.child.stateNode;default:return e.child.stateNode}}function ad(e,t){if(e=e.memoizedState,e!==null&&e.dehydrated!==null){var n=e.retryLane;e.retryLane=n!==0&&n<t?n:t}}function Bi(e,t){ad(e,t),(e=e.alternate)&&ad(e,t)}function Hp(){return null}var rd=typeof reportError=="function"?reportError:function(e){console.error(e)};function Fi(e){this._internalRoot=e}uo.prototype.render=Fi.prototype.render=function(e){var t=this._internalRoot;if(t===null)throw Error(h(409));co(e,t,null,null)},uo.prototype.unmount=Fi.prototype.unmount=function(){var e=this._internalRoot;if(e!==null){this._internalRoot=null;var t=e.containerInfo;Ya(function(){co(null,e,null,null)}),t[Vn]=null}};function uo(e){this._internalRoot=e}uo.prototype.unstable_scheduleHydration=function(e){if(e){var t=Dt();e={blockedOn:null,target:e,priority:t};for(var n=0;n<ft.length&&t!==0&&t<ft[n].priority;n++);ft.splice(n,0,e),n===0&&Qt(e)}};function Ki(e){return!(!e||e.nodeType!==1&&e.nodeType!==9&&e.nodeType!==11)}function mo(e){return!(!e||e.nodeType!==1&&e.nodeType!==9&&e.nodeType!==11&&(e.nodeType!==8||e.nodeValue!==" react-mount-point-unstable "))}function sd(){}function Wp(e,t,n,a,r){if(r){if(typeof a=="function"){var s=a;a=function(){var b=po(i);s.call(b)}}var i=nd(t,a,e,0,null,!1,!1,"",sd);return e._reactRootContainer=i,e[Vn]=i.current,Vr(e.nodeType===8?e.parentNode:e),Ya(),i}for(;r=e.lastChild;)e.removeChild(r);if(typeof a=="function"){var c=a;a=function(){var b=po(u);c.call(b)}}var u=Oi(e,0,!1,null,null,!1,!1,"",sd);return e._reactRootContainer=u,e[Vn]=u.current,Vr(e.nodeType===8?e.parentNode:e),Ya(function(){co(t,u,n,a)}),u}function ho(e,t,n,a,r){var s=n._reactRootContainer;if(s){var i=s;if(typeof r=="function"){var c=r;r=function(){var u=po(i);c.call(u)}}co(t,i,e,r)}else i=Wp(n,t,e,r,a);return po(i)}bt=function(e){switch(e.tag){case 3:var t=e.stateNode;if(t.current.memoizedState.isDehydrated){var n=U(t.pendingLanes);n!==0&&(ot(t,n|1),nn(t,p()),(De&6)===0&&(Lr=p()+500,ya()))}break;case 13:Ya(function(){var a=Zn(e,1);if(a!==null){var r=$t();Ln(a,e,1,r)}}),Bi(e,1)}},Mt=function(e){if(e.tag===13){var t=Zn(e,134217728);if(t!==null){var n=$t();Ln(t,e,134217728,n)}Bi(e,134217728)}},sn=function(e){if(e.tag===13){var t=ka(e),n=Zn(e,t);if(n!==null){var a=$t();Ln(n,e,t,a)}Bi(e,t)}},Dt=function(){return ye},yt=function(e,t){var n=ye;try{return ye=e,t()}finally{ye=n}},Br=function(e,t,n){switch(t){case"input":if(_a(e,n),t=n.name,n.type==="radio"&&t!=null){for(n=e;n.parentNode;)n=n.parentNode;for(n=n.querySelectorAll("input[name="+JSON.stringify(""+t)+'][type="radio"]'),t=0;t<n.length;t++){var a=n[t];if(a!==e&&a.form===e.form){var r=Ns(a);if(!r)throw Error(h(90));Rr(a),_a(a,r)}}}break;case"textarea":vn(e,n);break;case"select":t=n.value,t!=null&&ra(e,!!n.multiple,t,!1)}},Fr=Ni,gt=Ya;var Up={usingClientEntryPoint:!1,Events:[Jr,yr,Ns,bs,Ct,Ni]},us={findFiberByHostInstance:ja,bundleType:0,version:"18.3.1",rendererPackageName:"react-dom"},qp={bundleType:us.bundleType,version:us.version,rendererPackageName:us.rendererPackageName,rendererConfig:us.rendererConfig,overrideHookState:null,overrideHookStateDeletePath:null,overrideHookStateRenamePath:null,overrideProps:null,overridePropsDeletePath:null,overridePropsRenamePath:null,setErrorHandler:null,setSuspenseHandler:null,scheduleUpdate:null,currentDispatcherRef:O.ReactCurrentDispatcher,findHostInstanceByFiber:function(e){return e=la(e),e===null?null:e.stateNode},findFiberByHostInstance:us.findFiberByHostInstance||Hp,findHostInstancesForRefresh:null,scheduleRefresh:null,scheduleRoot:null,setRefreshHandler:null,getCurrentFiber:null,reconcilerVersion:"18.3.1-next-f1338f8080-20240426"};if(typeof __REACT_DEVTOOLS_GLOBAL_HOOK__<"u"){var go=__REACT_DEVTOOLS_GLOBAL_HOOK__;if(!go.isDisabled&&go.supportsFiber)try{K=go.inject(qp),re=go}catch{}}return an.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED=Up,an.createPortal=function(e,t){var n=2<arguments.length&&arguments[2]!==void 0?arguments[2]:null;if(!Ki(t))throw Error(h(200));return Gp(e,t,null,n)},an.createRoot=function(e,t){if(!Ki(e))throw Error(h(299));var n=!1,a="",r=rd;return t!=null&&(t.unstable_strictMode===!0&&(n=!0),t.identifierPrefix!==void 0&&(a=t.identifierPrefix),t.onRecoverableError!==void 0&&(r=t.onRecoverableError)),t=Oi(e,1,!1,null,null,n,!1,a,r),e[Vn]=t.current,Vr(e.nodeType===8?e.parentNode:e),new Fi(t)},an.findDOMNode=function(e){if(e==null)return null;if(e.nodeType===1)return e;var t=e._reactInternals;if(t===void 0)throw typeof e.render=="function"?Error(h(188)):(e=Object.keys(e).join(","),Error(h(268,e)));return e=la(t),e=e===null?null:e.stateNode,e},an.flushSync=function(e){return Ya(e)},an.hydrate=function(e,t,n){if(!mo(t))throw Error(h(200));return ho(null,e,t,!0,n)},an.hydrateRoot=function(e,t,n){if(!Ki(e))throw Error(h(405));var a=n!=null&&n.hydratedSources||null,r=!1,s="",i=rd;if(n!=null&&(n.unstable_strictMode===!0&&(r=!0),n.identifierPrefix!==void 0&&(s=n.identifierPrefix),n.onRecoverableError!==void 0&&(i=n.onRecoverableError)),t=nd(t,null,e,1,n??null,r,!1,s,i),e[Vn]=t.current,Vr(e),a)for(e=0;e<a.length;e++)n=a[e],r=n._getVersion,r=r(n._source),t.mutableSourceEagerHydrationData==null?t.mutableSourceEagerHydrationData=[n,r]:t.mutableSourceEagerHydrationData.push(n,r);return new uo(t)},an.render=function(e,t,n){if(!mo(t))throw Error(h(200));return ho(null,e,t,!1,n)},an.unmountComponentAtNode=function(e){if(!mo(e))throw Error(h(40));return e._reactRootContainer?(Ya(function(){ho(null,null,e,!1,function(){e._reactRootContainer=null,e[Vn]=null})}),!0):!1},an.unstable_batchedUpdates=Ni,an.unstable_renderSubtreeIntoContainer=function(e,t,n,a){if(!mo(n))throw Error(h(200));if(e==null||e._reactInternals===void 0)throw Error(h(38));return ho(e,t,n,!1,a)},an.version="18.3.1-next-f1338f8080-20240426",an}var md;function Zp(){if(md)return Hi.exports;md=1;function d(){if(!(typeof __REACT_DEVTOOLS_GLOBAL_HOOK__>"u"||typeof __REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE!="function"))try{__REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE(d)}catch(w){console.error(w)}}return d(),Hi.exports=Jp(),Hi.exports}var hd;function eu(){if(hd)return yo;hd=1;var d=Zp();return yo.createRoot=d.createRoot,yo.hydrateRoot=d.hydrateRoot,yo}var gd=eu(),lt=Vi();const tu="/",nu="/architectures",au="/ontology",ru="/benchmarks",su="/benchmarks/",yd="v1.106.3",ou="/version.json",Qi="v",iu="footerCeVersion",lu={cache:"no-cache"};function fd(d){const h=(d.startsWith(Qi)?d.slice(1):d).split(".").map(Q=>parseInt(Q,10)||0);return[h[0]||0,h[1]||0,h[2]||0]}function cu(d,w){const[h,Q,j]=fd(d),[x,X,de]=fd(w);return h!==x?h>x:Q!==X?Q>X:j>=de}const du=({currentRoute:d})=>{const[w,h]=lt.useState(yd),[Q,j]=lt.useState(!1);lt.useEffect(()=>{const W=()=>{j(window.scrollY>8)};return window.addEventListener("scroll",W,{passive:!0}),W(),()=>window.removeEventListener("scroll",W)},[]),lt.useEffect(()=>{fetch(ou,lu).then(W=>W.ok?W.json():null).then(W=>{const k=W&&(W.tag_name||W.version);if(k){const be=k.startsWith(Qi)?k:`${Qi}${k}`;if(cu(be,yd)){h(be);const Ae=document.getElementById(iu);Ae&&(Ae.textContent=be)}}}).catch(()=>{})},[d]);const x=d===tu,X=d===nu,de=d===au,te=d===ru||d.startsWith(su),ve=x?"#readers":"/#readers",ke=x?"#playground":"/#playground",he=x?"#gates":"/#gates",Ce=x?"#install":"/#install";return l.jsxs(l.Fragment,{children:[l.jsx("a",{className:"skip-link",href:"#main-content",children:"Skip to content"}),l.jsx("header",{className:`site-nav${Q?" is-scrolled":""}`,children:l.jsxs("div",{className:"wrap nav-bar",children:[l.jsxs("a",{href:"/",className:"wordmark","aria-label":"CodeExplorer home",children:[l.jsxs("svg",{className:"wordmark-glyph",viewBox:"0 0 32 32","aria-hidden":"true",children:[l.jsx("circle",{cx:"16",cy:"16",r:"13",fill:"none",stroke:"currentColor",strokeWidth:"1.5",strokeDasharray:"2 3"}),l.jsx("circle",{cx:"16",cy:"16",r:"7.5",fill:"none",stroke:"currentColor",strokeWidth:"1.5",opacity:".55"}),l.jsx("circle",{cx:"16",cy:"16",r:"3",fill:"var(--signal)"}),l.jsx("circle",{cx:"27.2",cy:"9.5",r:"2",fill:"var(--svc)"}),l.jsx("circle",{cx:"6",cy:"22",r:"2",fill:"var(--db)"})]}),l.jsx("span",{children:"CodeExplorer"})]}),l.jsx("nav",{"aria-label":"Primary",children:l.jsxs("ul",{className:"nav-links",children:[l.jsx("li",{className:"nav-anchor-item",children:l.jsx("a",{href:ve,children:"Product"})}),l.jsx("li",{className:"nav-anchor-item",children:l.jsx("a",{href:ke,children:"Query"})}),l.jsx("li",{className:"nav-anchor-item",children:l.jsx("a",{href:he,children:"Gates"})}),l.jsx("li",{children:l.jsx("a",{href:"/architectures","aria-current":X?"page":void 0,children:"Architectures"})}),l.jsx("li",{children:l.jsx("a",{href:"/ontology","aria-current":de?"page":void 0,children:"Ontology"})}),l.jsx("li",{children:l.jsx("a",{href:"/benchmarks","aria-current":te?"page":void 0,children:"Benchmarks"})}),l.jsx("li",{children:l.jsx("a",{href:"https://github.com/vmikhailov/code-explorer",target:"_blank",rel:"noopener",children:"GitHub"})})]})}),l.jsxs("div",{className:"nav-actions",children:[l.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/releases/latest",target:"_blank",rel:"noopener",className:"nav-version",id:"heroReleaseBadge","aria-label":"Latest CodeExplorer release",children:w}),l.jsx("a",{href:Ce,className:"btn btn--primary btn--sm",children:"Install"})]})]})})]})},pu="v1.106.3",uu=({versionLabel:d=pu})=>l.jsx("footer",{className:"site-footer",children:l.jsxs("div",{className:"wrap footer-grid",children:[l.jsxs("div",{className:"footer-brand",children:[l.jsx("a",{href:"/",className:"wordmark-text",children:"CodeExplorer"}),l.jsx("p",{children:"Local code architecture graph for VS Code, MCP coding agents, and CI invariant checks. Free and open source under the MIT License."})]}),l.jsxs("ul",{className:"footer-links",children:[l.jsx("li",{children:l.jsx("a",{href:"https://github.com/vmikhailov/code-explorer",target:"_blank",rel:"noopener",children:"GitHub"})}),l.jsx("li",{children:l.jsx("a",{href:"https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode",target:"_blank",rel:"noopener",children:"VS Code Marketplace"})}),l.jsx("li",{children:l.jsx("a",{href:"https://open-vsx.org/extension/vmikhailov/code-explorer-vscode",target:"_blank",rel:"noopener",children:"Open VSX Registry"})}),l.jsx("li",{children:l.jsx("a",{href:"/benchmarks",children:"Benchmarks"})}),l.jsx("li",{children:l.jsx("a",{href:"/ontology",children:"Graph ontology"})}),l.jsx("li",{children:l.jsx("a",{href:"/architectures",children:"Architecture gate packs"})}),l.jsx("li",{children:l.jsx("a",{href:"https://www.nuget.org/packages/codeexplorer.cli",target:"_blank",rel:"noopener",children:"NuGet (codeexplorer.cli)"})}),l.jsx("li",{children:l.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/releases",target:"_blank",rel:"noopener",children:"Release notes"})}),l.jsx("li",{children:l.jsx("a",{href:"/privacy",children:"Privacy Policy"})})]}),l.jsx("p",{className:"footer-version",id:"footerCeVersion",children:d})]})}),mu=({currentRoute:d,children:w,afterFooter:h})=>l.jsxs(l.Fragment,{children:[l.jsx(du,{currentRoute:d}),w,l.jsx(uu,{}),h]}),aa={"/":{routePath:"/",pageKind:"home",title:"CodeExplorer (ce): Live Architecture Maps, Agent MCP & CI Gates",description:"Map services and SQL tables from source code in VS Code, query call chains and data lineage via MCP, and check architectural rules in CI.",canonical:"https://code-explorer.app/",ogUrl:"https://code-explorer.app/",ogTitle:"CodeExplorer (ce): Live Architecture Maps, Agent MCP & CI Gates",ogDescription:"Architecture maps in VS Code, structural MCP tools for coding agents, and CI invariant checks.",mainHtml:`<!-- 1. Hero: split layout with dissolving radial orbit map -->
    <section class="hero" aria-labelledby="hero-title">
      <div class="wrap hero-grid">
        <div class="hero-copy">
          <h1 id="hero-title" class="display">Understand, query, and guard your architecture. <span class="tone-accent">For you and your agent.</span></h1>
          <p class="hero-lede">
            CodeExplorer indexes your repositories into one local SQLite database. Inspect architecture diagrams in VS Code, expose structural queries to coding agents over MCP, and check layer boundaries in CI.
          </p>
          <div class="hero-actions">
            <a href="#install" class="btn btn--primary">Installation</a>
            <button type="button" class="cmd-chip" data-copy="brew install vmikhailov/tap/ce" aria-label="Copy Homebrew install command">
              <span class="cmd-prompt" aria-hidden="true">$</span>
              <code>brew install vmikhailov/tap/ce</code>
              <span class="cmd-copy">Copy</span>
            </button>
          </div>
          <ul class="hero-facts">
            <li>Open source, MIT</li>
            <li>Runs 100% locally</li>
            <li>12 languages</li>
            <li>16 MCP tools</li>
          </ul>
        </div>

        <figure class="hero-map" data-lightbox-src="/all_services.webp?v=3" title="Click to enlarge System Architecture Map">
          <picture>
            <source srcset="/all_services.webp?v=3" type="image/webp">
            <img src="/all_services.png?v=3" width="2500" height="2500" alt="CodeExplorer orbit map of services, queues, and SQL tables built from source code" fetchpriority="high">
          </picture>
          <figcaption class="map-caption">
            <span class="map-legend">
              <span class="key key--svc">Services</span>
              <span class="key key--queue">Queues</span>
              <span class="key key--db">Databases</span>
            </span>
            <span class="map-source">Built straight from source code. Click map to enlarge.</span>
          </figcaption>
        </figure>
      </div>
    </section>

    <!-- 2. One graph, three readers -->
    <section class="section" id="readers" aria-labelledby="readers-title">
      <div class="wrap">
        <div class="section-head">
          <h2 id="readers-title" class="h2">One graph for engineers and AI agents.</h2>
          <p class="section-lede">
            CodeExplorer scans your repository once into <code>.codeexplorer/graph.db</code> so your IDE, coding agent, and pull-request checks read the same structural graph.
          </p>
        </div>

        <div class="readers">
          <div class="reader-list" role="tablist" aria-label="How CodeExplorer is used" aria-orientation="vertical">
            <button type="button" role="tab" class="reader-tab" id="tab-you" aria-controls="panel-you" aria-selected="true">
              <span class="reader-num">01</span>
              <span class="reader-text">
                <span class="reader-title">Understand It Yourself</span>
                <span class="reader-desc">Service, REST/gRPC, message queue, and SQL table diagrams built straight from source code, without hand-maintained schemas going stale.</span>
              </span>
            </button>
            <button type="button" role="tab" class="reader-tab" id="tab-agent" aria-controls="panel-agent" aria-selected="false" tabindex="-1">
              <span class="reader-num">02</span>
              <span class="reader-text">
                <span class="reader-title">Give Memory to Your AI</span>
                <span class="reader-desc">Attach <code>ce mcp</code> to Claude Code or Cursor. Your agent queries exact call chains, one-shot task context packs, and blast radius in milliseconds instead of blind <code>grep</code>.</span>
              </span>
            </button>
            <button type="button" role="tab" class="reader-tab" id="tab-ci" aria-controls="panel-ci" aria-selected="false" tabindex="-1">
              <span class="reader-num">03</span>
              <span class="reader-text">
                <span class="reader-title">Guard Architecture in CI</span>
                <span class="reader-desc">Enforce custom and built-in architectural rules on every PR. Freeze existing legacy debt as a baseline ratchet (<code>ce gate ratchet</code>) so CI stays green while blocking new violations.</span>
              </span>
            </button>
          </div>

          <div class="reader-stage">
            <div class="reader-panel" role="tabpanel" id="panel-you" aria-labelledby="tab-you">
              <img class="shot" src="/project_flow.png?v=2" width="2699" height="2033" loading="lazy" data-lightbox-src="/project_flow.png?v=2" title="Click to enlarge Project Flow view" alt="CodeExplorer Project Flow view in VS Code showing a service with its PostgreSQL tables, Pub/Sub topics, cache, and libraries">
            </div>

            <div class="reader-panel" role="tabpanel" id="panel-agent" aria-labelledby="tab-agent" hidden>
              <div class="transcript">
                <div class="transcript-top">
                  <span>stdio MCP · <code>ce mcp</code></span>
                  <button type="button" class="link-quiet" data-copy-mcp>Copy MCP JSON</button>
                </div>
                <p class="t-user">Who breaks if I change <code>PaymentProcessor.ChargeAsync</code>?</p>
                <div class="t-call">
                  <span class="t-label">tool call</span>
                  <code>get_call_chain(symbol: "ChargeAsync", direction: "callers", depth: 4)</code>
                </div>
                <div class="t-result">
                  <span class="t-label">result · 4 rows in 3.8 ms</span>
                  <ol class="t-rows">
                    <li><code>CaptureOrderHandler.Handle</code><span>1 hop</span></li>
                    <li><code>CheckoutController.SubmitOrder</code><span>2 hops</span></li>
                    <li><code>RenewalConsumer.Consume</code><span>2 hops</span></li>
                    <li><code>MobileCheckoutEndpoint.Execute</code><span>3 hops</span></li>
                  </ol>
                </div>
                <p class="t-agent">Four callers across Billing, Orders, Subscriptions, and the mobile gateway. I will update the two direct callers and verify the renewal consumer tests.</p>
              </div>
            </div>

            <div class="reader-panel" role="tabpanel" id="panel-ci" aria-labelledby="tab-ci" hidden>
              <pre class="terminal"><span class="dim"># 1. Index polyglot workspace into .codeexplorer/graph.db</span>
<span class="dim">$</span> ce scan
<span class="ok">Indexed 452 modules, 18,420 types, 142,810 edges in 3.4s</span>

<span class="dim"># 2. Check Semantic Invariant Gates &amp; affected tests</span>
<span class="dim">$</span> ce gate check &amp;&amp; ce test impact --git -f commands
<span class="ok">pass</span>  SIG-CA-B-001  Domain Layer Must Not Reference Infrastructure
<span class="ok">pass</span>  SIG-DDD-001   No Shared Table Writes Across Services
<span class="bad">fail</span>  SIG-AST-012   No Blocking .Result or .Wait() Inside Async Functions
        Payments.Api.WebhookController.HandleStripeAsync
        <span class="dim">src/Payments.Api/Controllers/WebhookController.cs:112</span>

<span class="bad">1 new violation.</span> <span class="dim">2 legacy violations tolerated by baseline. Exit 1.</span></pre>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 3. Proof: paired agent benchmarks -->
    <section class="section section--deep" id="benchmark" aria-labelledby="proof-title">
      <div class="wrap">
        <div class="proof-grid">
          <div class="proof-lead">
            <h2 id="proof-title" class="h2">Stop letting agents guess in the dark.</h2>
            <p class="big-number" data-bind="index.card_speed_comp">~7.0×</p>
            <p class="big-number-label">faster time to answer</p>
            <p class="proof-note" data-bind="index.card_speed_copy">Answered in 1m 33s vs 9m 15s on twentyhq/twenty (7 vs 28 turns) and 4m 16s vs 29m 52s across 324 services (18 vs 285 turns).</p>
            <p class="proof-stat-inline">
              <strong class="ok" data-bind="index.card_recall_stat">100% recall</strong> on paired architectural audits (<strong class="ok" data-bind="index.card_arch_recall">~98%</strong> across ORM queries, raw SQL tables, and message broker topics where AST-only indexers and <code>ripgrep</code> miss callers and services).
            </p>
            <a href="/benchmarks" class="link-quiet">Explore all 10 benchmark reports</a>
          </div>

          <div class="races">
            <article class="race">
              <div class="race-head">
                <h3 class="race-title" data-bind-html="index.cs01_head_html">Case 01: <code>twentyhq/twenty</code> (24,527 files)</h3>
                <a href="/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty" class="link-quiet">Report</a>
              </div>
              <div class="race-row">
                <div class="race-meta">
                  <span class="race-who"><code>codebase-memory-mcp</code> + AST scripts</span>
                  <span class="race-val" data-bind="index.cs01_cbm_val">9m 15s, 28 turns, 3.4% graph recall (13/381)</span>
                </div>
                <div class="bar"><span class="bar-fill bar-fill--other" style="--w: 1"></span></div>
              </div>
              <div class="race-row race-row--ce">
                <div class="race-meta">
                  <span class="race-who race-who--ce">CodeExplorer MCP</span>
                  <span class="race-val" data-bind="index.cs01_ce_val">1m 33s (~6.0× faster), 7 turns, 100% recall (381/381)</span>
                </div>
                <div class="bar"><span class="bar-fill bar-fill--ce" style="--w: 0.168"></span></div>
              </div>
              <p class="race-foot">
                Recall across 381 ground-truth items: <span data-bind="index.cs01_cbm_rec">3.4% (13/381) from CBM graph</span> vs <strong data-bind="index.cs01_ce_rec">100% (381/381) directly from graph.db</strong> (0 source file reads).
              </p>
            </article>

            <article class="race">
              <div class="race-head">
                <h3 class="race-title" data-bind-html="index.cs02_head_html">Case 02: 324-service workspace (13,782 files)</h3>
                <a href="/benchmarks/02-enterprise-microservices-deep-audit" class="link-quiet">Report</a>
              </div>
              <div class="race-row">
                <div class="race-meta">
                  <span class="race-who" data-bind-html="index.cs02_rg_label_html"><code>ripgrep</code> + Python scripts</span>
                  <span class="race-val" data-bind="index.cs02_rg_val">29m 52s, 285 turns, ~82% recall (missed 9 services)</span>
                </div>
                <div class="bar"><span class="bar-fill bar-fill--other" style="--w: 1"></span></div>
              </div>
              <div class="race-row race-row--ce">
                <div class="race-meta">
                  <span class="race-who race-who--ce" data-bind-html="index.cs02_ce_label"><code>CodeExplorer</code> (10 CLI commands + 16 file reads, 0 scripts, 0 compactions)</span>
                  <span class="race-val" data-bind="index.cs02_ce_val">4m 16s (~7.0× faster), 18 turns, 100% recall (0 missed)</span>
                </div>
                <div class="bar"><span class="bar-fill bar-fill--ce" style="--w: 0.143"></span></div>
              </div>
              <p class="race-foot">Paired agent runs with identical system prompts. Bars show wall-clock time to answer.</p>
            </article>
          </div>
        </div>
      </div>
    </section>

    <!-- 4. Cypher Query Playground -->
    <section class="section" id="playground" aria-labelledby="query-title">
      <div class="wrap">
        <div class="section-head">
          <h2 id="query-title" class="h2">Query your architecture with Cypher.</h2>
          <p class="section-lede">
            Ask structural and cross-service questions in Cypher instead of chaining grep across dozens of files.
          </p>
        </div>

        <div class="bench">
          <div class="chips" role="tablist" aria-label="Cypher query presets">
            <button type="button" role="tab" class="chip active" data-preset="blast_radius" aria-selected="true">Blast Radius (*1..4)</button>
            <button type="button" role="tab" class="chip" data-preset="cqrs_pipeline" aria-selected="false" tabindex="-1">CQRS Command to Handler</button>
            <button type="button" role="tab" class="chip" data-preset="api_to_db" aria-selected="false" tabindex="-1">Service to SQL Lineage</button>
            <button type="button" role="tab" class="chip" data-preset="cyclic_deps" aria-selected="false" tabindex="-1">Cross-Module Cycles</button>
          </div>

          <div class="bench-split">
            <div class="pane">
              <div class="pane-head">
                <span id="queryPresetTag" class="measured">blast_radius.cypher</span>
                <button type="button" class="link-quiet" data-copy-query>Copy CLI Command</button>
              </div>
              <pre class="code" id="queryCodeArea"><span class="syn-kw">MATCH</span> path = (caller:Function)-[:CALLS*<span class="syn-num">1..4</span>]-&gt;
      (target:Function {name: <span class="syn-str">"ChargeAsync"</span>})
<span class="syn-kw">RETURN</span> caller.symbol <span class="syn-kw">AS</span> UpstreamCaller,
       <span class="syn-fn">length</span>(path) <span class="syn-kw">AS</span> Hops
<span class="syn-kw">ORDER BY</span> Hops <span class="syn-kw">ASC</span>
<span class="syn-kw">LIMIT</span> <span class="syn-num">10</span></pre>
              <p class="pane-foot" id="queryExplanation">Finds upstream callers within 4 hops that reach PaymentProcessor.ChargeAsync.</p>
            </div>

            <div class="pane">
              <div class="pane-head">
                <span>Execution Result (SQLite Graph Engine)</span>
                <span class="measured ok" id="queryTimingBadge">4 rows in 3.8 ms</span>
              </div>
              <div class="table-wrap" id="queryResultsArea">
                <table class="table">
                  <thead><tr><th>UpstreamCaller</th><th>Hops</th></tr></thead>
                  <tbody>
                    <tr><td>Billing.Application.Commands.CaptureOrderHandler.Handle</td><td>1</td></tr>
                    <tr><td>Orders.Api.Controllers.CheckoutController.SubmitOrder</td><td>2</td></tr>
                    <tr><td>Subscriptions.Worker.Consumers.RenewalConsumer.Consume</td><td>2</td></tr>
                    <tr><td>Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute</td><td>3</td></tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 5. Semantic Invariant Gates + 5 Presets + Ratchet + GitHub Actions YAML -->
    <section class="section" id="gates" aria-labelledby="gates-title">
      <div class="wrap">
        <div class="section-head">
          <h2 id="gates-title" class="h2">Semantic Invariant Gates: architectural checks in CI.</h2>
          <p class="section-lede">
            Linters inspect one file at a time. Semantic Invariant Gates (SIG) check dependencies and dataflow across the full 5-layer graph, with a SHA-256 baseline ratchet so legacy violations stay frozen while new ones fail the build.
          </p>
        </div>

        <div class="chips-bar">
          <div class="chips" role="tablist" aria-label="Semantic Invariant Gate presets">
            <button type="button" role="tab" class="chip active" data-gate-preset="boundaries" aria-selected="true">1. Clean Architecture Boundary</button>
            <button type="button" role="tab" class="chip" data-gate-preset="topology" aria-selected="false" tabindex="-1">2. Shared SQL Table Coupling</button>
            <button type="button" role="tab" class="chip" data-gate-preset="visitor" aria-selected="false" tabindex="-1">3. Async Deadlock (.Result)</button>
            <button type="button" role="tab" class="chip" data-gate-preset="literal" aria-selected="false" tabindex="-1">4. Hardcoded URLs &amp; Literals</button>
            <button type="button" role="tab" class="chip" data-gate-preset="ratchet" aria-selected="false" tabindex="-1">5. Legacy Baseline Ratchet</button>
          </div>
          <a href="/architectures" class="link-quiet">Architecture Gate Packs</a>
        </div>

        <div class="gate-split">
          <div class="pane">
            <div class="pane-head">
              <span id="gateFileLabel" class="measured">.codeexplorer/gates/domain-purity.yaml</span>
              <button type="button" class="link-quiet" data-copy-gate>Copy Gate YAML</button>
            </div>
            <pre class="code" id="gateYamlArea"><span class="syn-kw">id:</span> <span class="syn-str">SIG-CA-B-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">Domain Layer Must Not Reference Infrastructure</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">target:</span> <span class="syn-str">"{inf.name}"</span>
<span class="syn-kw">message:</span> <span class="syn-str">"Domain type '{d.name}' references infrastructure type '{inf.name}'"</span>
<span class="syn-kw">query:</span> |-
  <span class="syn-kw">MATCH</span> (d:Type)-[:DEPENDS_ON|INHERITS|IMPLEMENTS]-&gt;(inf:Type)
  <span class="syn-kw">WHERE</span> d.file_path <span class="syn-kw">CONTAINS</span> <span class="syn-str">"/Domain/"</span>
    <span class="syn-kw">AND</span> (inf.file_path <span class="syn-kw">CONTAINS</span> <span class="syn-str">"/Infrastructure/"</span>
      <span class="syn-kw">OR</span> inf.symbol <span class="syn-kw">CONTAINS</span> <span class="syn-str">"DbContext"</span>)
  <span class="syn-kw">RETURN</span> d, inf</pre>
            <p class="pane-foot" id="gateDescriptionText">Blocks any type inside a Domain module from referencing Infrastructure or EF Core types.</p>
          </div>

          <div class="pane">
            <div class="pane-head">
              <span>Gate Evaluation Output</span>
              <span class="measured bad" id="gateStatusBadge">Exit 1 · 2 violations</span>
            </div>
            <div class="violations-list" id="gateViolationsArea">
              <div class="violation">
                <span class="violation-rule bad">SIG-CA-B-001 · Error</span>
                <span class="violation-target">Orders.Domain.Aggregates.Order -&gt; Orders.Infrastructure.Persistence.OrderDbContext</span>
                <span class="violation-loc">src/Orders.Domain/Aggregates/Order.cs:84</span>
              </div>
              <div class="violation">
                <span class="violation-rule bad">SIG-CA-B-001 · Error</span>
                <span class="violation-target">Billing.Domain.Services.InvoiceCalculator -&gt; Billing.Infrastructure.Http.TaxRateClient</span>
                <span class="violation-loc">src/Billing.Domain/Services/InvoiceCalculator.cs:41</span>
              </div>
            </div>
          </div>
        </div>

        <ol class="ratchet">
          <li>
            <span class="ratchet-step">Author YAML rules</span>
            <span class="ratchet-copy">Place invariant rules in <code>.codeexplorer/gates/*.yaml</code> or scaffold defaults with <code>ce gate init</code>.</span>
          </li>
          <li>
            <span class="ratchet-step">Snapshot brownfield debt</span>
            <span class="ratchet-copy">Run <code>ce gate ratchet --update</code> to record SHA-256 fingerprints of existing legacy violations in <code>.codeexplorer/baselines/*.json</code>.</span>
          </li>
          <li>
            <span class="ratchet-step">Block net-new violations</span>
            <span class="ratchet-copy"><code>ce gate check</code> passes when only baselined debt is present and exits with code 1 when a PR introduces a new violation.</span>
          </li>
          <li>
            <span class="ratchet-step">Auto-tighten as code heals</span>
            <span class="ratchet-copy">Whenever a baselined violation is fixed, re-running the ratchet lowers the ceiling so resolved debt cannot return.</span>
          </li>
        </ol>

        <div class="pane ci-card">
          <div class="pane-head">
            <span>GitHub Actions CI Pipeline</span>
            <code class="measured dim">.github/workflows/architecture-gates.yml</code>
          </div>
          <pre class="code"><span class="syn-kw">name:</span> Architecture Invariant Gates
<span class="syn-kw">on:</span> [pull_request]

<span class="syn-kw">jobs:</span>
  <span class="syn-fn">invariant-gates:</span>
    <span class="syn-kw">runs-on:</span> ubuntu-latest
    <span class="syn-kw">steps:</span>
      - <span class="syn-kw">uses:</span> actions/checkout@v4
      - <span class="syn-kw">name:</span> Install CodeExplorer CLI
        <span class="syn-kw">run:</span> curl -fsSL https://code-explorer.app/install.sh | sh
      - <span class="syn-kw">name:</span> Build Graph &amp; Verify Invariant Gates
        <span class="syn-kw">run:</span> |
          ~/.local/bin/ce scan
          ~/.local/bin/ce gate check --format sarif &gt; results.sarif</pre>
        </div>
      </div>
    </section>

    <!-- 6. Languages & Ontology -->
    <section class="section" id="languages" aria-labelledby="languages-title">
      <div class="wrap">
        <div class="section-head section-head--row">
          <div>
            <h2 id="languages-title" class="h2">Five-layer graph ontology across twelve languages.</h2>
            <p class="section-lede">
              Every node and relationship in <code>.codeexplorer/graph.db</code> belongs to one of five layers, from files on disk up to services, queues, SQL tables, and bounded contexts.
            </p>
          </div>
          <a href="/ontology" class="link-quiet">Explore all 38 entities &amp; 56 relationships</a>
        </div>

        <ul class="langs" aria-label="Supported programming languages">
          <li><span class="lang">C# / .NET</span><span class="fw">ASP.NET Core, EF Core, Dapper, MassTransit</span></li>
          <li><span class="lang">Java &amp; Groovy</span><span class="fw">Spring, Quarkus, Seam/JSF, GWT, Hibernate, Liquibase</span></li>
          <li><span class="lang">Kotlin</span><span class="fw">Spring, Ktor, Exposed, Coroutines</span></li>
          <li><span class="lang">TypeScript / JS</span><span class="fw">NestJS, Next.js, Express, TypeORM, Prisma</span></li>
          <li><span class="lang">Go</span><span class="fw">Gin, Echo, gRPC-Gateway, GORM, sqlx</span></li>
          <li><span class="lang">Python</span><span class="fw">FastAPI, Django, Flask, SQLAlchemy, Celery</span></li>
          <li><span class="lang">Rust</span><span class="fw">Axum, Actix, Tonic gRPC, SQLx, Diesel</span></li>
          <li><span class="lang">PHP</span><span class="fw">Symfony, Laravel, Doctrine, Nette</span></li>
          <li><span class="lang">Perl</span><span class="fw">DBI, Mojolicious, Dancer2, LWP</span></li>
          <li><span class="lang">ColdFusion</span><span class="fw">CFML components, cfquery SQL lineage</span></li>
          <li><span class="lang">SQL</span><span class="fw">PostgreSQL, T-SQL, MySQL, ClickHouse, BigQuery</span></li>
          <li><span class="lang">Terraform</span><span class="fw">HCL modules, Terragrunt, AWS/GCP/Azure</span></li>
        </ul>

        <div class="pane ci-card">
          <div class="pane-head">
            <span>Pluggable YAML Framework Detectors &amp; Coverage Diagnostics</span>
            <code class="measured dim">.codeexplorer/detectors/custom-bus.yaml</code>
          </div>
          <pre class="code"><span class="syn-kw">schema_version:</span> <span class="syn-num">1</span>
<span class="syn-kw">id:</span> <span class="syn-str">internal-rpc-bus</span>
<span class="syn-kw">name:</span> <span class="syn-str">Internal RPC &amp; Event Bus</span>
<span class="syn-kw">languages:</span> [java, kotlin, typescript]
<span class="syn-kw">packages:</span> [<span class="syn-str">"com.acme.platform:rpc-bus*"</span>]
<span class="syn-kw">messaging_brokers:</span>
  - <span class="syn-kw">protocol:</span> <span class="syn-str">acme-bus</span>
    <span class="syn-kw">listener_annotations:</span> [BusSubscriber]
    <span class="syn-kw">publish_methods:</span> [publishEvent]</pre>
          <p class="pane-foot">Spot uncovered legacy or internal packages with <code>ce doctor --gaps</code>, drop a declarative YAML detector into <code>.codeexplorer/detectors/</code>, and validate with <code>ce detector validate</code> without recompiling.</p>
        </div>
      </div>
    </section>

    <!-- 7. Install -->
    <section class="section section--install" id="install" aria-labelledby="install-title">
      <div class="wrap install-grid">
        <div>
          <h2 id="install-title" class="h2">Install in thirty seconds.</h2>
          <p class="section-lede">
            Single native binary. Runs locally on your machine and CI runners with no account, cloud dependencies, or source code uploads.
          </p>
        </div>

        <div class="install">
          <div class="chips" role="tablist" aria-label="Install method">
            <button type="button" role="tab" class="chip active" aria-selected="true" data-cmd="brew install vmikhailov/tap/ce" data-note="Install native binary via Homebrew on macOS (Apple Silicon & Intel) or Linux.">Homebrew</button>
            <button type="button" role="tab" class="chip" aria-selected="false" tabindex="-1" data-cmd="code --install-extension vmikhailov.code-explorer-vscode" data-note="Install from VS Code Marketplace or Open VSX (automatically manages the ce CLI).">VS Code</button>
            <button type="button" role="tab" class="chip" aria-selected="false" tabindex="-1" data-cmd="Invoke-WebRequest -Uri https://code-explorer.app/install.ps1 -OutFile install.ps1; .\\install.ps1" data-note="Download and run the standalone Windows installer in PowerShell.">Windows</button>
            <button type="button" role="tab" class="chip" aria-selected="false" tabindex="-1" data-cmd="curl -fsSL https://code-explorer.app/install.sh | sh" data-note="Install the standalone binary on Linux or macOS directly to ~/.local/bin/ce.">curl</button>
            <button type="button" role="tab" class="chip" aria-selected="false" tabindex="-1" data-cmd="dotnet tool install -g codeexplorer.cli" data-note="Install cross-platform via the NuGet global tool package (linux-*, osx-*, win-*).">.NET tool</button>
          </div>
          <div class="install-cmd">
            <code id="install-cmd">brew install vmikhailov/tap/ce</code>
            <button type="button" class="btn btn--primary btn--sm" data-copy-install>Copy</button>
          </div>
          <p class="install-note" id="install-note">Install native binary via Homebrew on macOS (Apple Silicon &amp; Intel) or Linux.</p>
          <div class="next-steps">
            <p><code>ce scan</code> indexes the repository into <code>.codeexplorer/graph.db</code>.</p>
            <p><code>ce context "&lt;task&gt;"</code> builds a one-shot context pack for an issue.</p>
            <p><code>ce doctor --gaps</code> checks index health, parse errors, and uncovered frameworks.</p>
            <p><code>ce mcp</code> starts the Model Context Protocol server over <code>stdio</code>.</p>
            <p><code>ce gate check</code> evaluates Semantic Invariant Gates in CI.</p>
          </div>
        </div>
      </div>
    </section>`},"/architectures":{routePath:"/architectures",pageKind:"architectures",title:"CodeExplorer (ce) Architecture Encyclopedia & Gate Packs",description:"Interactive catalog of software architectures with downloadable Cypher Semantic Invariant Gate (SIG) packs for Clean Architecture, Hexagonal, Modular Monolith, Vertical Slice, Feature-Sliced Design, and Microservices.",canonical:"https://code-explorer.app/architectures",ogUrl:"https://code-explorer.app/architectures",ogTitle:"CodeExplorer Architecture Encyclopedia & Gate Packs",ogDescription:"Pick an architectural style, inspect its dependency rules, and download executable Cypher gate packs.",mainHtml:`<section id="workbench" class="hero-stage">
      <div class="wrap">
        <div class="stage-intro">
          <div>
            <h1 class="hero-title">Architecture rules you can execute.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Select an architecture, inspect its layer rules, and install its gates.
            </p>
            <div class="hero-controls">
              <a href="#invariants" class="btn btn--primary">Inspect Gate YAML</a>
              <button type="button" id="hero-download-btn" class="btn">Download Selected Pack</button>
            </div>
          </div>
        </div>

        <div class="arch-switcher" role="tablist" aria-label="Architectural styles">
          <button type="button" role="tab" class="arch-tab" aria-selected="true" data-pack="clean-architecture">
            <span>Clean Architecture</span>
            <span class="arch-tab-count">5 gates</span>
          </button>
          <button type="button" role="tab" class="arch-tab" aria-selected="false" data-pack="hexagonal">
            <span>Hexagonal Architecture</span>
            <span class="arch-tab-count">4 gates</span>
          </button>
          <button type="button" role="tab" class="arch-tab" aria-selected="false" data-pack="modular-monolith">
            <span>Modular Monolith</span>
            <span class="arch-tab-count">5 gates</span>
          </button>
          <button type="button" role="tab" class="arch-tab" aria-selected="false" data-pack="vertical-slice">
            <span>Vertical Slice</span>
            <span class="arch-tab-count">4 gates</span>
          </button>
          <button type="button" role="tab" class="arch-tab" aria-selected="false" data-pack="feature-sliced-design">
            <span>Feature-Sliced Design</span>
            <span class="arch-tab-count">4 gates</span>
          </button>
          <button type="button" role="tab" class="arch-tab" aria-selected="false" data-pack="microservices-isolation">
            <span>Microservices DB Guard</span>
            <span class="arch-tab-count">4 gates</span>
          </button>
        </div>

        <div class="workbench-grid">
          <div class="blueprint-surface" aria-live="polite">
            <div class="blueprint-bar">
              <div>
                <h2 id="bp-title" class="panel-title">Clean Architecture (Onion Dependency Rule)</h2>
                <p id="bp-subtitle" class="panel-copy">All source code dependencies point strictly inward toward Domain entities.</p>
              </div>
              <div class="blueprint-legend">
                <code class="legend-tag legend-tag--allow">Allowed</code>
                <code class="legend-tag legend-tag--block">Blocked by gate</code>
              </div>
            </div>

            <div id="topology-stage" class="topology-stage">
              <div class="topo-layer">
                <span class="topo-layer-name">Presentation &amp; Delivery (API / UI / CLI)</span>
                <span class="topo-layer-paths">src/Api/**, src/Web/**</span>
              </div>
              <div class="topo-layer">
                <span class="topo-layer-name">Infrastructure &amp; Persistence (ORM / HTTP / Queue)</span>
                <span class="topo-layer-paths">src/Infrastructure/**, src/Persistence/**</span>
              </div>
              <div class="topo-layer">
                <span class="topo-layer-name">Application Use Cases (Commands / Queries / Ports)</span>
                <span class="topo-layer-paths">src/Application/**, src/UseCases/**</span>
              </div>
              <div class="topo-layer topo-layer--core">
                <span class="topo-layer-name">Domain Core (Entities / Value Objects / Policies)</span>
                <span class="topo-layer-paths">src/Domain/**, src/Core/** (0 outward deps)</span>
              </div>

              <div class="topo-connectors">
                <div class="edge-rule edge-rule--allow">
                  <span>Presentation to Application</span>
                  <code class="edge-rule-code">Allowed</code>
                </div>
                <div class="edge-rule edge-rule--allow">
                  <span>Infrastructure to Application &amp; Domain</span>
                  <code class="edge-rule-code">Allowed</code>
                </div>
                <a href="#drawer-0" class="edge-rule edge-rule--block" data-open-drawer="drawer-0">
                  <span>Domain to Infrastructure or Web</span>
                  <code class="edge-rule-code">sig-ca-b-001</code>
                </a>
                <a href="#drawer-2" class="edge-rule edge-rule--block" data-open-drawer="drawer-2">
                  <span>Controller direct to DbContext / SQL</span>
                  <code class="edge-rule-code">sig-ca-b-003</code>
                </a>
              </div>
            </div>

            <div id="bp-note" class="tradeoff-note">
              <strong>Why AST + SQL graph beats import linters:</strong> import linters only check file imports. CodeExplorer catches transitive call chains, DI interface resolutions, and raw SQL / ORM table accesses crossing layer boundaries.
            </div>
          </div>

          <div class="pack-console">
            <div>
              <div class="panel-head">
                <h3 class="panel-title">Install &amp; adapt gate pack</h3>
                <code id="pack-slug-badge" class="badge badge--accent">pack: clean-architecture</code>
              </div>
              <p class="panel-copy">
                Choose your language stack to tailor path conventions in the generated YAML rules:
              </p>
            </div>

            <div class="stack-strip" role="group" aria-label="Language stack preset">
              <button type="button" class="stack-btn" aria-pressed="true" data-stack="agnostic">agnostic</button>
              <button type="button" class="stack-btn" aria-pressed="false" data-stack="dotnet">dotnet</button>
              <button type="button" class="stack-btn" aria-pressed="false" data-stack="typescript">typescript</button>
              <button type="button" class="stack-btn" aria-pressed="false" data-stack="go">go</button>
              <button type="button" class="stack-btn" aria-pressed="false" data-stack="python">python</button>
              <button type="button" class="stack-btn" aria-pressed="false" data-stack="java">java</button>
            </div>

            <ul id="pack-kv" class="kv-list">
              <li class="kv-row">
                <span class="kv-label">Best fit</span>
                <span class="kv-val">Complex domain rules, long-lived backends</span>
              </li>
              <li class="kv-row">
                <span class="kv-label">Avoid when</span>
                <span class="kv-val">Thin CRUD microservices or prototypes</span>
              </li>
              <li class="kv-row">
                <span class="kv-label">Graph layers used</span>
                <span class="kv-val">L2 Project + L3 Call/Type + L4 SQL/ORM</span>
              </li>
              <li class="kv-row">
                <span class="kv-label">Legacy repo onboarding</span>
                <span class="kv-val">SHA-256 ratchet (0 Day-1 rewrites)</span>
              </li>
            </ul>

            <div class="cli-install-box">
              <code id="cli-cmd" class="cli-install-cmd">ce gate check --scan</code>
              <button type="button" id="copy-cli-btn" class="btn">Copy CLI</button>
            </div>

            <div class="pack-actions">
              <button type="button" id="download-pack-btn" class="btn btn--primary">Download .yaml Pack</button>
              <button type="button" id="copy-prompt-btn" class="btn">Copy AI Repo-Adapt Prompt</button>
            </div>
          </div>
        </div>
      </div>
    </section>

    <section id="invariants" class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 id="invariants-heading" class="band-title">1. Included Semantic Invariant Gates: Clean Architecture.</h2>
          <p class="band-lede">
            Every rule below is a zero-match Cypher query evaluated locally in milliseconds via <code>ce gate check</code> or MCP <code>check_semantic_gates</code> with zero LLM token cost.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Included semantic gates">
          <div class="table-caption-bar">
            <span id="table-pack-caption">Pack: clean-architecture (5 executable invariants)</span>
            <span id="table-stack-caption" class="measured">Stack preset: agnostic</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Gate ID &amp; Name</th>
                <th>FOIM Coordinates</th>
                <th>Forbidden Graph Pattern</th>
                <th>Severity</th>
              </tr>
            </thead>
            <tbody id="gates-tbody">
              <tr>
                <td><code>SIG-CA-B-001</code><br><strong>Domain Core Purity</strong></td>
                <td>Module x Boundaries</td>
                <td><code>(Domain)-[:DEPENDS_ON|CALLS]-&gt;(Application|Infrastructure|Presentation)</code></td>
                <td class="cell-num"><code class="badge badge--bad">Error</code></td>
              </tr>
              <tr>
                <td><code>SIG-CA-B-002</code><br><strong>Application Layer Isolation</strong></td>
                <td>Module x Boundaries</td>
                <td><code>(Application)-[:DEPENDS_ON|CALLS]-&gt;(Infrastructure|Presentation)</code></td>
                <td class="cell-num"><code class="badge badge--bad">Error</code></td>
              </tr>
              <tr>
                <td><code>SIG-CA-B-003</code><br><strong>No Delivery-to-Database Bypass</strong></td>
                <td>Method x Boundaries</td>
                <td><code>(Endpoint|Controller)-[:CALLS|READS_FROM|WRITES_TO]-&gt;(Query|Table)</code></td>
                <td class="cell-num"><code class="badge badge--bad">Error</code></td>
              </tr>
              <tr>
                <td><code>SIG-CA-C-004</code><br><strong>Persistence-Ignorant Domain Entities</strong></td>
                <td>Class x Contract</td>
                <td><code>(Type {layer:'Domain'}) with ORM / HTTP / framework attributes</code></td>
                <td class="cell-num"><code class="badge badge--accent">Warning</code></td>
              </tr>
              <tr>
                <td><code>SIG-CA-B-005</code><br><strong>Acyclic Project &amp; Module Graph</strong></td>
                <td>Module x Boundaries</td>
                <td><code>(p1:Project)-[:DEPENDS_ON]-&gt;(p2:Project)-[:DEPENDS_ON]-&gt;(p1)</code></td>
                <td class="cell-num"><code class="badge badge--bad">Error</code></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div id="yaml-drawers" class="prompt-stack">
          <div class="prompt-stack-head">
            <h3 class="prompt-stack-title">Inspect executable YAML &amp; Cypher definitions</h3>
            <span class="prompt-stack-note">Drop directly into <code>.codeexplorer/gates/</code> or adapt paths with your AI agent</span>
          </div>

          <details id="drawer-0" class="prompt-drawer" open>
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge badge--bad">SIG-CA-B-001</code>
                <span>Domain Core Purity (Cypher YAML)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block">id: SIG-CA-B-001
category: Architectural
platform: agnostic
scope: Module
dimension: Boundaries
severity: Error
name: Domain Core Purity
description: Domain types and functions must never call or depend on Application, Infrastructure, or Presentation layers.
target: "{dst.name}"
message: "Domain symbol '{src.name}' calls outer-layer symbol '{dst.name}'"
query: &gt;-
  MATCH (src:Function)-[:CALLS]-&gt;(dst:Function)
  WHERE (src.file_path CONTAINS '/Domain/' OR src.file_path CONTAINS '/Core/')
    AND (dst.file_path CONTAINS '/Infrastructure/' OR dst.file_path CONTAINS '/Application/' OR dst.file_path CONTAINS '/Api/' OR dst.file_path CONTAINS '/Web/')
  RETURN src, dst</pre>
            </div>
          </details>

          <details id="drawer-1" class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge badge--bad">SIG-CA-B-002</code>
                <span>Application Layer Isolation (Cypher YAML)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block">id: SIG-CA-B-002
category: Architectural
platform: agnostic
scope: Module
dimension: Boundaries
severity: Error
name: Application Layer Isolation
description: Application use cases define ports and orchestrate Domain logic without importing concrete Infrastructure or Delivery adapters.
target: "{dst.name}"
message: "Application use case '{src.name}' depends directly on adapter '{dst.name}'"
query: &gt;-
  MATCH (src:Function)-[:CALLS]-&gt;(dst:Function)
  WHERE (src.file_path CONTAINS '/Application/' OR src.file_path CONTAINS '/UseCases/')
    AND (dst.file_path CONTAINS '/Infrastructure/' OR dst.file_path CONTAINS '/Persistence/' OR dst.file_path CONTAINS '/Api/')
  RETURN src, dst</pre>
            </div>
          </details>

          <details id="drawer-2" class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge badge--bad">SIG-CA-B-003</code>
                <span>No Delivery-to-Database Bypass (Cypher YAML)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block">id: SIG-CA-B-003
category: Architectural
platform: agnostic
scope: Method
dimension: Boundaries
severity: Error
name: No Delivery-to-Database Bypass
description: HTTP controllers, GraphQL resolvers, and CLI handlers must dispatch through Application use cases rather than issuing SQL or ORM queries directly.
target: "{q.name}"
message: "Delivery handler '{fn.name}' executes database query '{q.name}' directly, bypassing Application layer"
query: &gt;-
  MATCH (q:Query)-[:ACCESSED_BY]-&gt;(fn:Function)
  WHERE fn.file_path CONTAINS '/Controllers/' OR fn.file_path CONTAINS '/Api/' OR fn.file_path CONTAINS '/Resolvers/'
  RETURN fn, q</pre>
            </div>
          </details>
        </div>
      </div>
    </section>

    <section id="catalog" class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Complete architecture encyclopedia catalog.</h2>
          <p class="band-lede">
            Compare architectural styles by structural trade-offs and the graph-level invariants that keep AI agents from eroding them over time.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Architecture encyclopedia catalog">
          <div class="table-caption-bar">
            <span>6 Canonical Architecture Packs (26 Semantic Invariant Gates)</span>
            <span class="measured">Agnostic + .NET, TS, Go, Python, Java, Rust, PHP, Perl</span>
          </div>
          <table class="bench-table catalog-table">
            <thead>
              <tr>
                <th>Architecture Style</th>
                <th>Core Structural Principle</th>
                <th>What Graph SIG Catches Beyond Import Linters</th>
                <th>Gates</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Clean Architecture</strong><br><span class="tone-muted">clean-architecture</span></td>
                <td>Concentric onion rings where Domain and Application own interfaces and Infrastructure implements adapters.</td>
                <td>Catches Controllers issuing SQL/ORM queries and Domain methods calling outward concrete implementations.</td>
                <td class="cell-num">5&nbsp;gates</td>
                <td><button type="button" class="btn" data-select-pack="clean-architecture">Load Pack</button></td>
              </tr>
              <tr>
                <td><strong>Hexagonal</strong><br><span class="tone-muted">hexagonal</span></td>
                <td>Strict separation between Application Core, Driving (inbound) adapters, and Driven (outbound) adapters.</td>
                <td>Prevents inbound HTTP/gRPC adapters from calling outbound DB/queue adapters directly without a Port interface.</td>
                <td class="cell-num">4&nbsp;gates</td>
                <td><button type="button" class="btn" data-select-pack="hexagonal">Load Pack</button></td>
              </tr>
              <tr>
                <td><strong>Modular Monolith</strong><br><span class="tone-muted">modular-monolith</span></td>
                <td>Autonomous vertical business modules inside a single deployment unit communicating only via public facades.</td>
                <td>Blocks cross-module internal class calls and cross-module SQL writes/joins into another module&rsquo;s owned tables.</td>
                <td class="cell-num">5&nbsp;gates</td>
                <td><button type="button" class="btn" data-select-pack="modular-monolith">Load Pack</button></td>
              </tr>
              <tr>
                <td><strong>Vertical Slice</strong><br><span class="tone-muted">vertical-slice</span></td>
                <td>Each feature slice encapsulates its own request, handler, validation, and data access with zero sibling coupling.</td>
                <td>Detects hidden lateral calls between sibling feature slices (<code>Features/Orders</code> calling <code>Features/Billing</code>).</td>
                <td class="cell-num">4&nbsp;gates</td>
                <td><button type="button" class="btn" data-select-pack="vertical-slice">Load Pack</button></td>
              </tr>
              <tr>
                <td><strong>Feature-Sliced Design</strong><br><span class="tone-muted">feature-sliced-design</span></td>
                <td>Unidirectional frontend hierarchy: <code>app</code> to <code>pages</code> to <code>widgets</code> to <code>features</code> to <code>entities</code> to <code>shared</code>.</td>
                <td>Enforces strict downward imports, public <code>index.ts</code> slice APIs, and zero cross-feature upward leaks.</td>
                <td class="cell-num">4&nbsp;gates</td>
                <td><button type="button" class="btn" data-select-pack="feature-sliced-design">Load Pack</button></td>
              </tr>
              <tr>
                <td><strong>Microservices DB Isolation</strong><br><span class="tone-muted">microservices-isolation</span></td>
                <td>Database-per-service ownership where cross-service state changes flow strictly over APIs or async topics.</td>
                <td>Traces L4 SQL/ORM <code>Table</code> lineage across services to block shared-database coupling and synchronous fan-out chains.</td>
                <td class="cell-num">4&nbsp;gates</td>
                <td><button type="button" class="btn" data-select-pack="microservices-isolation">Load Pack</button></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section id="onboarding" class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">3. Adopt any pack on a legacy codebase in 30 seconds.</h2>
          <p class="band-lede">
            You do not need a greenfield repository to enforce strict architecture. SHA-256 baseline ratcheting freezes existing technical debt so CI passes immediately while blocking every new violation.
          </p>
        </div>

        <div class="panels panels--3">
          <div class="panel">
            <span class="reading tone-accent">Step 01</span>
            <h3 class="panel-title">Install or download pack</h3>
            <p class="panel-copy">Drop the selected <code>.yaml</code> gate pack into <code>.codeexplorer/gates/</code> or ask your MCP coding agent to map its layer paths onto your actual workspace folders.</p>
            <code class="panel-code">.codeexplorer/gates/*.yaml</code>
          </div>
          <div class="panel">
            <span class="reading tone-cool">Step 02</span>
            <h3 class="panel-title">Freeze legacy baseline</h3>
            <p class="panel-copy">Run ratchet initialization once. Existing violations are hashed by symbol identity rather than line numbers, so unrelated edits never cause false alarms.</p>
            <code class="panel-code">ce gate ratchet --update</code>
          </div>
          <div class="panel">
            <span class="reading tone-ok">Step 03</span>
            <h3 class="panel-title">Enforce on every edit</h3>
            <p class="panel-copy">Pre-commit hooks, CI pipelines, and AI agents via <code>check_semantic_gates</code> verify all rules in milliseconds with zero LLM tokens.</p>
            <code class="panel-code">ce gate check (Exit 0 / Exit 1)</code>
          </div>
        </div>
      </div>
    </section>`},"/ontology":{routePath:"/ontology",pageKind:"ontology",title:"CodeExplorer (ce) 5-Layer Graph Ontology & Schema Reference",description:"Interactive 5-layer ontology map of 38 entity kinds, 331 properties, and 56 typed relationships extracted by CodeExplorer across polyglot codebases.",canonical:"https://code-explorer.app/ontology",ogUrl:"https://code-explorer.app/ontology",ogTitle:"CodeExplorer 5-Layer Graph Ontology Reference",ogDescription:"Explore all 38 entity kinds, 331 properties, and 56 typed relationships across 5 architectural layers, with live Cypher queries and URN schemes.",mainHtml:`<section id="explorer" class="ont-hero-stage">
      <div class="wrap">
        <div class="stage-intro">
          <div>
            <h1 class="hero-title">Every fact CodeExplorer extracts.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Inspect 38 entity kinds, 331 properties, and 56 relationships across 5 layers.
            </p>
            <div class="hero-controls">
              <a href="#swimlanes" class="btn btn--primary">Explore Ontology</a>
              <a href="#relationships" class="btn">Relationship Catalog</a>
            </div>
          </div>
        </div>

        <div id="swimlanes" class="ont-workbench">
          <div id="ont-graph-panel" class="ont-graph-panel" data-slot="ontology-workbench">
            <div class="ont-graph-bar">
              <div class="ont-bar-left">
                <div class="ont-mode-group" role="group" aria-label="Ontology visualization mode">
                  <button type="button" class="ont-mode-btn" data-mode="graph" aria-pressed="true">Graph</button>
                  <button type="button" class="ont-mode-btn" data-mode="layers" aria-pressed="false">5 Layers</button>
                </div>
                <input
                  type="search"
                  id="ont-search"
                  class="ont-search-input"
                  aria-label="Filter ontology entities, properties, or relationships"
                  placeholder="Filter entities, properties, or edges..."
                >
              </div>
              <div class="ont-graph-actions">
                <div id="ont-graph-zoom-group" class="ont-graph-zoom-group">
                  <button type="button" id="graph-show-all" class="ont-graph-btn ont-graph-btn--accent" hidden>Show All</button>
                  <label class="ont-air-control" title="Regulate elastic spacing (air) between nodes">
                    <span class="ont-air-label">Air</span>
                    <input type="range" id="graph-air-slider" min="0.1" max="2.4" step="0.1" value="1.0" aria-label="Node spacing factor">
                    <code id="graph-air-val" class="ont-air-val">1.0x</code>
                  </label>
                  <label class="ont-air-control" title="Scale repulsion and spring distance by node edge degree (ForceAtlas2 hub factor)">
                    <span class="ont-air-label">Hub</span>
                    <input type="range" id="graph-hub-slider" min="0.0" max="5.0" step="0.1" value="1.0" aria-label="Degree repulsion factor">
                    <code id="graph-hub-val" class="ont-air-val">1.0x</code>
                  </label>
                  <button type="button" id="graph-freeze" class="ont-graph-btn" aria-pressed="false" title="Freeze or resume elastic physics">Freeze</button>
                  <button type="button" id="graph-zoom-out" class="ont-graph-btn" aria-label="Zoom out">&minus;</button>
                  <button type="button" id="graph-zoom-in" class="ont-graph-btn" aria-label="Zoom in">+</button>
                  <button type="button" id="graph-zoom-fit" class="ont-graph-btn">Fit</button>
                </div>
                <button type="button" id="graph-fullscreen" class="ont-graph-btn" aria-pressed="false" title="Expand within window (Esc)">Expand</button>
              </div>
            </div>

            <div class="ont-stage-body">
              <div id="ont-filter-overlay" class="ont-filter-overlay" aria-label="Layer, inheritance, and relationship filters">
                <div class="ont-filter-row">
                  <div class="ont-layer-group" role="group" aria-label="Ontology layer filters">
              <button type="button" class="ont-layer-btn" data-layer="5" aria-pressed="true" title="Layer 5: Logical Structure: Business domains, bounded contexts, and strategic cross-context boundaries grouping modules, data stores, and domain types.">
                <code class="ont-layer-btn-tag">L5</code>
                <span>Logical</span>
              </button>
              <button type="button" class="ont-layer-btn" data-layer="4" aria-pressed="true" title="Layer 4: Semantic Structure: Runtime workloads, API &amp; broker contracts, databases, SQL queries, tables, and external or cloud dependencies.">
                <code class="ont-layer-btn-tag">L4</code>
                <span>Semantic</span>
              </button>
              <button type="button" class="ont-layer-btn" data-layer="3" aria-pressed="true" title="Layer 3: Syntax Structure: Language-agnostic AST declarations, call graph, semantic parameters, sliced call arguments, literals, and catch blocks.">
                <code class="ont-layer-btn-tag">L3</code>
                <span>Syntax</span>
              </button>
              <button type="button" class="ont-layer-btn" data-layer="2" aria-pressed="true" title="Layer 2: Project Structure: Compilation units, solution projects, external package dependencies, and executable entry points.">
                <code class="ont-layer-btn-tag">L2</code>
                <span>Boundaries</span>
              </button>
              <button type="button" class="ont-layer-btn" data-layer="1" aria-pressed="true" title="Layer 1: File Structure: Workspace folders, source files, and repository Git metadata.">
                <code class="ont-layer-btn-tag">L1</code>
                <span>Physical</span>
              </button>
                  </div>
                  <div class="ont-cat-group" role="group" aria-label="Relationship category filters">
                  <button type="button" class="ont-cat-btn" data-cat="control_flow" aria-pressed="true" title="Control &amp; Call Flow: Direct calls, endpoint triggers, entrypoints, and cross-service RPC/HTTP invocations.">
                    <span class="ont-dot ont-dot--control_flow"></span>
                    <span>Control</span>
                  </button>
                  <button type="button" class="ont-cat-btn" data-cat="data_flow" aria-pressed="true" title="Data, SQL &amp; Messaging: Parameter-to-argument slicing, SQL table reads/writes, database access, and broker pub/sub.">
                    <span class="ont-dot ont-dot--data_flow"></span>
                    <span>Data &amp; SQL</span>
                  </button>
                  <button type="button" class="ont-cat-btn" data-cat="type_dependency" aria-pressed="true" title="Types, Packages &amp; Cloud: Inheritance, type usage, project/package dependencies, test coverage, and cloud bindings.">
                    <span class="ont-dot ont-dot--type_dependency"></span>
                    <span>Types &amp; Cloud</span>
                  </button>
                  <button type="button" class="ont-cat-btn" data-cat="structural" aria-pressed="true" title="Containment &amp; Location: Physical file declarations, folder hierarchy, and resource ownership.">
                    <span class="ont-dot ont-dot--structural"></span>
                    <span>Containment</span>
                  </button>
                  </div>
                </div>
                <div id="ont-capsule-group" class="ont-capsule-group" role="group" aria-label="Inheritance hierarchy toggles">
                  <button type="button" class="ont-capsule-btn" data-hierarchy="Module" aria-pressed="true" title="Toggle :Module inheritance hierarchy (3 direct subtypes)">
                    <code class="ont-layer-btn-tag">Extends</code>
                    <span>:Module</span>
                  </button>
                  <button type="button" class="ont-capsule-btn" data-hierarchy="Workload" aria-pressed="true" title="Toggle :Workload inheritance hierarchy (4 direct subtypes)">
                    <code class="ont-layer-btn-tag">Extends</code>
                    <span>:Workload</span>
                  </button>
                </div>
              </div>

              <div id="ont-canvas-frame" class="ont-canvas-frame">
                <canvas id="ont-graph-canvas" class="ont-graph-canvas" role="img" aria-label="Interactive ontology entity relationship graph"></canvas>
              </div>

              <div id="ont-swimlanes" class="ont-swimlanes" hidden>
                <div id="ont-focus-strip" class="ont-focus-strip">
                  <div class="ont-focus-bar">
                    <h2 id="ont-focus-label" class="ont-focus-label">Active connections for :Endpoint</h2>
                    <span id="ont-focus-count" class="ont-layer-count">16 active links</span>
                  </div>
                  <div id="ont-focus-links" class="ont-focus-links">
                <button type="button" class="ont-focus-chip ont-focus-chip--control_flow" data-select-kind="Function"><code>out: CALLS :Function</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--control_flow" data-select-kind="Function"><code>out: EXPOSED_BY :Function</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--control_flow" data-select-kind="Type"><code>out: EXPOSED_BY :Type</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--control_flow" data-select-kind="Policy"><code>out: GUARDED_BY :Policy</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--control_flow" data-select-kind="Function"><code>out: TRIGGERS :Function</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="Database"><code>out: ATTRIBUTED_TO :Database</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="ExternalService"><code>out: ATTRIBUTED_TO :ExternalService</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="Query"><code>out: ATTRIBUTED_TO :Query</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="CallLiteral"><code>out: PASSES_LITERAL :CallLiteral</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="Topic"><code>out: SUBSCRIBES_TO :Topic</code></button>
                  </div>
                </div>

              <div class="ont-layer" data-layer-id="5">
                <div class="ont-layer-bar">
                  <h3 class="ont-layer-title">Layer 5: Logical Structure</h3>
                  <code class="ont-layer-pill ont-layer-pill--l5">2 kinds</code>
                </div>
                <p class="ont-layer-sub">Business domains, bounded contexts, and strategic cross-context boundaries grouping modules, data stores, and domain types.</p>
                <div class="ont-node-grid">
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Domain" title="Domains: Represents a high-level business domain (e.g. Core, Supporting, Generic, Shared) containing one or more bounded contexts in Level 5 (Logical Structure)." aria-selected="false">
                    <code class="ont-node-kind">:Domain</code>
                    <span class="ont-node-stats">5p / 3e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="BoundedContext" title="Bounded Contexts: Represents a DDD Bounded Context in Level 5 (Logical Structure) that encapsulates semantic workloads, data stores, topics, and domain types." aria-selected="false">
                    <code class="ont-node-kind">:BoundedContext</code>
                    <span class="ont-node-stats">6p / 8e</span>
                  </button>
                </div>
              </div>

              <div class="ont-layer" data-layer-id="4">
                <div class="ont-layer-bar">
                  <h3 class="ont-layer-title">Layer 4: Semantic Architecture</h3>
                  <code class="ont-layer-pill ont-layer-pill--l4">23 kinds</code>
                </div>
                <p class="ont-layer-sub">Runtime workloads, API &amp; broker contracts, databases, SQL queries, tables, and external or cloud dependencies.</p>
                <div class="ont-node-grid">
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Module" title="Software Modules: Abstract base software module encompassing executable workloads, shared libraries, and automated test suites." aria-selected="false">
                    <code class="ont-node-kind">:Module</code>
                    <span class="ont-node-stats">5p / 11e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Workload" title="Runtime Workloads: Abstract executable runtime workload encompassing backend services, client applications, background workers, and CLI tools." aria-selected="false">
                    <code class="ont-node-kind">:Workload</code>
                    <span class="ont-node-stats">5p / 21e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="App" title="Applications: Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular, Electron)." aria-selected="false">
                    <code class="ont-node-kind">:App</code>
                    <span class="ont-node-stats">5p / 21e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--control_flow" data-kind="Service" title="Services: Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server)." aria-selected="false">
                    <code class="ont-node-kind">:Service</code>
                    <span class="ont-node-stats">5p / 24e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Worker" title="Workers: Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service)." aria-selected="false">
                    <code class="ont-node-kind">:Worker</code>
                    <span class="ont-node-stats">5p / 22e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="CliTool" title="CLI Tools: Represents a command-line tool, developer script, or administrative CLI utility." aria-selected="false">
                    <code class="ont-node-kind">:CliTool</code>
                    <span class="ont-node-stats">5p / 21e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Library" title="Libraries &amp; SDKs: Represents a shared library, utility module, DTO package, or domain contract reused across services." aria-selected="false">
                    <code class="ont-node-kind">:Library</code>
                    <span class="ont-node-stats">5p / 12e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="TestSuite" title="Test Suites: Represents an automated test project, runner suite, or test module (e.g. NUnit/xUnit test project, Jest test suite, pytest suite)." aria-selected="false">
                    <code class="ont-node-kind">:TestSuite</code>
                    <span class="ont-node-stats">5p / 12e</span>
                  </button>
                  <button type="button" class="ont-node-btn" data-kind="Endpoint" title="API &amp; Message Endpoints (REST, gRPC, WS, MSG): Represents an exposed external API or message contract endpoint (REST, gRPC, GraphQL, WebSocket, WCF, or Broker Consumer)." aria-selected="true">
                    <code class="ont-node-kind">:Endpoint</code>
                    <span class="ont-node-stats">22p / 16e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="Topic" title="Message Topics &amp; Queues: Represents a message queue, event exchange, or topic boundary." aria-selected="false">
                    <code class="ont-node-kind">:Topic</code>
                    <span class="ont-node-stats">12p / 11e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="ExternalService" title="External Services &amp; Resources: Represents an external API, managed cloud resource, or remote service dependency outside the workspace (REST, gRPC, GraphQL, S3, Stripe, Auth0)." aria-selected="false">
                    <code class="ont-node-kind">:ExternalService</code>
                    <span class="ont-node-stats">17p / 13e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Message" title="CQRS &amp; Domain Messages: Represents a CQRS or application message contract (Query, Command, or Domain/Integration Event) handled via a mediator, bus, or domain dispatcher." aria-selected="false">
                    <code class="ont-node-kind">:Message</code>
                    <span class="ont-node-stats">6p / 8e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Cloud" title="Cloud Platforms &amp; Vendors: Represents a cloud platform, managed infrastructure provider, or SaaS vendor hosting external services, databases, brokers, or workloads (e.g. AWS, GCP, Azure, Cloudflare, Stripe, Supabase)." aria-selected="false">
                    <code class="ont-node-kind">:Cloud</code>
                    <span class="ont-node-stats">5p / 6e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--control_flow" data-kind="Policy" title="Authorization Policies &amp; Guards: Represents an authentication, role, permission, guard, or explicit public-access security policy protecting endpoints and CQRS messages." aria-selected="false">
                    <code class="ont-node-kind">:Policy</code>
                    <span class="ont-node-stats">7p / 6e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="CloudResource" title="Cloud Resources: Represents a provisioned or referenced cloud infrastructure resource (e.g. RDS instance, SQS queue, S3 bucket, VPC, IAM role, KMS key, Kubernetes workload) hosted in a Cloud platform." aria-selected="false">
                    <code class="ont-node-kind">:CloudResource</code>
                    <span class="ont-node-stats">11p / 13e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Job" title="Scheduled Jobs &amp; Workflows: Represents a scheduled cron task, recurring background worker, queued job, or durable workflow/activity." aria-selected="false">
                    <code class="ont-node-kind">:Job</code>
                    <span class="ont-node-stats">8p / 4e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="Database" title="Databases &amp; Storage: Represents a database server instance or catalog configured by a workload and containing one or more DbSchema namespaces." aria-selected="false">
                    <code class="ont-node-kind">:Database</code>
                    <span class="ont-node-stats">17p / 9e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="DbSchema" title="Database Schemas: Represents a logical or physical schema namespace within a database catalog owning tables, views, and stored procedures (e.g. public, dbo, billing, main)." aria-selected="false">
                    <code class="ont-node-kind">:DbSchema</code>
                    <span class="ont-node-stats">5p / 4e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Table" title="Tables &amp; Views: Represents a physical database table." aria-selected="false">
                    <code class="ont-node-kind">:Table</code>
                    <span class="ont-node-stats">10p / 18e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Procedure" title="Stored Procedures: Represents a stored procedure in a database." aria-selected="false">
                    <code class="ont-node-kind">:Procedure</code>
                    <span class="ont-node-stats">5p / 9e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Cache" title="Caches &amp; Regions: Represents an in-memory, distributed, or HTTP cache region/key with distinct read, write, and invalidation semantics." aria-selected="false">
                    <code class="ont-node-kind">:Cache</code>
                    <span class="ont-node-stats">7p / 6e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Transaction" title="Transaction Boundaries: Represents an explicit, declarative, ambient, or pipeline-behavior database transaction boundary." aria-selected="false">
                    <code class="ont-node-kind">:Transaction</code>
                    <span class="ont-node-stats">8p / 4e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="Query" title="SQL Queries: Represents a SQL query." aria-selected="false">
                    <code class="ont-node-kind">:Query</code>
                    <span class="ont-node-stats">9p / 10e</span>
                  </button>
                </div>
              </div>

              <div class="ont-layer" data-layer-id="3">
                <div class="ont-layer-bar">
                  <h3 class="ont-layer-title">Layer 3: Syntactic UAST &amp; Dataflow</h3>
                  <code class="ont-layer-pill ont-layer-pill--l3">7 kinds</code>
                </div>
                <p class="ont-layer-sub">Language-agnostic AST declarations, call graph, semantic parameters, sliced call arguments, literals, and catch blocks.</p>
                <div class="ont-node-grid">
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--control_flow" data-kind="Type" title="Types (Classes, Interfaces): Represents a type declaration (Class, Interface, Struct, Record, Enum, or Union type)." aria-selected="false">
                    <code class="ont-node-kind">:Type</code>
                    <span class="ont-node-stats">14p / 28e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--control_flow" data-kind="Function" title="Functions &amp; Methods: Represents a parsed method, function, subroutine, or procedure." aria-selected="false">
                    <code class="ont-node-kind">:Function</code>
                    <span class="ont-node-stats">24p / 47e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Member" title="Members &amp; Fields: Represents a declared field, property, parameter, or local variable." aria-selected="false">
                    <code class="ont-node-kind">:Member</code>
                    <span class="ont-node-stats">13p / 6e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Parameter" title="Semantic Parameters: Represents a semantically colored function or method parameter that flows into an architectural sink." aria-selected="false">
                    <code class="ont-node-kind">:Parameter</code>
                    <span class="ont-node-stats">11p / 4e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="CallArgument" title="Semantic Arguments: Represents a semantically colored invocation argument sliced backward to its provenance origin." aria-selected="false">
                    <code class="ont-node-kind">:CallArgument</code>
                    <span class="ont-node-stats">18p / 10e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="CallLiteral" title="Call Literals: Represents a raw literal argument passed directly into a method or constructor invocation." aria-selected="false">
                    <code class="ont-node-kind">:CallLiteral</code>
                    <span class="ont-node-stats">12p / 4e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--structural" data-kind="Scope" title="Control &amp; Resource Scopes: Represents a lexical, control-flow, or resource-management scope block (try, catch, finally, loop, branch, resource/using/defer, lock, unsafe, task_spawn)." aria-selected="false">
                    <code class="ont-node-kind">:Scope</code>
                    <span class="ont-node-stats">17p / 6e</span>
                  </button>
                </div>
              </div>

              <div class="ont-layer" data-layer-id="2">
                <div class="ont-layer-bar">
                  <h3 class="ont-layer-title">Layer 2: Project &amp; Package Boundaries</h3>
                  <code class="ont-layer-pill ont-layer-pill--l2">3 kinds</code>
                </div>
                <p class="ont-layer-sub">Compilation units, solution projects, external package dependencies, and executable entry points.</p>
                <div class="ont-node-grid">
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--structural" data-kind="Project" title="Projects: Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package)." aria-selected="false">
                    <code class="ont-node-kind">:Project</code>
                    <span class="ont-node-stats">21p / 19e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="EntryPoint" title="Entry Points: Represents the binary, process, or bundle bootstrap entry point of an executable project (e.g. Program.Main, func main, __main__, index.tsx root mount)." aria-selected="false">
                    <code class="ont-node-kind">:EntryPoint</code>
                    <span class="ont-node-stats">8p / 7e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Package" title="Packages &amp; Dependencies: Represents an external dependency package or workspace package referenced or produced by projects." aria-selected="false">
                    <code class="ont-node-kind">:Package</code>
                    <span class="ont-node-stats">8p / 5e</span>
                  </button>
                </div>
              </div>

              <div class="ont-layer" data-layer-id="1">
                <div class="ont-layer-bar">
                  <h3 class="ont-layer-title">Layer 1: Physical File System</h3>
                  <code class="ont-layer-pill ont-layer-pill--l1">3 kinds</code>
                </div>
                <p class="ont-layer-sub">Workspace folders, source files, and repository Git metadata.</p>
                <div class="ont-node-grid">
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--structural" data-kind="File" title="Files: Represents a source code file containing parsable content." aria-selected="false">
                    <code class="ont-node-kind">:File</code>
                    <span class="ont-node-stats">9p / 24e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Folder" title="Folders: Represents a directory within the indexed workspace." aria-selected="false">
                    <code class="ont-node-kind">:Folder</code>
                    <span class="ont-node-stats">2p / 5e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="GitSettings" title="Git Settings: Represents the Git repository configuration settings for the workspace." aria-selected="false">
                    <code class="ont-node-kind">:GitSettings</code>
                    <span class="ont-node-stats">9p / 2e</span>
                  </button>
                </div>
              </div>
              </div>

              <aside id="entity-inspector" class="ont-inspector is-compact" aria-live="polite" hidden>
                <div class="ont-insp-head">
                  <div class="ont-insp-top-bar">
                    <div class="ont-insp-title-row">
                      <h3 id="insp-title" class="ont-insp-title">:Endpoint</h3>
                      <code id="insp-layer-pill" class="ont-layer-pill ont-layer-pill--l4">Layer 4</code>
                      <code id="insp-abstract-pill" class="ont-edge-prop" hidden>abstract</code>
                    </div>
                    <div class="ont-insp-actions">
                      <button type="button" id="insp-toggle-mode-btn" class="ont-insp-action-btn" title="Toggle between compact summary and full node details">Full</button>
                      <button type="button" id="insp-close-btn" class="ont-insp-action-btn ont-insp-close-btn" aria-label="Close entity inspector" title="Close inspector">&times;</button>
                    </div>
                  </div>
                  <div class="ont-insp-meta">
                    <span id="insp-plural" class="ont-layer-sub">API &amp; Message Endpoints (REST, gRPC, WS, MSG)</span>
                  </div>
                  <div id="insp-lineage" class="ont-lineage-wrap" hidden></div>
                  <p id="insp-purpose" class="panel-copy ont-insp-purpose">Represents an exposed external API or message contract endpoint (REST, gRPC, GraphQL, WebSocket, WCF, or Broker Consumer).</p>
                  <div class="ont-urn-strip">
                    <code class="ont-urn-key">urn</code>
                    <code id="insp-urn" class="ont-urn-val">{workspaceId}:ep:{httpMethod}:{routeTemplate}</code>
                  </div>
                </div>

                <div id="insp-compact-body" class="ont-insp-compact-body">
                  <div class="ont-compact-group">
                    <span id="insp-compact-props-label" class="ont-block-label">Properties (22 + 4 base)</span>
                    <div id="insp-compact-props" class="ont-compact-pills"><code class="ont-edge-prop">name: string</code><code class="ont-edge-prop">path: string</code><code class="ont-edge-prop">http_method: string</code><code class="ont-edge-prop">route_template: string</code><code class="ont-edge-prop">protocol: string</code><code class="ont-edge-prop">is_anonymous: boolean</code><code class="ont-edge-prop">+16 more</code></div>
                  </div>
                  <div class="ont-compact-group">
                    <span id="insp-compact-rels-label" class="ont-block-label">Connected entities (14 out, 2 in)</span>
                    <div id="insp-compact-rels" class="ont-compact-pills"><button type="button" class="ont-jump-btn" data-select-kind="Function">:Function</button><button type="button" class="ont-jump-btn" data-select-kind="Type">:Type</button><button type="button" class="ont-jump-btn" data-select-kind="Policy">:Policy</button><button type="button" class="ont-jump-btn" data-select-kind="Database">:Database</button><button type="button" class="ont-jump-btn" data-select-kind="ExternalService">:ExternalService</button><button type="button" class="ont-jump-btn" data-select-kind="Query">:Query</button><code class="ont-edge-prop">+7 more</code></div>
                  </div>
                </div>

                <div id="insp-full-body" class="ont-insp-full-body">
                  <div class="ont-block">
                    <div class="ont-block-bar">
                      <span id="insp-props-heading" class="ont-block-label">Collected properties (22 + 4 base)</span>
                    </div>
                    <div id="insp-props-list" class="ont-props-list">
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">name</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">The endpoint name (e.g. GET /api/orders, MSG:SubmitOrder).</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">path</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">The path of the folder or file relative to its parent container.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">http_method</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">The HTTP Verb or contract verb (GET, POST, PUT, DELETE, ALL, MSG, WS, RPC).</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">route_template</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">The declared route template or message contract name.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">protocol</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">The contract protocol (REST, gRPC, GraphQL, WS, WCF, MSG, RPC).</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">is_anonymous</code>
                  <code class="ont-prop-type">boolean</code>
                </div>
                <span class="ont-prop-desc">Whether endpoint allows unauthenticated anonymous access.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">required_roles</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">Comma-separated required roles or permissions.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">policies</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">Authorization policies guarding the endpoint.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">request_type</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">The request payload type (e.g. StartRideRequest, CreateOrderDto).</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">response_type</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">The response payload type (e.g. RideDto, OrderResponse).</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">operation_type</code>
                  <code class="ont-prop-type">enum (OperationType)</code>
                </div>
                <span class="ont-prop-desc">The operation type for GraphQL (Query, Mutation, Subscription) or gRPC (Unary, Streaming).</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">file_path</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">Workspace-relative source file declaring the endpoint.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">start_line</code>
                  <code class="ont-prop-type">integer</code>
                </div>
                <span class="ont-prop-desc">1-indexed starting line number of the endpoint declaration.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">line</code>
                  <code class="ont-prop-type">integer</code>
                </div>
                <span class="ont-prop-desc">1-indexed source line number of the route or contract mapping.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">operation</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">Operation or handler action name associated with this endpoint.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">evidence_kind</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">Extraction provenance kind (endpoint, grpc, graphql, ws, consumer).</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">graphql_field</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">Exposed GraphQL root field name when protocol is GraphQL.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">graphql_sdl</code>
                  <code class="ont-prop-type">boolean</code>
                </div>
                <span class="ont-prop-desc">Indicates whether the endpoint was extracted from a GraphQL SDL schema.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">status_code</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">Declared HTTP response status codes (e.g. 200, 201, 404).</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">summary</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">OpenAPI / Swagger or docstring endpoint summary.</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">canonical_route</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">Normalized canonical route template with typed parameter slots (e.g. /users/{id:uuid}/orders).</span>
              </div>
              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">canonical_shape</code>
                  <code class="ont-prop-type">string</code>
                </div>
                <span class="ont-prop-desc">Structural canonical route shape for O(1) endpoint matching (e.g. /users/{}/orders).</span>
              </div>
                    </div>
                  </div>

                  <div class="ont-block">
                    <div class="ont-block-bar">
                      <span id="insp-rels-heading" class="ont-block-label">Typed relationships (14 out, 2 in)</span>
                    </div>
                    <div id="insp-rels-list" class="ont-rels-list">
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--control_flow">-[:CALLS]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Function">:Function</button>
                </div>
                <span class="ont-prop-desc">Links a calling function to the function it directly invokes.</span>
                <div class="ont-edge-props-row"><code class="ont-edge-prop" title="1-indexed source line of the primary invocation site.">r.line</code><code class="ont-edge-prop" title="Aggregated number of call sites between caller and callee.">r.call_count</code><code class="ont-edge-prop" title="True when invoked inside a for/foreach/while/do loop (N+1 risk signal).">r.in_loop</code><code class="ont-edge-prop" title="Nesting depth of enclosing loops at the call site.">r.loop_depth</code><code class="ont-edge-prop" title="True when guarded by an if/switch/ternary branch.">r.is_conditional</code><code class="ont-edge-prop" title="True when enclosed inside a try/catch block.">r.in_try</code><code class="ont-edge-prop" title="Parameter-to-argument dataflow slot mapping across the call boundary.">r.arg_flow</code></div>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--control_flow">-[:EXPOSED_BY]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Function">:Function</button>
                </div>
                <span class="ont-prop-desc">Links an entrypoint or endpoint to the type or function that exposes it.</span>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--control_flow">-[:EXPOSED_BY]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Type">:Type</button>
                </div>
                <span class="ont-prop-desc">Links an entrypoint or endpoint to the type or function that exposes it.</span>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--control_flow">-[:GUARDED_BY]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Policy">:Policy</button>
                </div>
                <span class="ont-prop-desc">Links an Endpoint or CQRS Message contract to a Layer 4 authorization Policy or Guard.</span>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--control_flow">-[:TRIGGERS]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Function">:Function</button>
                </div>
                <span class="ont-prop-desc">Links an entry point or API endpoint to the handler function it triggers.</span>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--data_flow">-[:ATTRIBUTED_TO]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Database">:Database</button>
                </div>
                <span class="ont-prop-desc">Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain.</span>
                <div class="ont-edge-props-row"><code class="ont-edge-prop" title="Call-chain hop distance from the entrypoint or endpoint to the sink.">r.hops</code><code class="ont-edge-prop" title="Target sink classification (Database, Query, ExternalService, Topic).">r.sink_kind</code></div>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--data_flow">-[:ATTRIBUTED_TO]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="ExternalService">:ExternalService</button>
                </div>
                <span class="ont-prop-desc">Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain.</span>
                <div class="ont-edge-props-row"><code class="ont-edge-prop" title="Call-chain hop distance from the entrypoint or endpoint to the sink.">r.hops</code><code class="ont-edge-prop" title="Target sink classification (Database, Query, ExternalService, Topic).">r.sink_kind</code></div>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--data_flow">-[:ATTRIBUTED_TO]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Query">:Query</button>
                </div>
                <span class="ont-prop-desc">Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain.</span>
                <div class="ont-edge-props-row"><code class="ont-edge-prop" title="Call-chain hop distance from the entrypoint or endpoint to the sink.">r.hops</code><code class="ont-edge-prop" title="Target sink classification (Database, Query, ExternalService, Topic).">r.sink_kind</code></div>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--data_flow">-[:PASSES_LITERAL]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="CallLiteral">:CallLiteral</button>
                </div>
                <span class="ont-prop-desc">Links a calling function or scope to a literal argument passed directly into an invocation.</span>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--data_flow">-[:SUBSCRIBES_TO]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Topic">:Topic</button>
                </div>
                <span class="ont-prop-desc">Links a service or handler function to a message queue or topic it listens to or consumes events from.</span>
                <div class="ont-edge-props-row"><code class="ont-edge-prop" title="Provenance source (http_call, grpc_call, publish, subscribe, config).">r.evidence_kind</code><code class="ont-edge-prop" title="JSON array of underlying call-site or endpoint URNs proving this link.">r.evidence_ids</code><code class="ont-edge-prop" title="Transitive call-chain hop count from workload root.">r.hops</code></div>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--data_flow">-[:TARGETED_BY]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="CallArgument">:CallArgument</button>
                </div>
                <span class="ont-prop-desc">Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it.</span>
                <div class="ont-edge-props-row"><code class="ont-edge-prop" title="1-indexed source line where the value transfer occurs.">r.line</code><code class="ont-edge-prop" title="0-indexed target parameter or argument slot.">r.arg_index</code></div>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--structural">-[:DECLARED_IN]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="File">:File</button>
                </div>
                <span class="ont-prop-desc">Links a syntactic type, function, or member declaration to its physical declaring source file.</span>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--structural">-[:DECLARED_IN]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Project">:Project</button>
                </div>
                <span class="ont-prop-desc">Links a syntactic type, function, or member declaration to its physical declaring source file.</span>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--structural">-[:DECLARES]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="Scope">:Scope</button>
                </div>
                <span class="ont-prop-desc">Indicates that a container entity declares a sub-entity.</span>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <button type="button" class="ont-jump-btn" data-select-kind="ExternalService">:ExternalService</button>
                  <code class="ont-edge-tag ont-edge-tag--control_flow">-[:CALLS_ENDPOINT]-&gt;</code>
                </div>
                <span class="ont-prop-desc">Links an external API call to the target HTTP endpoint it invokes.</span>
              </div>
              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <button type="button" class="ont-jump-btn" data-select-kind="Service">:Service</button>
                  <code class="ont-edge-tag ont-edge-tag--control_flow">-[:EXPOSES]-&gt;</code>
                </div>
                <span class="ont-prop-desc">Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.</span>
              </div>
                    </div>
                  </div>

                  <div class="ont-block">
                    <div class="ont-block-bar">
                      <span class="ont-block-label">Cypher query</span>
                      <button type="button" id="copy-cypher-btn" class="ont-copy-btn">Copy Query</button>
                    </div>
                    <pre id="insp-cypher" class="ont-cypher-box">MATCH (n:Endpoint)-[:CALLS]-&gt;(t:Function)
RETURN n.name, n.http_method, n.route_template, t.name
LIMIT 25</pre>
                  </div>
                </div>
              </aside>
            </div>

            <div id="ont-graph-foot" class="ont-graph-foot">
              <div class="ont-cluster-legend">
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--logical"></span>Domains</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--workload"></span>Workloads</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--contract"></span>Contracts</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--data"></span>Data &amp; SQL</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--cloud"></span>Cloud</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--syntax"></span>Syntax</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--slicing"></span>Slicing</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--project"></span>Projects</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--physical"></span>Files</span>
              </div>
              <span class="ont-layer-sub">Drag circles to rearrange, scroll to zoom, click entities or inheritance toggles to inspect.</span>
            </div>
          </div>
        </div>
      </div>
    </section>

    <section id="relationships" class="ont-rel-section">
      <div class="wrap">
        <div class="section-head">
          <h2>Typed Relationship Catalog</h2>
          <p class="panel-copy">
            Auto-generated at build time from C# ontology annotations. Every edge carries source line provenance or transitive call-chain evidence.
          </p>
        </div>

        <div class="ont-rel-cats">
          <div class="ont-rel-cat-block">
            <div class="ont-rel-cat-head">
              <h3>Control &amp; Call Flow</h3>
              <span class="ont-layer-count">18 relationship types</span>
            </div>
            <p class="panel-copy">Direct calls, endpoint triggers, entrypoints, and cross-service RPC/HTTP invocations.</p>
            <div class="table-surface">
              <table class="bench-table catalog-table">
                <thead>
                  <tr>
                    <th scope="col">Relationship</th>
                    <th scope="col">Semantics</th>
                    <th scope="col">Edge Properties</th>
                    <th scope="col">Connected Entities</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">CALLED_BY</code></td>
                    <td>Links a service, external service, or database to the function that invokes or queries it.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">ExternalService -&gt; Function</code><code class="ont-pair-pill">Service -&gt; Function</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">CALLS</code></td>
                    <td>Links a calling function to the function it directly invokes.</td>
                    <td><code>line</code>, <code>call_count</code>, <code>in_loop</code>, <code>loop_depth</code>, <code>is_conditional</code>, <code>in_try</code>, <code>arg_flow</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; Function</code><code class="ont-pair-pill">Function -&gt; Function</code><code class="ont-pair-pill">Function -&gt; Type</code><code class="ont-pair-pill">Procedure -&gt; Procedure</code><code class="ont-pair-pill">Scope -&gt; Function</code><code class="ont-pair-pill">Type -&gt; Function</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">CALLS_ENDPOINT</code></td>
                    <td>Links an external API call to the target HTTP endpoint it invokes.</td>
                    <td><code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">ExternalService -&gt; Endpoint</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">ENFORCED_BY</code></td>
                    <td>Links a Layer 4 authorization Policy to the guard or middleware function enforcing it.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Policy -&gt; Function</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">EXECUTES_IN_TX</code></td>
                    <td>Links a function to an active Layer 4 database Transaction boundary under which it executes.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; Transaction</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">EXPOSED_BY</code></td>
                    <td>Links an entrypoint or endpoint to the type or function that exposes it.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; Function</code><code class="ont-pair-pill">Endpoint -&gt; Type</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">EXPOSES</code></td>
                    <td>Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Service -&gt; Endpoint</code><code class="ont-pair-pill">Workload -&gt; Job</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">GUARDED_BY</code></td>
                    <td>Links an Endpoint or CQRS Message contract to a Layer 4 authorization Policy or Guard.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; Policy</code><code class="ont-pair-pill">Message -&gt; Policy</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">HANDLES_COMMAND</code></td>
                    <td>Links a command handler function to the Layer 4 CQRS Command message contract it executes.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; Message</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">HANDLES_EVENT</code></td>
                    <td>Links an event or notification handler function to the Layer 4 Domain or Integration Event message contract it processes.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; Message</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">HANDLES_QUERY</code></td>
                    <td>Links a query handler function to the Layer 4 CQRS Query message contract it executes.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; Message</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">HAS_ENTRYPOINT</code></td>
                    <td>Links an executable ProjectNode to its binary or process EntryPointNode.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Project -&gt; EntryPoint</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">IMPLEMENTS</code></td>
                    <td>Links a class/struct declaration to the interface it implements.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; EntryPoint</code><code class="ont-pair-pill">Type -&gt; Type</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">INSTANTIATES</code></td>
                    <td>Links a host component to a runtime UI or subsystem component it instantiates.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; EntryPoint</code><code class="ont-pair-pill">Function -&gt; Function</code><code class="ont-pair-pill">Function -&gt; Type</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">SERVICE_CALL</code></td>
                    <td>Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.</td>
                    <td><code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Project -&gt; Project</code><code class="ont-pair-pill">Workload -&gt; ExternalService</code><code class="ont-pair-pill">Workload -&gt; Service</code><code class="ont-pair-pill">Workload -&gt; Workload</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">TRANSITIVELY_CALLS</code></td>
                    <td>Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph.</td>
                    <td><code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">ExternalService -&gt; Function</code><code class="ont-pair-pill">Query -&gt; Function</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">TRIGGERS</code></td>
                    <td>Links an entry point or API endpoint to the handler function it triggers.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; Function</code><code class="ont-pair-pill">EntryPoint -&gt; File</code><code class="ont-pair-pill">Job -&gt; Function</code><code class="ont-pair-pill">Message -&gt; Function</code><code class="ont-pair-pill">Table -&gt; Procedure</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">WRAPS</code></td>
                    <td>Links a Layer 4 Transaction boundary to the root function or handler it wraps.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Transaction -&gt; Function</code></div></td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div class="ont-rel-cat-block">
            <div class="ont-rel-cat-head">
              <h3>Data, SQL &amp; Messaging</h3>
              <span class="ont-layer-count">17 relationship types</span>
            </div>
            <p class="panel-copy">Parameter-to-argument slicing, SQL table reads/writes, database access, and broker pub/sub.</p>
            <div class="table-surface">
              <table class="bench-table catalog-table">
                <thead>
                  <tr>
                    <th scope="col">Relationship</th>
                    <th scope="col">Semantics</th>
                    <th scope="col">Edge Properties</th>
                    <th scope="col">Connected Entities</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">ACCESSED_BY</code></td>
                    <td>Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it.</td>
                    <td><code>operation</code>, <code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Cache -&gt; Function</code><code class="ont-pair-pill">ExternalService -&gt; Function</code><code class="ont-pair-pill">ExternalService -&gt; Type</code><code class="ont-pair-pill">Member -&gt; Function</code><code class="ont-pair-pill">Procedure -&gt; Function</code><code class="ont-pair-pill">Query -&gt; Function</code> <code class="ont-pair-pill">+4 more</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">ATTRIBUTED_TO</code></td>
                    <td>Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain.</td>
                    <td><code>hops</code>, <code>sink_kind</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; Database</code><code class="ont-pair-pill">Endpoint -&gt; ExternalService</code><code class="ont-pair-pill">Endpoint -&gt; Query</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">FLOWS_TO</code></td>
                    <td>Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops.</td>
                    <td><code>line</code>, <code>arg_index</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">CallArgument -&gt; Parameter</code><code class="ont-pair-pill">Parameter -&gt; CallArgument</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">HAS_PARAMETER</code></td>
                    <td>Links a function or method to one of its semantically colored parameters.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; Parameter</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">INVALIDATES_CACHE</code></td>
                    <td>Links a function to a Layer 4 Cache region/key it evicts or deletes.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; Cache</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">PASSES_ARGUMENT</code></td>
                    <td>Links a calling function or scope to a semantically colored call argument.</td>
                    <td><code>line</code>, <code>arg_index</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; CallArgument</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">PASSES_LITERAL</code></td>
                    <td>Links a calling function or scope to a literal argument passed directly into an invocation.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; CallLiteral</code><code class="ont-pair-pill">Function -&gt; CallLiteral</code><code class="ont-pair-pill">Type -&gt; CallLiteral</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">PUBLISHED_BY</code></td>
                    <td>Links a topic to the function that publishes to it.</td>
                    <td><code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Topic -&gt; Function</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">PUBLISHES_TO</code></td>
                    <td>Links a service or function to a message queue or topic it publishes messages or events to.</td>
                    <td><code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Workload -&gt; Topic</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">READS_CACHE</code></td>
                    <td>Links a function to a Layer 4 Cache region/key it reads from.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; Cache</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">READS_FROM</code></td>
                    <td>Links a query or workload to the table or cloud resource it reads from.</td>
                    <td><code>operation</code>, <code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Procedure -&gt; Table</code><code class="ont-pair-pill">Query -&gt; Table</code><code class="ont-pair-pill">Table -&gt; Table</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">SUBSCRIBED_BY</code></td>
                    <td>Links a topic to the function that subscribes to it.</td>
                    <td><code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Topic -&gt; Function</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">SUBSCRIBES_TO</code></td>
                    <td>Links a service or handler function to a message queue or topic it listens to or consumes events from.</td>
                    <td><code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; Topic</code><code class="ont-pair-pill">Worker -&gt; Topic</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">TARGETED_BY</code></td>
                    <td>Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it.</td>
                    <td><code>line</code>, <code>arg_index</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Database -&gt; CallArgument</code><code class="ont-pair-pill">Endpoint -&gt; CallArgument</code><code class="ont-pair-pill">ExternalService -&gt; CallArgument</code><code class="ont-pair-pill">Procedure -&gt; CallArgument</code><code class="ont-pair-pill">Table -&gt; CallArgument</code><code class="ont-pair-pill">Topic -&gt; CallArgument</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">USES_DB</code></td>
                    <td>Links a project to a database catalog or instance it utilizes.</td>
                    <td><code>operation</code>, <code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Workload -&gt; Database</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">WRITES_CACHE</code></td>
                    <td>Links a function to a Layer 4 Cache region/key it populates or updates.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; Cache</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">WRITES_TO</code></td>
                    <td>Links a query or workload to the table or cloud resource it writes to.</td>
                    <td><code>operation</code>, <code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Procedure -&gt; Table</code><code class="ont-pair-pill">Query -&gt; Table</code></div></td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div class="ont-rel-cat-block">
            <div class="ont-rel-cat-head">
              <h3>Types, Packages &amp; Cloud</h3>
              <span class="ont-layer-count">12 relationship types</span>
            </div>
            <p class="panel-copy">Inheritance, type usage, project/package dependencies, test coverage, and cloud bindings.</p>
            <div class="table-surface">
              <table class="bench-table catalog-table">
                <thead>
                  <tr>
                    <th scope="col">Relationship</th>
                    <th scope="col">Semantics</th>
                    <th scope="col">Edge Properties</th>
                    <th scope="col">Connected Entities</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">DEPENDS_ON</code></td>
                    <td>Represents a dependency relationship between projects, packages, or other entities.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">BoundedContext -&gt; BoundedContext</code><code class="ont-pair-pill">CloudResource -&gt; CloudResource</code><code class="ont-pair-pill">CloudResource -&gt; Member</code><code class="ont-pair-pill">Domain -&gt; Domain</code><code class="ont-pair-pill">Module -&gt; Library</code><code class="ont-pair-pill">Module -&gt; Package</code> <code class="ont-pair-pill">+3 more</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">DEPLOYS</code></td>
                    <td>Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">CloudResource -&gt; Database</code><code class="ont-pair-pill">CloudResource -&gt; ExternalService</code><code class="ont-pair-pill">CloudResource -&gt; Topic</code><code class="ont-pair-pill">CloudResource -&gt; Workload</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">HOSTED_IN</code></td>
                    <td>Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">CloudResource -&gt; Cloud</code><code class="ont-pair-pill">Database -&gt; Cloud</code><code class="ont-pair-pill">ExternalService -&gt; Cloud</code><code class="ont-pair-pill">Topic -&gt; Cloud</code><code class="ont-pair-pill">Workload -&gt; Cloud</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">IMPLEMENTED_BY</code></td>
                    <td>Links a library package to the project that implements or encapsulates it.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Database -&gt; CloudResource</code><code class="ont-pair-pill">ExternalService -&gt; CloudResource</code><code class="ont-pair-pill">Message -&gt; Type</code><code class="ont-pair-pill">Module -&gt; Project</code><code class="ont-pair-pill">Package -&gt; Project</code><code class="ont-pair-pill">Policy -&gt; Type</code> <code class="ont-pair-pill">+3 more</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">INHERITS_FROM</code></td>
                    <td>Links a class/interface to its base class or inherited interface.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Type -&gt; Type</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">OF_TYPE</code></td>
                    <td>Links a member variable or field to its declared type.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Member -&gt; Type</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">POTENTIAL_TYPE</code></td>
                    <td>Links a variable or parameter to concrete classes that implement its declared interface type.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Type -&gt; Type</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">PRODUCES</code></td>
                    <td>Indicates that a library project builds and produces a package.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Package -&gt; Project</code><code class="ont-pair-pill">Project -&gt; Package</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">REFERENCES</code></td>
                    <td>Links a child database table to a parent table via a foreign key constraint.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Table -&gt; Table</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">TESTS</code></td>
                    <td>Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">TestSuite -&gt; Module</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">USES_GIT</code></td>
                    <td>Links the workspace to its repository configuration.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Project -&gt; GitSettings</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">USES_TYPE</code></td>
                    <td>Links a calling function or type reference to the type it instantiates or references.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Function -&gt; Type</code><code class="ont-pair-pill">Type -&gt; Type</code></div></td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div class="ont-rel-cat-block">
            <div class="ont-rel-cat-head">
              <h3>Containment &amp; Location</h3>
              <span class="ont-layer-count">9 relationship types</span>
            </div>
            <p class="panel-copy">Physical file declarations, folder hierarchy, and resource ownership.</p>
            <div class="table-surface">
              <table class="bench-table catalog-table">
                <thead>
                  <tr>
                    <th scope="col">Relationship</th>
                    <th scope="col">Semantics</th>
                    <th scope="col">Edge Properties</th>
                    <th scope="col">Connected Entities</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">BELONGS_TO</code></td>
                    <td>Links an entity to its parent project or container.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">EntryPoint -&gt; Project</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">CONTAINS</code></td>
                    <td>Represents directory structure containment or syntactic scoping of elements.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">BoundedContext -&gt; Database</code><code class="ont-pair-pill">BoundedContext -&gt; Module</code><code class="ont-pair-pill">BoundedContext -&gt; Topic</code><code class="ont-pair-pill">Database -&gt; DbSchema</code><code class="ont-pair-pill">DbSchema -&gt; Procedure</code><code class="ont-pair-pill">DbSchema -&gt; Table</code> <code class="ont-pair-pill">+14 more</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">DECLARED_IN</code></td>
                    <td>Links a syntactic type, function, or member declaration to its physical declaring source file.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Cache -&gt; File</code><code class="ont-pair-pill">CallArgument -&gt; File</code><code class="ont-pair-pill">CallLiteral -&gt; File</code><code class="ont-pair-pill">Cloud -&gt; File</code><code class="ont-pair-pill">CloudResource -&gt; File</code><code class="ont-pair-pill">Database -&gt; File</code> <code class="ont-pair-pill">+17 more</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">DECLARES</code></td>
                    <td>Indicates that a container entity declares a sub-entity.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; Scope</code><code class="ont-pair-pill">Function -&gt; Function</code><code class="ont-pair-pill">Function -&gt; Member</code><code class="ont-pair-pill">Function -&gt; Scope</code><code class="ont-pair-pill">Function -&gt; Type</code><code class="ont-pair-pill">Type -&gt; Type</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">DEFINES</code></td>
                    <td>Indicates that an entity defines a particular property or configuration.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">BoundedContext -&gt; Type</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">HAS_MEMBER</code></td>
                    <td>Links a type declaration to its declared member variables or fields.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Type -&gt; Member</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">HAS_METHOD</code></td>
                    <td>Links a type declaration to its declared methods or functions.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Type -&gt; Function</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">LOCATED_IN</code></td>
                    <td>Links a project to the physical folder or files structure where it is located.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Project -&gt; Folder</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">OWNS</code></td>
                    <td>Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">BoundedContext -&gt; Table</code><code class="ont-pair-pill">Module -&gt; Table</code></div></td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </section>`},"/benchmarks":{routePath:"/benchmarks",pageKind:"benchmarks",title:"CodeExplorer (ce) Benchmarks Catalog",description:"Catalog of 10 reproducible benchmarks comparing CodeExplorer (ce) against codebase-memory-mcp (CBM), Graphify, ArchUnit, and ripgrep across brownfield feature tasks, architectural gates, multi-hop audits, and graph indexing.",canonical:"https://code-explorer.app/benchmarks",ogUrl:"https://code-explorer.app/benchmarks",ogTitle:"CodeExplorer (ce) Benchmarks Catalog",ogDescription:"Catalog of 10 reproducible benchmarks comparing CodeExplorer (ce) against codebase-memory-mcp (CBM), Graphify, ArchUnit, and ripgrep across brownfield feature tasks, architectural gates, multi-hop audits, and graph indexing.",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="hero-top">
          <div>
            <h1 class="hero-title">Benchmarks: real-life coding, AI audits &amp; graph engines.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              We evaluate CodeExplorer across three complementary tracks:<br>
              1. Real-Life Feature Coding &amp; Guardrails (measuring implementation speed, test passage, and whether an agent catches or ships hidden architectural violations).<br>
              2. Autonomous Multi-Hop Audits (measuring time, tokens, and recall on read-only blast-radius and data-lineage discovery).<br>
              3. Graph Engine Head-to-Head (measuring cold scan speed, RAM, query latency, and recall without an LLM).
            </p>
          </div>
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Real-life feature coding &amp; architectural guardrails.</h2>
          <p class="band-lede">
            Everyday engineering goes beyond read-only search: developers ask AI agents to implement new endpoints, service orchestration, and unit tests in brownfield repositories where existing code contains legacy shortcuts. Measures implementation speed, unit test passage, and whether the agent catches and self-heals architectural violations before finishing.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Track 1: Real-life feature coding and architectural guardrails">
          <div class="table-caption-bar">
            <span>Track 1: Real-Life Feature Implementation &amp; Pre-Commit Guardrails (Cases 10, 09, 08, 07 &amp; 06)</span>
            <span class="measured">Full runtime transcripts &amp; git diffs</span>
          </div>
          <table class="bench-table catalog-table">
            <thead>
              <tr>
                <th>Case Study</th>
                <th>Engineering Task &amp; Codebase</th>
                <th>Compared Tools</th>
                <th>Speed, Guardrails &amp; Outcome</th>
                <th>Report</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong><a href="/benchmarks/10-cqrs-read-write-invariants">Case Study 10</a></strong><br><span class="tone-muted">13 CQRS Repos</span></td>
                <td>Trace <code>QueryHandler</code> contracts and <code>HTTP GET</code> routes across DI and mediator buses down to SQL/ORM writes (<code>SIG-CQRS-001..003</code>).</td>
                <td>CodeExplorer<br>vs<br>ArchUnit</td>
                <td class="cell-win" data-bind-html="catalog.cs10_outcome"><strong class="tone-ok">97 QueryHandlers traced</strong> across 6 repos (29/29 clean in modular-monolith)<br><strong class="tone-ok">2 QueryBus + 8 GET write paths</strong> caught past DI interfaces<br><strong>0 tokens ($0.00)</strong> · 3 built-in Cypher gates</td>
                <td><a href="/benchmarks/10-cqrs-read-write-invariants" class="btn btn--primary">Read Case 10</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent">Case Study 09</a></strong><br><span class="tone-muted">twentyhq/twenty (6 Runs)</span></td>
                <td>Zero-spoiler constructive coding benchmark across 3 multi-module tickets (API key revocation lifecycle, app public-domain DNS teardown, subdomain collision guard) with 100% symmetric architectural rules, comparing Bare Agent (<code>rg</code> + <code>read_file</code>) against CodeExplorer One-Shot Context Pack.</td>
                <td>CodeExplorer<br>vs<br>ripgrep</td>
                <td class="cell-win" data-bind-html="catalog.cs09_outcome"><strong class="tone-ok">~2.2× fewer input tokens</strong> (~3.1M vs ~6.9M)<br><strong class="tone-ok">~1.9× fewer file reads &amp; ~1.4× faster</strong> (55 vs 104 reads, 19m 38s vs 28m 02s)<br><strong>15/15 Jest specs &amp; 21/21 SIG gates pass</strong> (0 rg calls needed in Task 2)</td>
                <td><a href="/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent" class="btn btn--primary">Read Case 09</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/08-multi-task-architecture-suite-on-twenty">Case Study 08</a></strong><br><span class="tone-muted">twentyhq/twenty (4 Tasks)</span></td>
                <td>Implement 4 brownfield backend features &amp; unit test suites across auth, messaging, admin-panel, and domain (24,527 files, 2.49M LOC), comparing CBM against CodeExplorer pre-gate (Navigation Only) and post-gate (Navigation + Gates).</td>
                <td>CodeExplorer<br>vs<br>CBM</td>
                <td class="cell-win" data-bind-html="catalog.cs08_outcome"><strong class="tone-ok">0/4 vs 4/4 violated tasks</strong> (both copied [TOL] debt in 4/4 tasks; ce gate check fixed 4/4 in 1 turn, 0 bypasses)<br><strong class="tone-ok">~1.3× faster suite total</strong> (34m 14s vs 44m 6s across 4 tasks; $0.48 vs $0.48)<br><strong>14/14 unit tests pass for both</strong> (CBM shipped 4 cross-module / transport leaks)</td>
                <td><a href="/benchmarks/08-multi-task-architecture-suite-on-twenty" class="btn btn--primary">Read Case 08</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/07-agent-feature-ce-vs-cbm-on-twenty">Case Study 07</a></strong><br><span class="tone-muted">twentyhq/twenty</span></td>
                <td>Implement a new <code>suspendWorkspace</code> GraphQL mutation, service orchestration (cleanup job, billing cancellation, cache flush), and Jest unit tests across 46 TypeScript packages (24,527 files, 2.49M LOC) under 4 architectural gates with zero rule hints.</td>
                <td>CodeExplorer<br>vs<br>CBM</td>
                <td class="cell-win" data-bind-html="catalog.cs07_outcome"><strong class="tone-ok">~1.6× faster end-to-end</strong> (12m 59s vs 20m 25s; 10m 3s vs 15m 17s draft)<br><strong class="tone-ok">4/4 gates passed vs 1 new violation</strong> (pre-configured gate caught copied legacy debt; 1-turn fix)<br><strong>Both passed unit tests &amp; oxlint</strong> (4/4 vs 3/3 specs; CBM kept AdminPanelBillingService &rarr; emailingDomain)</td>
                <td><a href="/benchmarks/07-agent-feature-ce-vs-cbm-on-twenty" class="btn">Read Case 07</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/06-precommit-architectural-guard">Case Study 06</a></strong><br><span class="tone-muted">argoproj/argo-cd</span></td>
                <td>Automated pre-commit architectural guard &amp; 4-phase SHA-256 ratchet walkthrough: enforce 6 invariant rules across 18 subsystems (4,216 files), freeze legacy debt with line-shift immunity, block single-line PR leaks, and monotonically tighten baselines.</td>
                <td>CodeExplorer<br><span class="tone-muted">(Native SIG)</span></td>
                <td class="cell-win" data-bind-html="catalog.cs06_outcome"><strong class="tone-ok">422 ms native build check</strong><br><strong class="tone-ok">0 tokens ($0.00)</strong> · 4-phase SHA-256 ratchet<br><strong>6 of 6 gates evaluated</strong> (2 PASS, 1 WARN, 3 FAIL)</td>
                <td><a href="/benchmarks/06-precommit-architectural-guard" class="btn">Read Report</a></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Autonomous multi-hop architectural audits (time, tokens &amp; recall).</h2>
          <p class="band-lede">
            Identical model (Gemini 3.8 Flash), identical prompt, same pinned repository commit. Measures how fast an autonomous agent finishes a multi-hop architecture audit, how many tokens it burns, and whether it finds every affected service and table.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Track 2: AI agent audit benchmarks">
          <div class="table-caption-bar">
            <span>Track 2: Multi-Hop Architectural Audit Workflows (Cases 01, 02 &amp; 05)</span>
            <span class="measured">Full runtime transcripts</span>
          </div>
          <table class="bench-table catalog-table">
            <thead>
              <tr>
                <th>Case Study</th>
                <th>Engineering Task &amp; Codebase</th>
                <th>Compared Tools</th>
                <th>Speed, Tokens &amp; Completeness</th>
                <th>Report</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong><a href="/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty">Case Study 01</a></strong><br><span class="tone-muted">twentyhq/twenty</span></td>
                <td>Find all DI service callers, class inheritance chains, BullMQ queue producers/consumers, and API-to-database write paths across 46 TypeScript packages (24,527 files, 2.49M LOC).</td>
                <td>CodeExplorer<br>vs<br>CBM</td>
                <td class="cell-win" data-bind-html="catalog.cs01_outcome"><strong>~6.0× faster</strong> (1m 33s vs 9m 15s)<br><strong>~7.0× fewer tokens</strong> (~242K vs ~1.7M)<br><strong>100% vs 3.4%</strong> native graph recall</td>
                <td><a href="/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty" class="btn btn--primary">Read Case 01</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/02-enterprise-microservices-deep-audit">Case Study 02</a></strong><br><span class="tone-muted">324 Microservices</span></td>
                <td>Full blast-radius audit across shared ORM tables, synchronous cross-service HTTP/RPC fan-in, and RabbitMQ/PubSub queues (13,782 files, TS + Go + SQL, zero schema hints).</td>
                <td>CodeExplorer<br>vs<br>ripgrep + Python</td>
                <td class="cell-win" data-bind-html="catalog.cs02_outcome"><strong>~7.0× faster</strong> (4m 16s vs 29m 52s)<br><strong>~25× fewer tokens</strong> (~686K vs ~17.2M)<br><strong>100% vs ~82%</strong> (rg missed 9 services)</td>
                <td><a href="/benchmarks/02-enterprise-microservices-deep-audit" class="btn">Read Case 02</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/05-polyglot-architecture-and-blast-radius">Case Study 05</a></strong><br><span class="tone-muted">OrchardCore</span></td>
                <td>4-part architectural governance, AppSec <code>AuthorizeAsync</code> audit, <code>YesSql.ISession</code> layer violations, <code>PublishedAsync</code> reactive pipeline, and <code>IContentManager</code> blast radius across 334 projects (7,837 files).</td>
                <td>CodeExplorer<br>vs<br>ripgrep</td>
                <td class="cell-win" data-bind-html="catalog.cs05_outcome"><strong class="tone-ok">~2.5× faster</strong> (4m 25s vs 11m 04s, 92 vs 193 turns)<br><strong class="tone-ok">~4.1× fewer tokens</strong> (~3.9M vs ~16.1M, ~2.9× cheaper)<br><strong>100% vs ~93%</strong> (331/331 vs 307/331 items)</td>
                <td><a href="/benchmarks/05-polyglot-architecture-and-blast-radius" class="btn">Read Case 05</a></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band" style="border-bottom: none;">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">3. Graph engine head-to-head (no LLM in the loop).</h2>
          <p class="band-lede">
            Direct indexer and MCP server comparison on twenty (24,527 files) and immich (3,205 files) across 371 source-verified items: cold scan time, memory/disk footprint, query latency, and extraction recall.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Track 3: Graph engine head-to-head benchmarks">
          <div class="table-caption-bar">
            <span>Track 3: Deterministic Indexer &amp; MCP Benchmarks (Cases 03 &amp; 04)</span>
            <span class="measured">371 automated ground-truth items (seed=42)</span>
          </div>
          <table class="bench-table catalog-table">
            <thead>
              <tr>
                <th>Case Study</th>
                <th>What Was Measured</th>
                <th>Compared Engines</th>
                <th>Indexing Speed &amp; Graph Recall</th>
                <th>Report</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong><a href="/benchmarks/03-code-explorer-vs-codebase-memory-mcp">Case Study 03</a></strong><br><span class="tone-muted">twenty + immich</span></td>
                <td>Cold indexing speed, RAM &amp; SQLite DB size, warm JSON-RPC query latency, basic code recall (130 items: routes, HTTP, calls, inheritance), and architecture recall (241 items: queues, ORM tables, SQL lineage).</td>
                <td>CodeExplorer<br>vs<br>CBM</td>
                <td class="cell-num" data-bind-html="catalog.cs03_outcome"><strong class="tone-ok">Basic Code: 78%</strong> vs 38% (102/130 vs 49/130)<br><strong class="tone-ok">DB &amp; Queues: ~98%</strong> vs 0% (235/241)<br><strong class="tone-accent">Scan:</strong> ce faster on immich; CBM faster on twenty</td>
                <td><a href="/benchmarks/03-code-explorer-vs-codebase-memory-mcp" class="btn">Read Case 03</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/04-code-explorer-vs-graphify">Case Study 04</a></strong><br><span class="tone-muted">twenty + immich</span></td>
                <td>Monorepo indexing scalability from 461k LOC (<code>immich</code>) to 2.49M LOC (<code>twenty</code>), cold MCP server startup time, and graph recall across all 371 items.</td>
                <td>CodeExplorer<br>vs<br>Graphify</td>
                <td class="cell-win" data-bind-html="catalog.cs04_outcome"><strong>~6.1× faster scan</strong> on immich (~18s vs ~108s)<br><strong>&gt;18× faster</strong> on twenty (~147s vs DNF &gt;45m)<br><strong>Basic Code: 78%</strong> vs 18% · <strong>DB &amp; Queues: ~98%</strong></td>
                <td><a href="/benchmarks/04-code-explorer-vs-graphify" class="btn">Read Case 04</a></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-xl);" data-bind-html="catalog.participants_html">Tested participants &amp; versions across the benchmark suite:<br>
          1. CodeExplorer (ce): v1.75.0 to v1.97.0 (.NET 10 &amp; .NET 11, SQLite + OpenCypher, 16 MCP tools)<br>
          2. codebase-memory-mcp (CBM): v0.12.1 (win32-x64 Go + SQLite binary, 17 MCP tools)<br>
          3. Graphify (graphifyy): v0.9.104 (Python 3.10, Tree-sitter + NetworkX --code-only, 10 MCP tools)<br>
          4. Lexical CLI Baseline: ripgrep (rg v14.1.1) + Python 3.10 (Case 02) + view_file<br>
          5. LLM Models: Gemini 3.8 Flash (Cases 01, 02, 05, 07 &amp; 08) &amp; Gemini 4 Argon (Medium) (Case 09, symmetric system prompts across paired runs)<br>
          6. Pinned Target Commits: twentyhq/twenty @ b9ef7107 (Case 01), twentyhq/twenty @ 12e8fae4 (Cases 03, 04, 07, 08 &amp; 09), immich-app/immich @ 2153d22f (Cases 03 &amp; 04), OrchardCMS/OrchardCore @ 05ad2d9a (Case 05), argoproj/argo-cd @ 6d689fa3 (Case 06)</div>
      </div>
    </section>`},"/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty":{routePath:"/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty",pageKind:"benchmark-case",title:"Case Study 01: Agent Audit on twentyhq/twenty (CodeExplorer vs codebase-memory-mcp)",description:"",canonical:"https://code-explorer.app/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty",ogUrl:"https://code-explorer.app/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty",ogTitle:"Case Study 01: Agent Audit on twentyhq/twenty (CodeExplorer vs codebase-memory-mcp)",ogDescription:"",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 01: Agent Audit on twentyhq/twenty</span>
          </div>
          <span class="measured" data-bind="cs01.crumb_scale">46 packages, 24,527 files, 2.49M LOC</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 01: Autonomous agent audit on twentyhq/twenty (ce vs CBM).</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Two identical AI agents were given the same 4-part architectural audit on the public twentyhq/twenty monorepo with 100% word-for-word symmetric system prompts: one equipped with CodeExplorer and one equipped with codebase-memory-mcp (CBM). Both graphs were pre-indexed before the run.
            </p>
            <div class="hero-controls">
              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty/results" target="_blank" rel="noopener" class="btn btn--primary">Inspect Outputs &amp; metrics.json</a>
              <a href="#prompts" class="btn">Inspect Tested Prompts</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-ok" data-bind="cs01.card_speedup">~6.0×</span>
            <h2 class="panel-title">Shorter Time to Answer</h2>
            <p class="panel-copy" data-bind-html="cs01.card_speed_copy">CodeExplorer answered in 1m 33s (2m 14s wall); CBM took 9m 15s (10m 34s wall).</p>
            <code class="panel-code" data-bind="cs01.card_turns_footer">7 turns vs 28 turns</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs01.card_tokens">~7.0×</span>
            <h2 class="panel-title">Fewer Input Tokens</h2>
            <p class="panel-copy" data-bind-html="cs01.card_tokens_copy">~242K cumulative input tokens vs ~1.7M tokens across the session.</p>
            <code class="panel-code" data-bind="cs01.card_cost_footer">$0.06 vs $0.23 (~3.9×)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs01.card_recall">100% vs 3.4%</span>
            <h2 class="panel-title">Native Graph Recall</h2>
            <p class="panel-copy" data-bind-html="cs01.card_recall_copy">CodeExplorer resolved 381 / 381 items directly from graph.db. CBM returned 13 / 381 (only self-calls).</p>
            <code class="panel-code" data-bind="cs01.card_recall_footer">0 source files read by ce</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs01.card_scripts">0 vs 3</span>
            <h2 class="panel-title">Fallback AST Scripts</h2>
            <p class="panel-copy" data-bind-html="cs01.card_scripts_copy">After 8 CBM queries returned 0 cross-file edges, the CBM agent wrote 3 custom TS AST scripts &amp; read 10 files.</p>
            <code class="panel-code" data-bind="cs01.card_scripts_footer">0 scripts vs 1,037 LOC of JS</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs01.participants_html">Tested participants:<br>
          1. CodeExplorer (ce): v1.77.2 (.NET 10, SQLite + OpenCypher)<br>
          2. codebase-memory-mcp (CBM): v0.12.1 (Go + SQLite)<br>
          3. LLM Model: Gemini 3.8 Flash (100% symmetric system prompts)<br>
          4. Target Repository: twentyhq/twenty (commit b9ef7107, 46 packages, 24,527 files, 2.49M LOC)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Execution telemetry scorecard.</h2>
          <p class="band-lede">
            All metrics are extracted directly from the runtime logs (transcript_full.jsonl) of conversations <code data-bind="cs01.conv_ce">4a49e7f1</code> (ce) and <code data-bind="cs01.conv_cbm">9a849d22</code> (CBM). Both agents ran under 100% word-for-word symmetric system prompts with zero pre-baked query examples.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>End-to-End Agent Execution Telemetry on twentyhq/twenty (24,527 files, 100% Symmetric Prompts)</span>
            <span class="measured">Simultaneous Paired A/B Run</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Metric</th>
                <th>CBM (cbm + rg + 3 AST Scripts)</th>
                <th>CodeExplorer (ce Graph Only)</th>
                <th>Delta</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Time to Answer (<code>send_message</code>)</td>
                <td class="cell-loss" data-bind="cs01.tbl_time_cbm">555 s (9m 15s)</td>
                <td class="cell-win" data-bind="cs01.tbl_time_ce">93 s (1m 33s)</td>
                <td class="cell-win" data-bind="cs01.tbl_time_delta">6.0× faster (-7m 42s)</td>
              </tr>
              <tr>
                <td>Total Session Wall-Clock</td>
                <td class="cell-loss" data-bind="cs01.tbl_wall_cbm">634 s (10m 34s)</td>
                <td class="cell-win" data-bind="cs01.tbl_wall_ce">134 s (2m 14s)</td>
                <td class="cell-win" data-bind="cs01.tbl_wall_delta">4.7× faster (-8m 20s)</td>
              </tr>
              <tr>
                <td>LLM Reasoning Turns</td>
                <td class="cell-loss" data-bind="cs01.tbl_turns_cbm">28 turns (67 steps)</td>
                <td class="cell-win" data-bind="cs01.tbl_turns_ce">7 turns (23 steps)</td>
                <td class="cell-win" data-bind="cs01.tbl_turns_delta">4.0× fewer turns</td>
              </tr>
              <tr>
                <td>Total Tool Calls</td>
                <td class="cell-loss" data-bind-html="cs01.tbl_calls_cbm">37 (8 <code>cbm</code>, 15 <code>rg</code>/Node, 10 <code>view_file</code>, 3 scripts, 1 <code>send_message</code>)</td>
                <td class="cell-win" data-bind-html="cs01.tbl_calls_ce">15 (1 <code>ce taxonomy</code>, 13 <code>ce query</code>, 0 file reads, 1 <code>send_message</code>)</td>
                <td class="cell-win" data-bind="cs01.tbl_calls_delta">2.5× fewer calls (0 file reads)</td>
              </tr>
              <tr>
                <td>Raw Tool Output Ingested</td>
                <td class="cell-num" data-bind="cs01.tbl_payload_cbm">243 KB (~60.8K tokens)</td>
                <td class="cell-win" data-bind="cs01.tbl_payload_ce">82.1 KB (~20.5K tokens)</td>
                <td class="cell-win" data-bind="cs01.tbl_payload_delta">3.0× smaller payload</td>
              </tr>
              <tr>
                <td>Cumulative Input Tokens</td>
                <td class="cell-loss" data-bind="cs01.tbl_in_cbm">1,678,913 tokens (129K uncached + 1.55M cached)</td>
                <td class="cell-win" data-bind="cs01.tbl_in_ce">241,528 tokens (52.5K uncached + 189.0K cached)</td>
                <td class="cell-win" data-bind="cs01.tbl_in_delta">7.0× fewer input tokens</td>
              </tr>
              <tr>
                <td>Generated Output Tokens</td>
                <td class="cell-num" data-bind="cs01.tbl_out_cbm">29,139 tokens</td>
                <td class="cell-win" data-bind="cs01.tbl_out_ce">7,741 tokens</td>
                <td class="cell-win" data-bind="cs01.tbl_out_delta">3.8× fewer output tokens</td>
              </tr>
              <tr>
                <td>API Cost w/ 90% Cache Discount (Flash / Pro)</td>
                <td class="cell-num" data-bind="cs01.tbl_cost_cbm">$0.23 / $0.86</td>
                <td class="cell-win" data-bind="cs01.tbl_cost_ce">$0.06 / $0.22</td>
                <td class="cell-win" data-bind="cs01.tbl_cost_delta">3.9× cheaper</td>
              </tr>
              <tr>
                <td>Direct Graph Recall (No Fallback)</td>
                <td class="cell-loss" data-bind="cs01.tbl_rec_cbm">3.4% (13 / 381 items)</td>
                <td class="cell-win" data-bind="cs01.tbl_rec_ce">100% (381 / 381 items)</td>
                <td class="cell-win" data-bind="cs01.tbl_rec_delta">29× more items from graph</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Why the CBM agent abandoned codebase-memory-mcp after 8 queries.</h2>
          <p class="band-lede">
            In Turns 1 to 5, cbm_twenty_audit_agent ran get_graph_schema and 7 query_graph calls across all four tasks. Because CBM returned zero cross-file relationships, the agent fell back to writing 3 custom TypeScript Compiler AST scripts:
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Native Graph Recall Across Tasks 1 to 4</span>
            <span class="measured">381 Ground-Truth Items</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Audit Task</th>
                <th>GT Items</th>
                <th>CBM Graph Only</th>
                <th>CBM + 3 AST Scripts</th>
                <th>CodeExplorer Graph Only</th>
                <th>Root Cause of CBM Graph Gap</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Task 1: Cross-Module DI Call Fan-In</strong><br><code>BillingSubscriptionService</code>, <code>ThrottlerService</code>, <code>AccessTokenService</code></td>
                <td class="cell-num">93</td>
                <td class="cell-loss">13 / 93 (14%)<br>(0 / 80 DI calls)</td>
                <td class="cell-win">93 / 93 (100%)</td>
                <td class="cell-win">93 / 93 (100%)</td>
                <td>Tracks intra-class <code>this.method()</code> calls (13) but does not resolve constructor-injected services across files (0/80).</td>
              </tr>
              <tr>
                <td><strong>Task 2: Inheritance &amp; Interfaces</strong><br><code>SyncableEntity</code>, <code>WorkspaceRelatedEntity</code>, <code>WorkspaceEntityMigrationBuilderService</code>, <code>WorkspaceResolverBuilderFactoryInterface</code></td>
                <td class="cell-num">167</td>
                <td class="cell-loss">0 / 167 (0%)</td>
                <td class="cell-win">167 / 167 (100%)</td>
                <td class="cell-win">167 / 167 (100%)</td>
                <td>Does not link <code>INHERITS</code> / <code>IMPLEMENTS</code> across TypeScript module imports.</td>
              </tr>
              <tr>
                <td><strong>Task 3: BullMQ Queue Topology</strong><br>Producers (39) &amp; Consumers (37) across 5 queues</td>
                <td class="cell-num">76</td>
                <td class="cell-loss">0 / 76 (0%)</td>
                <td class="cell-win">76 / 76 (100%)</td>
                <td class="cell-win">76 / 76 (100%)</td>
                <td>Does not model NestJS <code>@InjectMessageQueue</code> or <code>@Processor</code> decorators.</td>
              </tr>
              <tr>
                <td><strong>Task 4: API-to-DB Write Lineage</strong><br>GraphQL <code>@Mutation</code> / REST &rarr; Service (0..3 hops) &rarr; TypeORM <code>Table</code></td>
                <td class="cell-num">45</td>
                <td class="cell-loss">0 / 45 (0%)</td>
                <td class="cell-win">45 / 45 (100%)</td>
                <td class="cell-win">45 / 45 (100%)</td>
                <td>Does not model TypeORM <code>@Entity</code> tables, repository write methods, or GraphQL <code>@Mutation</code> endpoints.</td>
              </tr>
              <tr class="row-total">
                <td>Total Across All 4 Tasks</td>
                <td class="cell-num">381</td>
                <td class="cell-loss">13 / 381 (3.4%)</td>
                <td class="cell-win">381 / 381 (100%)</td>
                <td class="cell-win">381 / 381 (100%)</td>
                <td><code>ce</code> solved the audit in 1m 33s from <code>graph.db</code>; <code>CBM</code> required 3 custom AST scripts + 10 file reads.</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="sub-block">
          <h3 class="sub-title">Verbatim Thinking Traces from CBM Agent (Steps 7 &amp; 16)</h3>
          <pre class="code-block">// Step 7 (14:21:13 UTC):
"\`codebase-memory-mcp\`'s output for Task 1 is limited to intra-class method calls, neglecting inter-class dependency
injection calls. This suggests a potential flaw in how it identifies and retrieves method call information across class boundaries."

// Step 16 (14:22:19 UTC):
"\`codebase-memory-mcp\` failed to extract critical relationships from the TypeScript codebase, specifically inheritance,
implementation details, dependency injection calls, and interactions with queuing systems and database ORMs. A new script,
either in Python or Node.js with the TypeScript compiler, is needed to accurately extract this information."</pre>
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">3. Turn-by-turn context accumulation curve.</h2>
          <p class="band-lede" data-bind-html="cs01.sec3_lede">Every LLM turn re-sends the active conversation history. Because the CodeExplorer agent finished in 7 turns while the CBM agent iterated through 28 turns of AST script writing and file reading, cumulative billed input tokens differed by ~7.0× (~242K vs ~1.7M).</p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Active Context &amp; Cumulative Billed Input Tokens Across All 28 Turns</span>
            <span class="measured">Source: results/metrics.json</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Turn / Stage</th>
                <th>CBM Agent Context<br><span class="tone-muted">Cumulative Input</span></th>
                <th>CodeExplorer Agent Context<br><span class="tone-muted">Cumulative Input</span></th>
                <th>Agent Activity</th>
              </tr>
            </thead>
            <tbody data-bind-html="cs01.turns_tbody">
              <tr>
                <td>Turn 1</td>
                <td class="cell-num">13,697&nbsp;tokens<br><span class="tone-muted">13,697&nbsp;total</span></td>
                <td class="cell-num">18,091&nbsp;tokens<br><span class="tone-muted">18,091&nbsp;total</span></td>
                <td>Both agents inspect their graph schema (<code>get_graph_schema</code> vs <code>ce taxonomy</code>).</td>
              </tr>
              <tr>
                <td>Turn 2</td>
                <td class="cell-num">14,644&nbsp;tokens<br><span class="tone-muted">28,341&nbsp;total</span></td>
                <td class="cell-num">19,921&nbsp;tokens<br><span class="tone-muted">38,012&nbsp;total</span></td>
                <td>Both agents dispatch parallel Cypher queries (3 in <code>CBM</code>, 5 in <code>ce</code>).</td>
              </tr>
              <tr>
                <td>Turn 3</td>
                <td class="cell-num">19,412&nbsp;tokens<br><span class="tone-muted">47,753&nbsp;total</span></td>
                <td class="cell-num">30,559&nbsp;tokens<br><span class="tone-muted">68,571&nbsp;total</span></td>
                <td><code>ce</code> queries Task 1 and Task 3 without file paths and inspects Task 4 tables/endpoints; <code>CBM</code> gets 0 rows on <code>INHERITS|IMPLEMENTS</code>.</td>
              </tr>
              <tr>
                <td>Turn 4</td>
                <td class="cell-num">20,468&nbsp;tokens<br><span class="tone-muted">68,221&nbsp;total</span></td>
                <td class="cell-num">39,397&nbsp;tokens<br><span class="tone-muted">107,968&nbsp;total</span></td>
                <td><code>ce</code> runs 0 to 3 hop <code>Endpoint &rarr; Function &rarr; WRITES_TO &rarr; Table</code> lineage queries for Task 4; <code>CBM</code> retries <code>INHERITS|IMPLEMENTS</code> (0 rows).</td>
              </tr>
              <tr>
                <td>Turn 5</td>
                <td class="cell-num">20,986&nbsp;tokens<br><span class="tone-muted">89,207&nbsp;total</span></td>
                <td class="cell-num">41,397&nbsp;tokens<br><span class="tone-muted">149,365&nbsp;total</span></td>
                <td><code>ce</code> verifies <code>Query</code> write paths and max inheritance depth; <code>CBM</code> runs its 8th empty query and switches to <code>rg</code>.</td>
              </tr>
              <tr>
                <td>Turn 6 (<code>ce</code> Answers @ 1m 33s)</td>
                <td class="cell-num">26,845&nbsp;tokens<br><span class="tone-muted">116,052&nbsp;total</span></td>
                <td class="cell-win">43,424&nbsp;tokens<br>192,789&nbsp;total</td>
                <td><code>ce</code> agent sends its complete 4-part audit report (381 / 381 items, 100% recall) via <code>send_message</code> at 93.0 s.</td>
              </tr>
              <tr>
                <td>Turn 7 (<code>ce</code> Done @ 2m 14s)</td>
                <td class="cell-num">34,652&nbsp;tokens<br><span class="tone-muted">150,704&nbsp;total</span></td>
                <td class="cell-win">48,739&nbsp;tokens<br>241,528&nbsp;total</td>
                <td><code>ce</code> session finishes at 134.0 s; <code>CBM</code> agent locates the local <code>typescript</code> compiler package.</td>
              </tr>
              <tr>
                <td>Turn 8</td>
                <td class="cell-num">35,270&nbsp;tokens<br><span class="tone-muted">185,974&nbsp;total</span></td>
                <td class="cell-num tone-muted">&minus;</td>
                <td><code>CBM</code> agent inspects TypeORM entities and BullMQ queue decorators via <code>rg</code> before writing custom AST scripts.</td>
              </tr>
              <tr>
                <td>Turns 9 to 10</td>
                <td class="cell-num">40,330&nbsp;tokens<br><span class="tone-muted">261,810&nbsp;total</span></td>
                <td class="cell-num tone-muted">&minus;</td>
                <td>Writes and executes <code>audit_part123.cjs</code> (342 LOC of custom TS Compiler AST code) for Tasks 1 to 3.</td>
              </tr>
              <tr>
                <td>Turns 11 to 16</td>
                <td class="cell-num">66,022&nbsp;tokens<br><span class="tone-muted">591,310&nbsp;total</span></td>
                <td class="cell-num tone-muted">&minus;</td>
                <td>Writes and executes <code>audit_part4.cjs</code> (345 LOC), inspects truncated tool outputs, and runs follow-up <code>rg</code> checks.</td>
              </tr>
              <tr>
                <td>Turns 17 to 19</td>
                <td class="cell-num">78,629&nbsp;tokens<br><span class="tone-muted">817,158&nbsp;total</span></td>
                <td class="cell-num tone-muted">&minus;</td>
                <td>Writes and executes <code>verify_part3_and_4.cjs</code> (350 LOC) to refine TypeORM repository and <code>entityManager</code> write detection.</td>
              </tr>
              <tr>
                <td>Turns 20 to 26</td>
                <td class="cell-num">99,628&nbsp;tokens<br><span class="tone-muted">1,469,621&nbsp;total</span></td>
                <td class="cell-num tone-muted">&minus;</td>
                <td>Runs <code>rg</code> and reads 6 source files (GraphQL resolvers, services, and <code>ApiPath.ts</code>) via <code>view_file</code> to verify routes.</td>
              </tr>
              <tr>
                <td>Turn 27 (<code>CBM</code> Answers @ 9m 15s)</td>
                <td class="cell-loss">100,346&nbsp;tokens<br>1,569,967&nbsp;total</td>
                <td class="cell-num tone-muted">&minus;</td>
                <td><code>CBM</code> agent sends its final audit report via <code>send_message</code> at 555.0 s.</td>
              </tr>
              <tr>
                <td>Turn 28 (<code>CBM</code> Done @ 10m 34s)</td>
                <td class="cell-loss">108,946&nbsp;tokens<br>1,678,913&nbsp;total</td>
                <td class="cell-num tone-muted">&minus;</td>
                <td><code>CBM</code> session finishes at 634.0 s total wall-clock.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep" style="border-bottom: none;">
      <div class="wrap">
        <div id="prompts" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">
          <div class="prompt-stack-head">
            <h2 class="prompt-stack-title">4. Reproducibility: tested prompts &amp; 100% symmetric agent instructions.</h2>
            <span class="prompt-stack-note">Click any drawer below to expand verbatim prompt text</span>
          </div>
          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Task Prompt</span>
                <span>prompts/task-prompt.md: Shared 4-Part Audit Task Given Identically to Both Agents</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/01-agent-audit-ce-vs-cbm-on-twenty/prompts/task-prompt.md">Perform a comprehensive, 4-part architectural audit on the \`twenty\` monorepo located at \`./repos/twenty\` (\`24,527\` tracked files across \`46\` packages). Answer all four architectural questions below with 100% completeness (do not guess, truncate, or omit items; exclude \`.spec.ts\` / \`.test.ts\` / \`__tests__\` test files). To keep both query outputs and your final report concise, **do not include file path columns** and **use compact grouped bullet lists** rather than wide multi-line Markdown tables:

1. **Task 1 — Service Method Call Graph &amp; DI Fan-In (\`Layer 3\`)**:
   Identify every non-test caller function/method (\`CallerClass.callerMethod\` and target \`calleeMethod\`) across the monorepo that invokes any method on these 3 core services:
   - \`BillingSubscriptionService\`
   - \`ThrottlerService\`
   - \`AccessTokenService\`
   Group by target service, report the total call pair count (\`totalCallPairs\`) for each service, and list \`callerMethod -&gt; calleeMethod\` pairs grouped compactly by \`CallerClass\`.

2. **Task 2 — Multi-Hop Class Inheritance &amp; Interface Realization (\`Layer 3\`)**:
   List every non-test class/type name (\`className\`) across the monorepo that:
   (a) Inherits **directly or transitively (1 to 3 hops)** from \`SyncableEntity\`
   (b) Inherits **directly or transitively (1 to 3 hops)** from \`WorkspaceRelatedEntity\` (include both direct 1-hop subclasses and 2-hop transitive subclasses)
   (c) Inherits (directly or transitively) from \`WorkspaceEntityMigrationBuilderService\`
   (d) Implements the \`WorkspaceResolverBuilderFactoryInterface\` interface
   For each of (a), (b), (c), and (d), report the exact total count and a compact comma-separated list of \`className\` values (grouped by hop distance where applicable).

3. **Task 3 — Asynchronous BullMQ Queue Producers &amp; Consumers (\`Layer 4\`)**:
   For each of the following 5 BullMQ queues in \`twenty\`:
   - \`webhook-queue\`
   - \`workspace-queue\`
   - \`campaign-queue\`
   - \`ai-queue\`
   - \`delete-cascade-queue\`
   Identify every non-test \`ClassName.methodName\` that:
   (a) **Publishes / enqueues** jobs to the queue, and
   (b) **Subscribes / processes** jobs from the queue.
   Report exact counts and a compact comma-separated list of \`ClassName.methodName\` per queue and role.

4. **Task 4 — End-to-End API-to-Database Write Lineage (\`Layer 4–5\`)**:
   For each of the following 4 database tables:
   - \`billingSubscription\`
   - \`billingCustomer\`
   - \`connectedAccount\`
   - \`messageChannel\`
   Identify every incoming GraphQL (\`@Mutation\` / \`@Query\`) or REST (\`@Controller\`) endpoint and its handler/resolver method (\`endpointName\`, \`httpMethod\`/operation, and \`HandlerClass.handlerMethodName\`) that **writes (\`WRITES_TO\` — inserts, updates, saves, upserts, or deletes)** to that table either directly (\`0\` hops) or within \`1 to 3\` function call hops (\`Endpoint -&gt; Handler/Resolver -&gt; Service -&gt; Repository/Query -&gt; Table\`). Provide the distinct count and compact list of \`endpointName (httpMethod) -&gt; HandlerClass.handlerMethodName\` per table.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--win">System Prompt (ce)</span>
                <span>prompts/ce-agent.md: System Instructions for CodeExplorer Agent (ce_twenty_audit_agent)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/01-agent-audit-ce-vs-cbm-on-twenty/prompts/ce-agent.md"># Agent System Instructions (\`ce_twenty_audit_agent\`)

You are an expert software architect and researcher equipped with the **CodeExplorer CLI (\`ce\`)** plus standard filesystem/search tools (\`rg\`, \`read_file\`, Python/PowerShell scripts).
You are analyzing the \`twenty\` monorepo at \`./repos/twenty\` (\`24,527\` tracked files, \`46\` packages), which is pre-indexed in \`./repos/twenty/.codeexplorer/graph.db\`.

CRITICAL BENCHMARK RULES:
1. Use your assigned graph CLI (\`ce\`) as your primary source of truth. First inspect the graph schema to understand available node labels and relationships. If and only if the graph does not contain the relationships needed to answer a question completely, you may fall back to \`rg\`, \`read_file\`, or scripts.
2. Available graph CLI commands (run via \`run_command\`):
   - Inspect schema &amp; ontology:
     \`ce taxonomy -d "./repos/twenty"\` (or \`ce --help\`)
   - Query graph (\`Cypher\`):
     \`ce query "&lt;CYPHER&gt;" -d "./repos/twenty" --no-truncate\`
   - Search symbols:
     \`ce search "&lt;QUERY&gt;" -d "./repos/twenty"\`
   - Trace callers/callees:
     \`ce callers "&lt;NAME&gt;" -d "./repos/twenty"\`
3. You are STRICTLY FORBIDDEN from running \`codebase-memory-mcp\` or accessing any CBM cache files. Always pass \`-g "!.codeexplorer/**"\` when running \`rg\`.
4. Do NOT modify or create any files inside \`./repos/twenty\`.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--cool">System Prompt (CBM)</span>
                <span>prompts/cbm-agent.md: System Instructions for codebase-memory-mcp Agent (cbm_twenty_audit_agent)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/01-agent-audit-ce-vs-cbm-on-twenty/prompts/cbm-agent.md"># Agent System Instructions (\`cbm_twenty_audit_agent\`)

You are an expert software architect and researcher equipped with the **\`codebase-memory-mcp\` CLI (\`codebase-memory-mcp\`)** plus standard filesystem/search tools (\`rg\`, \`read_file\`, Python/PowerShell scripts).
You are analyzing the \`twenty\` monorepo at \`./repos/twenty\` (\`24,527\` tracked files, \`46\` packages), which is pre-indexed in \`codebase-memory-mcp\` under project name \`twenty\`.

CRITICAL BENCHMARK RULES:
1. Use your assigned graph CLI (\`codebase-memory-mcp\`) as your primary source of truth. First inspect the graph schema to understand available node labels and relationships. If and only if the graph does not contain the relationships needed to answer a question completely, you may fall back to \`rg\`, \`read_file\`, or scripts.
2. Available graph CLI commands (run via \`run_command\`):
   - Inspect schema &amp; ontology:
     \`codebase-memory-mcp cli get_graph_schema --project twenty --format json\` (or \`codebase-memory-mcp --help\`)
   - Query graph (\`Cypher\`):
     \`codebase-memory-mcp cli query_graph --project twenty --query "&lt;CYPHER&gt;" --max-rows 5000 --max-output-tokens 100000 --format json\`
   - Search symbols:
     \`codebase-memory-mcp cli search_graph --project twenty --name-pattern "&lt;REGEX&gt;" --format json\`
   - Trace callers/callees:
     \`codebase-memory-mcp cli trace_path --project twenty --function-name "&lt;NAME&gt;" --direction inbound --format json\`
3. You are STRICTLY FORBIDDEN from running \`ce\`, \`codeexplorer\`, or accessing any \`.codeexplorer\` / \`graph.db\` files. Always pass \`-g "!.codeexplorer/**"\` when running \`rg\`.
4. Do NOT modify or create any files inside \`./repos/twenty\`.</pre>
            </div>
          </details>
        </div>
      </div>
    </section>`},"/benchmarks/02-enterprise-microservices-deep-audit":{routePath:"/benchmarks/02-enterprise-microservices-deep-audit",pageKind:"benchmark-case",title:"Case Study 02: 324-Service Enterprise Multi-Repo Audit (CodeExplorer vs ripgrep)",description:"",canonical:"https://code-explorer.app/benchmarks/02-enterprise-microservices-deep-audit",ogUrl:"https://code-explorer.app/benchmarks/02-enterprise-microservices-deep-audit",ogTitle:"Case Study 02: 324-Service Enterprise Multi-Repo Audit (CodeExplorer vs ripgrep)",ogDescription:"",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 02: 324-Service Enterprise Multi-Repo Audit</span>
          </div>
          <span class="measured">450 modules (324 services, 13,782 files, TS + Go + SQL)</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 02: 324-service enterprise multi-repo audit (ce vs ripgrep).</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Two identical AI agents executed a 4-part cross-service architectural audit on a distributed multi-repo microservices workspace (324 independent microservice repositories, 93 shared library repositories, 33 workers, 13,782 source files) under symmetric system prompts with zero schema hints: one using ripgrep + Python scripts + view_file, and one using CodeExplorer (ce taxonomy + ce query + view_file).
            </p>
            <div class="hero-controls">
              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/02-enterprise-microservices-deep-audit/results" target="_blank" rel="noopener" class="btn btn--primary">Inspect Outputs &amp; metrics.json</a>
              <a href="#prompts" class="btn">Inspect Tested Prompts</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-ok" data-bind="cs02.card_speedup">~7.0×</span>
            <h2 class="panel-title">Shorter Time to Answer</h2>
            <p class="panel-copy" data-bind-html="cs02.card_speed_copy">4m 16s (256s) with CodeExplorer (including ce taxonomy discovery) vs 29m 52s (1,792s) with ripgrep + Python scripts.</p>
            <code class="panel-code" data-bind="cs02.card_turns_footer">18 turns vs 285 turns (~16×)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs02.card_tokens">~25×</span>
            <h2 class="panel-title">Fewer Input Tokens</h2>
            <p class="panel-copy" data-bind-html="cs02.card_tokens_copy">~686K cumulative input tokens (0 compactions) vs ~17.2M post-compaction tokens.</p>
            <code class="panel-code" data-bind="cs02.card_cost_footer">$0.10 vs $1.15 (~11×)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs02.card_reads">16 vs 114</span>
            <h2 class="panel-title">Manual File Reads</h2>
            <p class="panel-copy" data-bind-html="cs02.card_reads_copy">Without schema hints in the prompt, the CodeExplorer agent discovered the 5-layer ontology via ce taxonomy and needed 16 file reads vs 114 file reads and 14 scripts.</p>
            <code class="panel-code" data-bind="cs02.card_reads_footer">27 tool calls vs 284 tool calls</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs02.card_recall">100% vs ~82%</span>
            <h2 class="panel-title">Blast-Radius Recall</h2>
            <p class="panel-copy">Both agents tied at 100% on Tasks 1 &amp; 4. On Task 2, ripgrep missed 9 RTB microservices wired via environment host constants.</p>
            <code class="panel-code" data-bind="cs02.card_recall_footer">83/83 callers vs 9 services missed</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs02.participants_html">Tested participants:<br>
          1. CodeExplorer (ce): v1.77.0 (.NET 10, SQLite + OpenCypher, schema v6: 209,271 nodes, 454,394 edges)<br>
          2. Lexical CLI Baseline: ripgrep (rg v14.1.1) + Python 3.10 + view_file<br>
          3. LLM Model: Gemini 3.8 Flash (symmetric system prompts with zero schema spoilers)<br>
          4. Target Workspace: Enterprise Multi-Repo Microservices System (450 independent service/library repositories, 324 services, 13,782 files; service names anonymized 1-to-1)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Execution telemetry scorecard.</h2>
          <p class="band-lede">
            Extracted from transcript_full.jsonl (conversations <code data-bind="cs02.conv_ce">3e1e9d36</code> for ce and ede04347 for ripgrep) and committed in benchmarks/02-enterprise-microservices-deep-audit/results/metrics.json. Neither agent received any schema hints or pre-baked Cypher templates in its system prompt.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Paired A/B Agent Telemetry Across 324 Microservices (13,782 files, Zero Schema Hints)</span>
            <span class="measured">Symmetric Prompts</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Metric</th>
                <th data-bind-html="cs02.th_rg">ripgrep + Python (142 CLI runs + 114 file reads)</th>
                <th data-bind-html="cs02.th_ce">CodeExplorer (10 CLI runs + 16 file reads)</th>
                <th>Delta</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Time to Answer (<code>send_message</code>)</td>
                <td class="cell-loss" data-bind="cs02.tbl_time_rg">1,792.0 s (29 min 52 sec)</td>
                <td class="cell-win" data-bind="cs02.tbl_time_ce">256.0 s (4 min 16 sec)</td>
                <td class="cell-win" data-bind="cs02.tbl_time_delta">7.00× faster (-25m 36s)</td>
              </tr>
              <tr>
                <td>Total Session Wall-Clock</td>
                <td class="cell-loss" data-bind="cs02.tbl_wall_rg">1,962.0 s (32 min 42 sec)</td>
                <td class="cell-win" data-bind="cs02.tbl_wall_ce">332.0 s (5 min 32 sec)</td>
                <td class="cell-win" data-bind="cs02.tbl_wall_delta">5.91× faster (-27m 10s)</td>
              </tr>
              <tr>
                <td>LLM Reasoning Turns</td>
                <td class="cell-loss" data-bind="cs02.tbl_turns_rg">285 turns (584 steps)</td>
                <td class="cell-win" data-bind="cs02.tbl_turns_ce">18 turns (46 steps)</td>
                <td class="cell-win" data-bind="cs02.tbl_turns_delta">15.83× fewer turns (-93.7%)</td>
              </tr>
              <tr>
                <td>Total Tool Calls</td>
                <td class="cell-loss" data-bind-html="cs02.tbl_calls_rg">284 (142 <code>run_command</code>, 114 <code>view_file</code>, 14 <code>write_to_file</code>, 13 <code>manage_task</code>, 1 <code>send_message</code>)</td>
                <td class="cell-win" data-bind-html="cs02.tbl_calls_ce">27 (10 <code>run_command</code>, 16 <code>view_file</code>, 0 scripts written, 1 <code>send_message</code>)</td>
                <td class="cell-win" data-bind="cs02.tbl_calls_delta">10.52× fewer tool calls (7.13× fewer file reads)</td>
              </tr>
              <tr>
                <td>Raw Tool Output Ingested</td>
                <td class="cell-loss" data-bind="cs02.tbl_payload_rg">694,891 chars (~173,722 tokens)</td>
                <td class="cell-win" data-bind="cs02.tbl_payload_ce">262,638 chars (~65,659 tokens)</td>
                <td class="cell-win" data-bind="cs02.tbl_payload_delta">2.65× smaller tool payload</td>
              </tr>
              <tr>
                <td>Context Window Compactions</td>
                <td class="cell-loss">1 compaction (at Turn 162, 160.3K tokens)</td>
                <td class="cell-win">0 compactions</td>
                <td class="cell-win">0 context resets</td>
              </tr>
              <tr>
                <td>Cumulative Input Tokens (post-compaction)</td>
                <td class="cell-loss" data-bind="cs02.tbl_in_rg">68,776,567 chars (~17,194,141 tokens)</td>
                <td class="cell-win" data-bind="cs02.tbl_in_ce">2,742,953 chars (~685,738 est. / 1,314,308 API tokens)</td>
                <td class="cell-win" data-bind="cs02.tbl_in_delta">25.07× fewer input tokens (13.08× vs API)</td>
              </tr>
              <tr>
                <td>API Cost w/ 90% Cache Discount (Flash / Pro)</td>
                <td class="cell-loss" data-bind="cs02.tbl_cost_rg">$1.15 / $4.47</td>
                <td class="cell-win" data-bind="cs02.tbl_cost_ce">$0.10 / $0.38 ($0.17 / $0.66 API)</td>
                <td class="cell-win" data-bind="cs02.tbl_cost_delta">11.50× cheaper (Flash)</td>
              </tr>
              <tr>
                <td>Task-by-Task Recall</td>
                <td class="cell-num">100% on Tasks 1 &amp; 4; ~81% on Tasks 2 &amp; 3 (missed 9 RTB services &amp; 15+ callers)</td>
                <td class="cell-win">100% on all 4 tasks (all 9 RTB services &amp; callers verified)</td>
                <td class="cell-win">0 missed services</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Breakdown of the 4 audit tasks (where ripgrep tied vs where it failed).</h2>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Task-Level Comparison Across 450 Modules</span>
            <span class="measured" data-bind="cs02.graph_scale">209,271 Nodes, 454,394 Edges in .codeexplorer/graph.db</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Task</th>
                <th>ripgrep + Python</th>
                <th>CodeExplorer</th>
                <th>Explanation</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Task 1: Shared DB Tables (&ge;3 ORM Entities)</strong></td>
                <td class="cell-win">100% (8/8 tables)</td>
                <td class="cell-win">100% (8/8 tables)</td>
                <td><strong>Tie on accuracy:</strong> <code>ripgrep</code> agent wrote Python scripts to scan <code>@Entity</code> / <code>@Table</code> decorators; <code>ce</code> discovered <code>(:Table)-[:IMPLEMENTED_BY]-&gt;(:Type)</code> via <code>ce taxonomy</code> (Turns 1 to 2).</td>
              </tr>
              <tr>
                <td><strong>Task 2: Outbound Service Call Fan-In (5 target services)</strong></td>
                <td class="cell-loss">~81% (missed 9 partner RTB services &amp; 15+ callers)</td>
                <td class="cell-win">100% (all 9 RTB services &amp; wrapper callers found)</td>
                <td><code>ripgrep</code> missed <code>WorkerCoreClientService.updateEndpoint</code> across 9 RTB services because the host URL was injected via an environment constant (<code>WORKER_CORE_API</code>). <code>ce</code> resolved all 9 via <code>(:Service)-[:CALLED_BY]-&gt;(:Function)</code>.</td>
              </tr>
              <tr>
                <td><strong>Task 3: Polyglot Messaging Topology (TS + Go)</strong></td>
                <td class="cell-num">Found core TS &amp; Go handlers; missed shared strategy publishers</td>
                <td class="cell-win">100% (all TS &amp; Go publishers, subscribers, and wrapper callers)</td>
                <td><code>ce</code> queried <code>Topic</code> edges (<code>PUBLISHED_BY</code> / <code>SUBSCRIBED_BY</code>) and verified constant-aliased topics via surgical <code>view_file</code> reads.</td>
              </tr>
              <tr>
                <td><strong>Task 4: Multi-Hop Inheritance &amp; Strategy Realization</strong></td>
                <td class="cell-win">100% (13 + 5 + 8 = 26 classes)</td>
                <td class="cell-win">100% (13 + 5 + 8 = 26 classes)</td>
                <td><strong>Tie on accuracy:</strong> Solved on Turn 3 via <code>(:Type)-[:INHERITS_FROM|IMPLEMENTS*1..5]-&gt;(:Type)</code>.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band" style="border-bottom: none;">
      <div class="wrap">
        <div id="prompts" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">
          <div class="prompt-stack-head">
            <h2 class="prompt-stack-title">3. Reproducibility: symmetric prompts &amp; agent system instructions.</h2>
            <span class="prompt-stack-note">Click any drawer below to expand verbatim prompt text</span>
          </div>
          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Task Prompt</span>
                <span>prompts/task-prompt.md: Shared 4-Part Cross-Service Audit Task Given Identically to Both Agents</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/02-enterprise-microservices-deep-audit/prompts/task-prompt.md">Perform a comprehensive, cross-service architectural audit on the enterprise multi-repo microservices workspace (\`src/\` contains 320+ independent microservice repositories and 90+ shared library repositories across TypeScript/NestJS and Go). Answer all four architectural questions below with 100% completeness (do not guess or omit items; provide exact symbol/class/function names and relative file paths using forward slashes \`/\`):

1. **Cross-Service Database Table Persistence Coupling (Shared ORM Tables)**:
   Identify every database table (\`Table\`) in the workspace that has **3 or more distinct ORM entity classes (\`@Entity\` / model classes)** mapped/persisted to it across different services or libraries.
   For each such table, provide:
   - The exact table name
   - The total count of distinct entity classes mapped to it
   - Every mapped entity class name and its relative file path

2. **Cross-Service Synchronous Outbound Call Fan-In (\`SERVICE_CALL\`)**:
   Identify every caller service, function/method name, and relative file path across the workspace that makes an outbound synchronous service call (\`linkGenerator\` / HTTP client service call) to any of these 5 internal target services:
   - \`svc-worker-core\`
   - \`svc-alert-notifier\`
   - \`svc-network-telemetry\`
   - \`svc-exchange-hub\`
   - \`svc-pricing-engine\`
   Group your results by target service.

3. **Polyglot Asynchronous Messaging &amp; Queue Topology (TypeScript &amp; Go)**:
   For each of the following 5 messaging topics / queue channels across the TypeScript and Go services:
   - \`topic-routing-rule-updated\`
   - \`PUB_SUB_PARTNER_NETWORK_TOPIC\`
   - \`REALTIME_BID_CACHE_QUEUE\`
   - \`TelemetryImpressionTopic\` (\`ImpressionTopic\`)
   - \`TelemetryConversionTopic\` (\`ConversionTopic\`)
   Identify every function/method name and its declared relative file path that:
   (a) **Publishes** messages/events to the topic, and
   (b) **Subscribes / listens** to messages/events from the topic.

4. **Multi-Hop Inheritance &amp; Strategy Realization Across Microservices**:
   List every class/type and its relative file path across the workspace that:
   (a) Inherits (directly or transitively) from \`BaseEventController\`
   (b) Implements the \`ICloudPubSubService\` interface
   (c) Implements the \`IOptimizationCampaignStrategy\` interface

&gt; **Confidentiality Note**: All proprietary service, table, topic, class, and interface identifiers in this benchmark artifact have been systematically anonymized to preserve commercial confidentiality while maintaining a 1-to-1 structural correspondence with the underlying enterprise multi-repo workspace.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--win">System Prompt (ce)</span>
                <span>prompts/graph-agent.md: Symmetric System Instructions for CodeExplorer Agent (Zero Schema Hints)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/02-enterprise-microservices-deep-audit/prompts/graph-agent.md"># Agent System Instructions

You are an expert software architect and researcher analyzing a large enterprise polyglot multi-repo microservices workspace (\`324\` independent service repositories, \`93\` shared library repositories, \`13,782\` source files across TypeScript/NestJS, Go, and SQL).
You must answer the user's architectural questions with 100% accuracy and completeness based on the actual codebase, providing exact symbol names and relative file paths.

CRITICAL BENCHMARK RULES:
1. You have access to the CodeExplorer CLI (\`ce\`) via \`run_command\` (which queries the pre-indexed \`.codeexplorer/graph.db\` semantic graph) and \`view_file\` to inspect source files when needed.
2. To discover available \`ce\` commands or inspect the graph schema (node labels, properties, and relationship types), use \`ce --help\`, \`ce taxonomy\`, or \`ce status\`.
3. Do NOT modify or create any files inside the repository.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--cool">System Prompt (ripgrep)</span>
                <span>prompts/baseline-agent.md: Symmetric System Instructions for ripgrep Baseline Agent</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/02-enterprise-microservices-deep-audit/prompts/baseline-agent.md"># Agent System Instructions

You are an expert software architect and researcher analyzing a large enterprise polyglot multi-repo microservices workspace (\`324\` independent service repositories, \`93\` shared library repositories, \`13,782\` source files across TypeScript/NestJS, Go, and SQL).
You must answer the user's architectural questions with 100% accuracy and completeness based on the actual codebase, providing exact symbol names and relative file paths.

CRITICAL BENCHMARK RULES:
1. You have access to standard CLI search commands (\`rg\` / \`ripgrep\`, \`git grep\`, \`Get-ChildItem\`, \`Select-String\`, or custom shell/Python scripts) via \`run_command\`, and \`view_file\` to inspect source files.
2. You are STRICTLY FORBIDDEN from running \`ce\`, \`ce.exe\`, \`codeexplorer\`, or accessing \`.codeexplorer/graph.db\` (always pass \`-g "!.codeexplorer/**"\` if needed).
3. Do NOT modify or create any files inside the repository.</pre>
            </div>
          </details>
        </div>
      </div>
    </section>`},"/benchmarks/03-code-explorer-vs-codebase-memory-mcp":{routePath:"/benchmarks/03-code-explorer-vs-codebase-memory-mcp",pageKind:"benchmark-case",title:"Case Study 03: CodeExplorer vs codebase-memory-mcp (CBM) on twenty & immich",description:"Deterministic benchmark comparing CodeExplorer (v1.75.0) and codebase-memory-mcp (v0.12.1) across cold indexing, 371 ground-truth items (seed=42), and MCP server latency on twenty (2.49M LOC) and immich (461k LOC).",canonical:"https://code-explorer.app/benchmarks/03-code-explorer-vs-codebase-memory-mcp",ogUrl:"https://code-explorer.app/benchmarks/03-code-explorer-vs-codebase-memory-mcp",ogTitle:"Case Study 03: CodeExplorer vs codebase-memory-mcp (CBM) on twenty & immich",ogDescription:"Deterministic benchmark comparing CodeExplorer (v1.75.0) and codebase-memory-mcp (v0.12.1) across cold indexing, 371 ground-truth items (seed=42), and MCP server latency on twenty (2.49M LOC) and immich (461k LOC).",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 03: CodeExplorer vs codebase-memory-mcp</span>
          </div>
          <span class="measured">twenty (2.49M LOC) &amp; immich (461k LOC)</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 03: CodeExplorer vs codebase-memory-mcp (CBM).</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Head-to-head indexer and MCP server comparison on twenty and immich: how accurately each engine extracts routes, function calls, inheritance, queues, and database tables across 371 source-verified items.
            </p>
            <div class="hero-controls">
              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/03-code-explorer-vs-codebase-memory-mcp/scripts" target="_blank" rel="noopener" class="btn btn--primary">Inspect Evaluation Scripts</a>
              <a href="#prompts" class="btn">Inspect Queries &amp; Spec</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-ok" data-bind="cs03.card_group_a">78% vs 38%</span>
            <h2 class="panel-title">Basic Code &amp; Call Graph</h2>
            <p class="panel-copy">Across 130 basic items supported by both tools (routes, HTTP calls, DI method calls, class inheritance): ce scored 102/130 vs CBM 49/130 (7 wins, 1 tie, 0 losses).</p>
            <code class="panel-code">102 / 130 vs 49 / 130</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs03.card_group_b">~98%</span>
            <h2 class="panel-title">Queues, DB Tables &amp; SQL</h2>
            <p class="panel-copy">Across 241 message queue, ORM table, SQL read/write lineage, and sub-project items: ce matched 235/241. CBM does not index these.</p>
            <code class="panel-code">235 / 241 verified items</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool">~4.3 to 4.7ms</span>
            <h2 class="panel-title">OpenCypher Latency</h2>
            <p class="panel-copy">Single-shot warm OpenCypher round-trip in ce (4.3 to 4.7 ms) vs 38.1 to 71.2 ms in CBM over persistent JSON-RPC stdio.</p>
            <code class="panel-code">~8.9× to 15× faster Cypher query</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs03.card_scan">97s vs 147s</span>
            <h2 class="panel-title">Where CBM Won</h2>
            <p class="panel-copy">CBM indexed twenty ~1.5× faster with ~2.2× lower peak RAM (2.1 GB vs 4.7 GB), while ce was faster on immich with a 5.1× smaller DB.</p>
            <code class="panel-code">CBM faster on 24.5k-file cold scan</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs03.participants_html">Tested participants:<br>
          1. CodeExplorer (ce): v1.75.0 (.NET 10, SQLite + OpenCypher, 16 MCP tools)<br>
          2. codebase-memory-mcp (CBM): v0.12.1 (win32-x64 Go + SQLite binary, 17 MCP tools)<br>
          3. Target Repositories: twentyhq/twenty (commit 12e8fae4, 24,527 files, 2.49M LOC) &amp; immich-app/immich (commit 2153d22f, 3,205 files, 461k LOC)</div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Graph completeness: basic code vs full architecture.</h2>
          <p class="band-lede">
            CBM indexes files, classes, routes, and function calls, whereas CodeExplorer also extracts message queues, ORM database tables, and SQL read/write queries. To keep the comparison fair, we separate the 371 verified items into Basic Code (130 items supported by both tools) and Architecture, DB &amp; Queues (241 items supported only by CodeExplorer).
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Group A: Basic Code &amp; Call Graph (130 Verified Items Supported by Both Tools, seed=42)</span>
            <span class="measured">ce: 7 Wins, 1 Tie, 0 Losses vs CBM</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>What Was Tested</th>
                <th>Repo</th>
                <th>Sample Size</th>
                <th>CodeExplorer</th>
                <th>CBM</th>
                <th>Why the Scores Differ</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Incoming HTTP &amp; GraphQL Endpoints (<code>C1</code>)</td>
                <td><code>twenty</code></td>
                <td class="cell-num">20</td>
                <td class="cell-win">100% (20/20)</td>
                <td class="cell-loss">0% (0/20)</td>
                <td><code>ce</code> indexes NestJS GraphQL <code>@Resolver</code> operations and template-literal controller routes.</td>
              </tr>
              <tr>
                <td>Incoming HTTP &amp; FastAPI Endpoints (<code>C1</code>)</td>
                <td><code>immich</code></td>
                <td class="cell-num">20</td>
                <td class="cell-win">100% (20/20)</td>
                <td class="cell-num">90% (18/20)</td>
                <td><code>ce</code> resolves TypeScript enum route prefixes (<code>@Controller(RouteKey.Asset)</code>).</td>
              </tr>
              <tr>
                <td>Outbound HTTP Client Calls (<code>C2</code>)</td>
                <td><code>twenty</code></td>
                <td class="cell-num">12</td>
                <td class="cell-win">50% (6/12)</td>
                <td class="cell-num">25% (3/12)</td>
                <td><code>ce</code> extracts <code>fetch</code>, <code>axios</code>, and wrapper HTTP client calls.</td>
              </tr>
              <tr>
                <td>Outbound HTTP Client Calls (<code>C2</code>)</td>
                <td><code>immich</code></td>
                <td class="cell-num">6</td>
                <td class="cell-win">83% (5/6)</td>
                <td class="cell-loss">0% (0/6)</td>
                <td><code>ce</code> resolves variable-URL and template-literal HTTP requests.</td>
              </tr>
              <tr>
                <td>Function &amp; DI Service Call Graph (<code>C6</code>)</td>
                <td><code>twenty</code></td>
                <td class="cell-num">12</td>
                <td class="cell-win">100% (12/12)</td>
                <td class="cell-num">75% (9/12)</td>
                <td><code>CBM</code> misses calls on constructor-injected TypeScript service fields across files.</td>
              </tr>
              <tr>
                <td>Function &amp; DI Service Call Graph (<code>C6</code>)</td>
                <td><code>immich</code></td>
                <td class="cell-num">20</td>
                <td class="cell-win">100% (20/20)</td>
                <td class="cell-num">60% (12/20)</td>
                <td><code>ce</code> links NestJS constructor DI properties to target class methods.</td>
              </tr>
              <tr>
                <td>Class Inheritance &amp; <code>implements</code> (<code>C7</code>)</td>
                <td><code>twenty</code></td>
                <td class="cell-num">20</td>
                <td class="cell-win">80% (16/20)</td>
                <td class="cell-loss">20% (4/20)</td>
                <td><code>ce</code> links cross-package base classes and generic TypeScript interfaces.</td>
              </tr>
              <tr>
                <td>Class Inheritance &amp; Mixins (<code>C7</code>)</td>
                <td><code>immich</code></td>
                <td class="cell-num">20</td>
                <td class="cell-num">15% (3/20)</td>
                <td class="cell-num">15% (3/20)</td>
                <td><strong>Tie at 15%</strong>: 17 of 20 DTOs use functional mixin inheritance (<code>extends createZodDto(...)</code>).</td>
              </tr>
              <tr class="row-total">
                <td>Group A Total: Basic Code &amp; Call Graph</td>
                <td>Both</td>
                <td class="cell-num">130</td>
                <td class="cell-win">78% (102/130)</td>
                <td class="cell-num">38% (49/130)</td>
                <td>On <code>immich</code> alone (66 GT): <code>ce</code> 73% (48/66) vs <code>CBM</code> 50% (33/66).</td>
              </tr>
              <tr class="row-total">
                <td>Group B Total: Queues, DB Tables &amp; SQL Lineage</td>
                <td>Both</td>
                <td class="cell-num">241</td>
                <td class="cell-win">97.5% (235/241)</td>
                <td class="cell-loss">Not modeled (0/241)</td>
                <td>BullMQ producers/consumers, TypeORM/Kysely tables &amp; columns, SQL read/write lineage, sub-projects.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Cold indexing speed, disk size, and memory.</h2>
          <p class="band-lede">
            CodeExplorer was faster and produced a 5.1× smaller SQLite database on immich (3,205 files). On twenty (24,527 files), CBM completed cold indexing ~1.5× faster with lower peak RAM because it extracts only basic AST and call edges, whereas ce also runs 250 framework, ORM, and queue analyzers.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Cold Indexing Performance on twenty (24,527 files) &amp; immich (3,205 files)</span>
            <span class="measured">Identical Windows Workstation</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Metric</th>
                <th>Repo</th>
                <th>CodeExplorer</th>
                <th>CBM</th>
                <th>Winner &amp; Notes</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Cold Index Wall Time</td>
                <td><code>immich</code> (3.2k files)</td>
                <td class="cell-win">17.60 s</td>
                <td class="cell-num">20.60 s</td>
                <td><strong>ce (1.17× faster)</strong></td>
              </tr>
              <tr>
                <td>Cold Index Wall Time</td>
                <td><code>twenty</code> (24.5k files)</td>
                <td class="cell-num">147.33 s (Layer 1 to 5 pipeline)</td>
                <td class="cell-win">96.90 s (Layer 1 to 3 pipeline)</td>
                <td><strong>CBM (1.52× faster)</strong></td>
              </tr>
              <tr>
                <td>SQLite Database Size on Disk</td>
                <td><code>immich</code> / <code>twenty</code></td>
                <td class="cell-win">55.1 MB / 784.0 MB</td>
                <td class="cell-loss">282.0 MB / 842.5 MB</td>
                <td><strong>ce (5.1× smaller on immich; 7% smaller on twenty)</strong></td>
              </tr>
              <tr>
                <td>Peak Working Set (RAM)</td>
                <td><code>immich</code> / <code>twenty</code></td>
                <td class="cell-num">780.5 MB / 4,660.6 MB</td>
                <td class="cell-win">595.0 MB / 2,076.0 MB</td>
                <td><strong>CBM (1.3× to 2.2× lower peak RAM)</strong></td>
              </tr>
              <tr>
                <td>Parse Warnings</td>
                <td><code>immich</code> / <code>twenty</code></td>
                <td class="cell-win">0 warnings / 0 warnings</td>
                <td class="cell-loss">4 partial / 209 partial + 1 unusable</td>
                <td><strong>ce (0 parse failures)</strong></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">3. MCP server latency &amp; tool trade-offs.</h2>
          <p class="band-lede">
            Single-shot JSON-RPC round-trip measurements over persistent stdio sessions (results/mcp-benchmark.json).
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Single-Shot Warm JSON-RPC Latency (immich / twenty)</span>
            <span class="measured">ce (36 tools) vs CBM (17 tools)</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>MCP Operation</th>
                <th>CodeExplorer (immich / twenty)</th>
                <th>CBM (immich / twenty)</th>
                <th>Winner &amp; Notes</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Architecture Overview (<code>get_architecture_map</code> / <code>get_architecture</code>)</td>
                <td class="cell-num">267.7 ms (4.4 KB) / 371.7 ms (11.0 KB)</td>
                <td class="cell-win">117.5 ms (3.4 KB) / 286.9 ms (6.1 KB)</td>
                <td><strong>CBM faster</strong>; <code>ce</code> lists all 21 / 46 sub-projects and DBs.</td>
              </tr>
              <tr>
                <td>Graph Schema / Taxonomy (1st / Cached)</td>
                <td class="cell-win">22.3 ms (1.9 ms cached) / 23.2 ms (2.7 ms cached)</td>
                <td class="cell-num">35.6 ms (40.9 ms 2nd) / 72.0 ms (75.3 ms 2nd)</td>
                <td><strong>ce 1.6× to 3.1× faster (21× to 28× cached)</strong> with full property schema (51.5 to 54.7 KB vs 1.0 KB).</td>
              </tr>
              <tr>
                <td>Read-Only Cypher Query (<code>LIMIT 10</code>)</td>
                <td class="cell-win">4.3 ms (1.0 KB) / 4.7 ms (1.1 KB)</td>
                <td class="cell-num">38.1 ms (0.3 KB) / 71.2 ms (0.3 KB)</td>
                <td><strong>ce 8.9× to 15× faster</strong> on single-shot Cypher execution.</td>
              </tr>
              <tr>
                <td>Inbound Callers by Method Name (<code>validate</code>)</td>
                <td class="cell-win">76.5 ms (1.5 KB) / 530.1 ms (2.5 KB)</td>
                <td class="cell-loss">7.5 ms (18.6 KB ambiguous) / 6.5 ms (45.6 KB ambiguous)</td>
                <td><strong>ce resolves callers in 1 turn</strong>; <code>CBM</code> returns 62 / 67 ambiguous candidates requiring a 2nd call.</td>
              </tr>
              <tr>
                <td>Symbol Lookup by Name (<code>AuthService</code>)</td>
                <td class="cell-num">80.8 ms (0.4 KB) / 367.3 ms (1.1 KB)</td>
                <td class="cell-win">55.6 ms (1.1 KB) / 97.5 ms (0.8 KB)</td>
                <td><strong>CBM 1.5× to 3.8× faster</strong> on symbol lookup by name.</td>
              </tr>
              <tr>
                <td>File Outline (<code>auth.service.ts</code> / <code>auth.resolver.ts</code>)</td>
                <td class="cell-num">13.4 ms (0.4 KB) / 92.7 ms (0.9 KB)</td>
                <td class="cell-num">17.3 ms (4.8 KB) / 40.5 ms (7.8 KB)</td>
                <td><strong>ce 1.3× faster on immich; CBM 2.3× faster on twenty</strong> (<code>ce</code> payload 8× to 12× smaller).</td>
              </tr>
              <tr>
                <td>Unique Capabilities</td>
                <td class="cell-num">VS Code extension, 32+ CI gates (<code>ce gate</code>), data lineage, test impact</td>
                <td class="cell-num">Snapshot diffing (<code>compare_graphs</code>), OpenTelemetry overlay (<code>ingest_traces</code>), ADRs (<code>manage_adr</code>)</td>
                <td>Complementary tool surfaces beyond core graph queries.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band" style="border-bottom: none;">
      <div class="wrap">
        <div id="prompts" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">
          <div class="prompt-stack-head">
            <h2 class="prompt-stack-title">4. Reproducibility: tested queries &amp; ground-truth specification.</h2>
            <span class="prompt-stack-note">Click any drawer below to expand verbatim queries</span>
          </div>
          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Group A Queries (C1, C2, C6, C7)</span>
                <span>scripts/evaluate_ce_vs_cbm.py: Exact OpenCypher Queries Executed on Both Tools (130 Basic Code Items)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block"># C1: Incoming HTTP / GraphQL / FastAPI Endpoints
# CodeExplorer (ce):
MATCH (e:Endpoint)-[:EXPOSED_BY]-&gt;(f:Function)
RETURN f.file_path, f.start_line, f.name, e.http_method, e.route_template

# codebase-memory-mcp (CBM):
MATCH (m)-[:HANDLES]-&gt;(r:Route)
RETURN m.file_path, m.start_line, m.name, r.name

# ------------------------------------------------------------------------------
# C2: Outbound Inter-Service &amp; External HTTP Calls
# CodeExplorer (ce):
MATCH (ext:ExternalService)-[r:CALLED_BY]-&gt;(f:Function)
WHERE COALESCE(ext.protocol, 'http') &lt;&gt; 'graphql'
RETURN f.file_path, COALESCE(r.line, f.start_line), f.name, COALESCE(ext.url, ext.name);

MATCH (f:Function)-[r:CALLS_ENDPOINT]-&gt;(ep:Endpoint)
WHERE COALESCE(ep.protocol, 'REST') &lt;&gt; 'GraphQL'
RETURN f.file_path, COALESCE(r.line, f.start_line), f.name, COALESCE(ep.route_template, ep.name);

# codebase-memory-mcp (CBM):
MATCH (a)-[:HTTP_CALLS]-&gt;(b)
RETURN a.file_path, a.start_line, a.name, b.name

# ------------------------------------------------------------------------------
# C6: Call Graph (Direct &amp; DI Service Method Callers)
# CodeExplorer (ce):
MATCH (caller:Function)-[:CALLS]-&gt;(callee:Function)
WHERE callee.name IN [&lt;target_methods&gt;]
OPTIONAL MATCH (enclosing:Function)-[:DECLARES]-&gt;(caller)
RETURN caller.file_path, COALESCE(enclosing.start_line, caller.start_line), COALESCE(enclosing.name, caller.name), callee.name

# codebase-memory-mcp (CBM):
MATCH (caller)-[:CALLS]-&gt;(callee)
WHERE callee.name = '&lt;target_method&gt;'
RETURN caller.file_path, caller.start_line, caller.name, callee.name

# ------------------------------------------------------------------------------
# C7: Class Inheritance &amp; Interface Implementation
# CodeExplorer (ce):
MATCH (c:Type)-[r:INHERITS_FROM|IMPLEMENTS]-&gt;(p)
RETURN c.file_path, c.start_line, c.name, type(r), p.name

# codebase-memory-mcp (CBM):
MATCH (c)-[r:INHERITS|IMPLEMENTS]-&gt;(p)
RETURN c.file_path, c.start_line, c.name, type(r), p.name</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--win">Group B Queries (C3 to C5, C8 to C11)</span>
                <span>scripts/evaluate_ce_vs_cbm.py: Queue, ORM Table, Data Lineage &amp; 6 Architectural Queries (241 Items)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block"># C3 &amp; C8: Message Queue Producers &amp; Consumers (BullMQ / @OnJob)
MATCH (t:Topic)-[:PUBLISHED_BY]-&gt;(f:Function)
RETURN f.file_path, f.start_line, f.name, t.name;

MATCH (t:Topic)-[:SUBSCRIBED_BY]-&gt;(m:Function)
RETURN m.file_path, m.start_line, m.name, t.name;

# C4 &amp; C10: Database Tables, Entities &amp; Schema Members
MATCH (t:Table) OPTIONAL MATCH (t)-[:IMPLEMENTED_BY]-&gt;(ent:Type)
RETURN COALESCE(ent.file_path, t.path), ent.start_line, COALESCE(ent.name, t.name), t.name;

MATCH (t:Table)-[:IMPLEMENTED_BY]-&gt;(ent:Type)-[:HAS_MEMBER]-&gt;(m:Member)
RETURN ent.file_path, m.start_line, m.name, t.name;

# C5: Code-to-Table Read/Write Lineage (TypeORM &amp; Kysely)
MATCH (q:Query)-[r:WRITES_TO|READS_FROM]-&gt;(t:Table)
OPTIONAL MATCH (q)-[:ACCESSED_BY]-&gt;(f:Function)
RETURN COALESCE(f.file_path, q.file_path), q.start_line, COALESCE(f.name, q.name), type(r), t.name;

# C9: Monorepo Services &amp; Sub-Projects
MATCH (p:Project) RETURN p.path, labels(p), p.name;

# C11: 6 End-to-End Architectural Queries (Single-Query Expressibility)
# Q1: Topics with producers but no consumers (or vice versa):
MATCH (t:Topic) WHERE NOT (t)-[:SUBSCRIBED_BY]-&gt;() OR NOT (t)-[:PUBLISHED_BY]-&gt;() RETURN t.name
# Q2: Pairs of projects sharing a database:
MATCH (p1:Project)-[:USES_DB]-&gt;(d:Database)&lt;-[:USES_DB]-(p2:Project) WHERE p1.name &lt; p2.name RETURN p1.name, p2.name, d.name
# Q3: Top written database tables:
MATCH (q:Query)-[:WRITES_TO]-&gt;(t:Table) RETURN t.name, count(q) AS writes ORDER BY writes DESC LIMIT 10
# Q4: Endpoints transitively writing to a database table within 1..4 call hops:
MATCH (e:Endpoint)-[:EXPOSED_BY]-&gt;(f:Function)-[:CALLS*1..4]-&gt;(w:Function)&lt;-[:DECLARED_IN]-(q:Query)-[:WRITES_TO]-&gt;(t:Table) RETURN e.name, t.name LIMIT 20
# Q5: Per-project endpoint and published topic counts:
MATCH (p:Project) OPTIONAL MATCH (p)-[:EXPOSES]-&gt;(e:Endpoint) OPTIONAL MATCH (p)-[:PUBLISHES_TO]-&gt;(tp:Topic) RETURN p.name, count(DISTINCT e), count(DISTINCT tp)
# Q6: End-to-end chain: Endpoint -&gt; Call Chain (0..3 hops) -&gt; Queue Publish -&gt; Queue Subscriber:
MATCH (e:Endpoint)-[:EXPOSED_BY]-&gt;(f:Function)-[:CALLS*0..3]-&gt;(pub:Function)&lt;-[:PUBLISHED_BY]-(t:Topic)-[:SUBSCRIBED_BY]-&gt;(sub) RETURN e.name, t.name, sub.name LIMIT 20</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--cool">Ground-Truth Spec</span>
                <span>groundtruth/patterns.md: Deterministic Ground-Truth Sampling Rules (seed = 42, 371 Items)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block">All patterns were verified against real source files in \`twentyhq/twenty\` (\`12e8fae471865e9dab60c0b0fd59f318505c804e\`) and \`immich-app/immich\` (\`2153d22f34349de690cd98890ee4c635808da638\`). Test files (\`*.spec.ts\`, \`*.test.ts\`, \`__tests__\`, \`test/\`, \`e2e/\`, \`fixtures/\`, \`test_*.py\`) are excluded across all categories. Stratified random sampling uses \`random.Random(42)\`.

- C1 (Incoming HTTP / GraphQL / FastAPI Endpoints): NestJS \`@Controller\` + \`@(Get|Post|Put|Patch|Delete)\`, NestJS GraphQL \`@Resolver\` + \`@(Query|Mutation|Subscription)\`, and Python FastAPI \`@(app|router).(get|post|put|delete|patch)\`.
- C2 (Outbound Inter-Service / External HTTP Calls): \`fetch(...)\`, \`axios.(get|post|put|delete|patch|request)\`, \`this.httpService.(get|post|...)\` (excluding IMAP \`imapClient.fetch\`).
- C3 &amp; C8 (Message Queues &amp; Async Jobs): \`twenty\` \`enum MessageQueue\` (27 queues), \`@InjectMessageQueue\` producers, \`@Processor\` + \`@Process\` consumers; \`immich\` \`enum QueueName\` (19 queues) &amp; \`enum JobName\`, \`jobRepository.queue\` producers, \`@OnJob\` consumers.
- C4 &amp; C10 (Database Tables &amp; Schema): \`twenty\` TypeORM \`@Entity\` + \`@Column\`/\`@ManyToOne\`/\`@OneToMany\`; \`immich\` \`@Table\` + \`@Column\`/\`@ForeignKeyColumn\`.
- C5 (Code-to-Table Lineage): \`twenty\` \`@InjectRepository\` / \`@InjectWorkspaceScopedRepository\` read (\`find*\`, \`count\`, \`createQueryBuilder\`) &amp; write (\`save\`, \`insert\`, \`update\`, \`delete\`, \`softDelete\`, \`upsert\`); \`immich\` Kysely \`selectFrom\` (read) &amp; \`insertInto\`/\`updateTable\`/\`deleteFrom\` (write).
- C6 (Call Graph): Deterministic sample of 10 service methods across \`*.service.ts\`, tracing non-test callers invoking \`this.&lt;serviceField&gt;.&lt;methodName&gt;(\` or \`this.&lt;methodName&gt;(\`.
- C7 (Inheritance &amp; Implementation): Top 5 base classes/interfaces with &ge;3 non-test classes matching \`export (abstract )?class &lt;Child&gt; (extends|implements) &lt;Parent&gt;\`.</pre>
            </div>
          </details>
        </div>
      </div>
    </section>`},"/benchmarks/04-code-explorer-vs-graphify":{routePath:"/benchmarks/04-code-explorer-vs-graphify",pageKind:"benchmark-case",title:"Case Study 04: CodeExplorer vs Graphify on twenty & immich",description:"Deterministic benchmark comparing CodeExplorer (v1.75.0) and Graphify (v0.9.104) across cold indexing, 371 ground-truth items (seed=42), and MCP server latency on twenty (2.49M LOC) and immich (461k LOC).",canonical:"https://code-explorer.app/benchmarks/04-code-explorer-vs-graphify",ogUrl:"https://code-explorer.app/benchmarks/04-code-explorer-vs-graphify",ogTitle:"Case Study 04: CodeExplorer vs Graphify on twenty & immich",ogDescription:"Deterministic benchmark comparing CodeExplorer (v1.75.0) and Graphify (v0.9.104) across cold indexing, 371 ground-truth items (seed=42), and MCP server latency on twenty (2.49M LOC) and immich (461k LOC).",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 04: CodeExplorer vs Graphify</span>
          </div>
          <span class="measured">twenty (2.49M LOC) &amp; immich (461k LOC)</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 04: CodeExplorer vs Graphify.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Head-to-head indexer and MCP server comparison on immich and twenty: cold indexing scalability, MCP startup latency, and graph completeness across 371 verified items.
            </p>
            <div class="hero-controls">
              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/04-code-explorer-vs-graphify/scripts" target="_blank" rel="noopener" class="btn btn--primary">Inspect Evaluation Scripts</a>
              <a href="#prompts" class="btn">Inspect Queries &amp; MCP Calls</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-ok" data-bind-html="cs04.card_speedup">~6.1× / &gt;18×</span>
            <h2 class="panel-title">Faster Cold Indexing</h2>
            <p class="panel-copy">~18s vs ~108s on immich (3.2k files), and ~147s vs DNF (&gt;45m timeout) on twenty (24.5k files).</p>
            <code class="panel-code">SQLite vs NetworkX JSON</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs04.card_group_a">78% vs 18%</span>
            <h2 class="panel-title">Basic Code &amp; Call Graph</h2>
            <p class="panel-copy">Across 130 basic items: ce scored 102/130 vs Graphify 23/130 (on immich alone: ce 73% vs Graphify 35%).</p>
            <code class="panel-code">6 wins, 2 ties, 0 losses</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs04.card_group_b">~98%</span>
            <h2 class="panel-title">Queues, DB Tables &amp; SQL</h2>
            <p class="panel-copy">ce matched 235/241 queue, ORM table, data-lineage, and sub-project items. Graphify does not index these.</p>
            <code class="panel-code">235 / 241 verified items</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading">100% Tie</span>
            <h2 class="panel-title">Where Graphify Tied</h2>
            <p class="panel-copy">On immich Call Graph &amp; DI (C6), Graphify tied ce at 100% (20/20), resolving TS constructor DI accurately.</p>
            <code class="panel-code">20/20 on immich C6</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs04.participants_html">Tested participants:<br>
          1. CodeExplorer (ce): v1.75.0 (.NET 10, SQLite + OpenCypher, 16 MCP tools)<br>
          2. Graphify (graphifyy): v0.9.104 (Python 3.10, Tree-sitter + NetworkX --code-only, 10 MCP tools)<br>
          3. Target Repositories: twentyhq/twenty (commit 12e8fae4, 24,527 files, 2.49M LOC) &amp; immich-app/immich (commit 2153d22f, 3,205 files, 461k LOC)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Cold indexing &amp; scalability across repository sizes.</h2>
          <p class="band-lede">
            On immich (3,205 files), both tools completed cleanly, with ce finishing ~6.1× faster. On twenty (24,527 files), Graphify&rsquo;s Python AST &amp; NetworkX pipeline stalled on a single CPU core in build_from_json and did not finish within the 45-minute timeout.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Cold Indexing Performance on immich (3,205 files) &amp; twenty (24,527 files)</span>
            <span class="measured">Deterministic --code-only mode</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Metric</th>
                <th>Repo</th>
                <th>CodeExplorer</th>
                <th>Graphify</th>
                <th>Outcome</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Cold Index Wall Time</td>
                <td><code>immich</code> (3.2k files)</td>
                <td class="cell-win">17.60 s (55.1 MB SQLite DB)</td>
                <td class="cell-loss">107.60 s (32.0 MB JSON / 52.3 MB dir)</td>
                <td><strong>ce 6.11× faster</strong></td>
              </tr>
              <tr>
                <td>Cold Index Wall Time</td>
                <td><code>twenty</code> (24.5k files)</td>
                <td class="cell-win">147.33 s (784.0 MB SQLite DB)</td>
                <td class="cell-loss">DNF (&gt;2,740 s / &gt;45.6 min timeout)</td>
                <td><strong>ce &gt;18.6× faster (Graphify timed out)</strong></td>
              </tr>
              <tr>
                <td>Peak Working Set (RAM)</td>
                <td><code>immich</code> / <code>twenty</code></td>
                <td class="cell-win">780.5 MB / 4,660.6 MB</td>
                <td class="cell-loss">956.9 MB / 2,140.0 MB (stalled)</td>
                <td><strong>ce 1.23× lower RAM on immich</strong></td>
              </tr>
              <tr>
                <td>Parse Warnings / Errors</td>
                <td><code>immich</code></td>
                <td class="cell-win">0 warnings</td>
                <td class="cell-loss">9 syntax errors + 171 empty files</td>
                <td><strong>ce 0 parse failures</strong></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-md);">
          <strong>Methodological note on <code>--code-only</code> vs LLM synthesis:</strong> Graphify also offers an optional LLM pass for extracting <code>semantic</code> / <code>rationale</code> nodes from Markdown docs, PDFs, and images. This benchmark evaluates deterministic static code extraction (<code>--code-only</code>) so that every result measures the indexer itself rather than an external LLM&rsquo;s token budget or non-deterministic sampling.
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Graph completeness: basic code vs full architecture.</h2>
          <p class="band-lede">
            Because Graphify timed out on twenty, we report both immich-only recall (66 basic items where both tools completed indexing) and combined recall across all 371 verified items.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Group A: Basic Code (130 Items) &amp; Group B: Queues, DB Tables &amp; SQL (241 Items)</span>
            <span class="measured">ce: 6 Wins, 2 Ties, 0 Losses vs Graphify</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>What Was Tested</th>
                <th>Repo</th>
                <th>Sample Size</th>
                <th>CodeExplorer</th>
                <th>Graphify</th>
                <th>Why the Scores Differ</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Incoming HTTP &amp; FastAPI Endpoints (<code>C1</code>)</td>
                <td><code>immich</code></td>
                <td class="cell-num">20</td>
                <td class="cell-win">100% (20/20)</td>
                <td class="cell-loss">0% (0/20 strict)</td>
                <td>Graphify emits decorator references without extracting route URLs or HTTP verbs.</td>
              </tr>
              <tr>
                <td>Outbound HTTP Client Calls (<code>C2</code>)</td>
                <td><code>immich</code></td>
                <td class="cell-num">6</td>
                <td class="cell-win">83% (5/6)</td>
                <td class="cell-loss">0% (0/6)</td>
                <td>Graphify does not extract outbound HTTP client calls.</td>
              </tr>
              <tr>
                <td>Function &amp; DI Service Call Graph (<code>C6</code>)</td>
                <td><code>immich</code></td>
                <td class="cell-num">20</td>
                <td class="cell-win">100% (20/20)</td>
                <td class="cell-win">100% (20/20)</td>
                <td><strong>Tie at 100%</strong>: Graphify&rsquo;s TS extractor resolves constructor property types (<code>this.svc.method()</code>) on <code>immich</code>.</td>
              </tr>
              <tr>
                <td>Class Inheritance &amp; Mixins (<code>C7</code>)</td>
                <td><code>immich</code></td>
                <td class="cell-num">20</td>
                <td class="cell-num">15% (3/20)</td>
                <td class="cell-num">15% (3/20)</td>
                <td><strong>Tie at 15%</strong>: 17 of 20 DTOs use functional mixin inheritance (<code>extends createZodDto(...)</code>).</td>
              </tr>
              <tr>
                <td>All Basic Code Categories (<code>C1, C2, C6, C7</code>)</td>
                <td><code>twenty</code></td>
                <td class="cell-num">64</td>
                <td class="cell-win">84% (54/64)</td>
                <td class="cell-loss">0% (DNF)</td>
                <td>Graphify timed out during cold indexing on <code>twenty</code>.</td>
              </tr>
              <tr class="row-total">
                <td>Group A Total on <code>immich</code> Only (Both Tools Completed)</td>
                <td><code>immich</code></td>
                <td class="cell-num">66</td>
                <td class="cell-win">73% (48/66)</td>
                <td class="cell-num">35% (23/66)</td>
                <td>Direct comparison on the repository where both indexers completed.</td>
              </tr>
              <tr class="row-total">
                <td>Group A Combined Total (<code>immich</code> + <code>twenty</code>)</td>
                <td>Both</td>
                <td class="cell-num">130</td>
                <td class="cell-win">78% (102/130)</td>
                <td class="cell-loss">18% (23/130)</td>
                <td>6 wins, 2 ties, 0 losses.</td>
              </tr>
              <tr class="row-total">
                <td>Group B Total: Queues, DB Tables &amp; SQL Lineage</td>
                <td>Both</td>
                <td class="cell-num">241</td>
                <td class="cell-win">97.5% (235/241)</td>
                <td class="cell-loss">Not modeled (0/241)</td>
                <td>Graphify parses raw <code>.sql</code> DDL only and does not model Kysely/TypeORM tables, BullMQ queues, or SQL lineage.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">3. MCP server latency &amp; tool trade-offs on immich.</h2>
          <p class="band-lede">
            Because Graphify loads graph.json into an in-memory Python NetworkX graph on startup, cold MCP initialization takes ~3.7 seconds vs ~0.6 seconds for ce&rsquo;s SQLite engine.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>MCP Server Startup &amp; Query Latency on immich</span>
            <span class="measured">ce (36 tools) vs Graphify (10 tools + 6 resources)</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>MCP Operation</th>
                <th>CodeExplorer</th>
                <th>Graphify</th>
                <th>Notes</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Cold MCP Init (<code>initialize</code>)</td>
                <td class="cell-win">599.0 ms (63.9 MB RSS)</td>
                <td class="cell-loss">3,682.9 ms (162.8 MB RSS)</td>
                <td><strong>ce 6.15× faster startup</strong> with 2.55× lower memory.</td>
              </tr>
              <tr>
                <td>Architecture / Hub Overview (<code>get_architecture_map</code> / <code>god_nodes</code>)</td>
                <td class="cell-num">239.2 ms (4.5 KB, 21 sub-projects &amp; DBs)</td>
                <td class="cell-win">21.8 ms (0.7 KB, top 20 high-degree nodes)</td>
                <td><strong>Graphify faster</strong> from in-memory NetworkX graph; <code>ce</code> maps sub-projects, DBs, ingress &amp; egress.</td>
              </tr>
              <tr>
                <td>Graph Schema / Stats (1st / Cached)</td>
                <td class="cell-win">23.3 ms (1.7 ms cached, 51.2 KB)</td>
                <td class="cell-loss">76.1 ms (73.0 ms 2nd call, 0.2 KB)</td>
                <td><strong>ce 3.3× faster (43× cached)</strong>; Graphify&rsquo;s <code>graph_stats</code> spawns <code>git rev-parse</code> on every call.</td>
              </tr>
              <tr>
                <td>Symbol Lookup by Name / ID (<code>AuthService</code>)</td>
                <td class="cell-win">36.1 ms by name (0.4 KB table)</td>
                <td class="cell-num">3.7 ms (<code>Ambiguous: 5 nodes</code>) / 9.6 ms by ID</td>
                <td><strong>ce resolves in 1 turn</strong>; Graphify is fast (9.6 ms) once given the exact slug ID.</td>
              </tr>
              <tr>
                <td>Multi-Token Search (<code>authentication token validate</code>)</td>
                <td class="cell-win">41.7 ms (3.0 KB, FTS5 search)</td>
                <td class="cell-loss">333.8 ms (3.6 KB, depth-2 BFS)</td>
                <td><strong>ce 8.0× faster</strong> via SQLite FTS5 index.</td>
              </tr>
              <tr>
                <td>Inbound Callers by Name / ID (<code>validate</code>)</td>
                <td class="cell-win">37.0 ms by name / 19.0 ms by ID (1.5 KB, 8 callers)</td>
                <td class="cell-num">3.8 ms (<code>Ambiguous: 6 nodes</code>) / 2.4 ms by ID</td>
                <td><strong>ce resolves all 8 callers in 1 turn</strong>; Graphify is fast (2.4 ms) once disambiguated to 1 node ID.</td>
              </tr>
              <tr>
                <td>Unique Capabilities</td>
                <td class="cell-num">OpenCypher queries (3.1 ms), DB &amp; queue lineage, 32+ CI gates, VS Code extension</td>
                <td class="cell-num">Leiden community clustering, Obsidian vault export, D3 HTML export, <code>triage_prs</code></td>
                <td>Different primary design goals (architecture &amp; CI governance vs knowledge-graph clustering &amp; visualization).</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep" style="border-bottom: none;">
      <div class="wrap">
        <div id="prompts" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">
          <div class="prompt-stack-head">
            <h2 class="prompt-stack-title">4. Reproducibility: tested queries, MCP payloads &amp; ground-truth spec.</h2>
            <span class="prompt-stack-note">Click any drawer below to expand verbatim queries</span>
          </div>
          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Queries &amp; Matchers</span>
                <span>scripts/eval_ce_vs_graphify.py: Exact OpenCypher Queries (ce) &amp; NetworkX Graph Extraction Rules (Graphify)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block"># CodeExplorer (ce v1.75.0) OpenCypher Queries:
# C1 (Endpoints):
MATCH (e:Endpoint)-[:EXPOSED_BY]-&gt;(f:Function) RETURN f.file_path, f.start_line, f.name, e.http_method, e.route_template
# C2 (Outbound HTTP):
MATCH (ext:ExternalService)-[r:CALLED_BY]-&gt;(f:Function) WHERE COALESCE(ext.protocol, 'http') &lt;&gt; 'graphql' RETURN f.file_path, COALESCE(r.line, f.start_line), f.name, COALESCE(ext.url, ext.name)
# C3 &amp; C8 (Queues / Topics):
MATCH (t:Topic)-[:PUBLISHED_BY]-&gt;(f:Function) RETURN f.file_path, f.start_line, f.name, t.name;
MATCH (t:Topic)-[:SUBSCRIBED_BY]-&gt;(m:Function) RETURN m.file_path, m.start_line, m.name, t.name
# C4 &amp; C10 (ORM Tables &amp; Schema):
MATCH (t:Table) OPTIONAL MATCH (t)-[:IMPLEMENTED_BY]-&gt;(ent:Type) RETURN COALESCE(ent.file_path, t.path), ent.start_line, COALESCE(ent.name, t.name), t.name
# C5 (Read/Write Lineage):
MATCH (q:Query)-[r:WRITES_TO|READS_FROM]-&gt;(t:Table) OPTIONAL MATCH (q)-[:ACCESSED_BY]-&gt;(f:Function) RETURN COALESCE(f.file_path, q.file_path), q.start_line, COALESCE(f.name, q.name), type(r), t.name
# C6 (Call Graph &amp; DI Callers):
MATCH (caller:Function)-[:CALLS]-&gt;(callee:Function) WHERE callee.name IN [&lt;target_methods&gt;] OPTIONAL MATCH (enclosing:Function)-[:DECLARES]-&gt;(caller) RETURN caller.file_path, COALESCE(enclosing.start_line, caller.start_line), COALESCE(enclosing.name, caller.name), callee.name
# C7 (Inheritance &amp; implements):
MATCH (c:Type)-[r:INHERITS_FROM|IMPLEMENTS]-&gt;(p) RETURN c.file_path, c.start_line, c.name, type(r), p.name

# ------------------------------------------------------------------------------
# Graphify (v0.9.104) graph.json Node &amp; Link Extraction (eval_ce_vs_graphify.py):
# - Cold indexing command: \`graphify index &lt;repo_path&gt; --code-only\`
# - C6 (Call Graph): Links in \`graph.json["links"]\` where \`relation == "calls"\`, matching source function \`(source_file, source_line, label)\` and target method \`.label.split(".")[-1]\`.
# - C7 (Inheritance &amp; implements): Links in \`graph.json["links"]\` where \`relation in ("inherits", "implements")\`, matching child class \`(source_file, source_line, label)\` and parent class/interface label.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--win">MCP JSON-RPC Suite</span>
                <span>scripts/bench_mcp_graphify.py: Exact MCP Tool Invocations Benchmarked Over Persistent stdio</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block"># 1. Graph Schema / Stats
# CodeExplorer (\`ce mcp --root &lt;repo&gt;\`):
{"name": "get_taxonomy", "arguments": {}}
# Graphify (\`python -m graphify.serve graphify-out/graph.json\`):
{"name": "graph_stats", "arguments": {}}

# 2. Multi-Token Semantic / BFS Search
# CodeExplorer:
{"name": "search_identifiers", "arguments": {"query": "authentication token validate", "limit": 15}}
# Graphify:
{"name": "query_graph", "arguments": {"question": "authentication token validate", "depth": 2}}

# 3. Direct Symbol Lookup
# CodeExplorer:
{"name": "find_symbol", "arguments": {"name": "AuthService"}}
# Graphify:
{"name": "get_node", "arguments": {"label": "AuthService"}}

# 4. Inbound Callers by Method Name
# CodeExplorer:
{"name": "get_callers", "arguments": {"target": "validate"}}
# Graphify:
{"name": "get_neighbors", "arguments": {"label": "validate", "relation_filter": "calls"}}

# 5. Call Chain / Shortest Path + OpenCypher Query
# CodeExplorer:
{"name": "get_call_chain", "arguments": {"startFunction": "login", "endFunction": "validate"}}
{"name": "execute_custom_read_cypher", "arguments": {"query": "MATCH (n:Function) RETURN n.name LIMIT 10"}}
# Graphify:
{"name": "shortest_path", "arguments": {"source": "&lt;login_node_id&gt;", "target": "&lt;validate_node_id&gt;", "undirected": true}}</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--cool">Ground-Truth Spec</span>
                <span>groundtruth/patterns.md: Deterministic Ground-Truth Sampling Rules (seed = 42, 371 Items)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block">Identical 371 ground-truth items (\`seed = 42\`) used in Case Study 03 across \`twentyhq/twenty\` (\`12e8fae471865e9dab60c0b0fd59f318505c804e\`) and \`immich-app/immich\` (\`2153d22f34349de690cd98890ee4c635808da638\`):
- Group A (130 Basic Code Items across L1 to L3): C1 Incoming Endpoints (40), C2 Outbound HTTP Calls (18), C6 Call Graph &amp; DI Callers (32), C7 Class Inheritance &amp; Interface Implementation (40).
- Group B (241 Architectural Items across L4 to L5): C3 Queue Producers &amp; Consumers (70), C4 ORM Tables (40), C5 Code-to-Table Read/Write Lineage (40), C8 Topic Resolution (37), C9 Services &amp; Topology Edges (14), C10 Table Columns &amp; Relations (28), C11 End-to-End Architectural Queries (12).</pre>
            </div>
          </details>
        </div>
      </div>
    </section>`},"/benchmarks/05-polyglot-architecture-and-blast-radius":{routePath:"/benchmarks/05-polyglot-architecture-and-blast-radius",pageKind:"benchmark-case",title:"Case Study 05: OrchardCore Modular .NET Audit (CodeExplorer vs ripgrep)",description:"",canonical:"https://code-explorer.app/benchmarks/05-polyglot-architecture-and-blast-radius",ogUrl:"https://code-explorer.app/benchmarks/05-polyglot-architecture-and-blast-radius",ogTitle:"Case Study 05: OrchardCore Modular .NET Audit (CodeExplorer vs ripgrep)",ogDescription:"",mainHtml:'<section class="hero-stage">\n      <div class="wrap">\n        <div class="crumb-bar">\n          <div class="crumb-trail">\n            <a href="/benchmarks">Benchmarks Catalog</a>\n            <span>/</span>\n            <span class="tone-ink">Case Study 05: OrchardCore Modular .NET Audit</span>\n          </div>\n          <span class="measured" data-bind="cs05.crumb_scale">334 projects, 7,837 files</span>\n        </div>\n\n        <div class="hero-top">\n          <div>\n            <h1 class="hero-title">Case Study 05: OrchardCore modular .NET monolith audit (ce vs ripgrep).</h1>\n          </div>\n          <div class="hero-aside">\n            <p class="hero-lede">\n              Paired A/B agent benchmark on OrchardCMS/OrchardCore (open-source modular .NET CMS &amp; framework with 334 projects and 7,837 files). Measures how an AI agent resolves AppSec authorization invariants, YesSql.ISession boundary violations, cross-module PublishedAsync pipelines, and IContentManager blast radius.\n            </p>\n            <div class="hero-controls">\n              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/05-polyglot-architecture-and-blast-radius/results" target="_blank" rel="noopener" class="btn btn--primary">Inspect Outputs &amp; metrics.json</a>\n              <a href="#prompts" class="btn">Inspect Tested Prompts</a>\n            </div>\n          </div>\n        </div>\n\n        <div class="panels panels--4" style="margin-top: var(--space-xl);">\n          <div class="panel panel--sunken">\n            <span class="reading tone-ok" data-bind="cs05.card_speedup">~2.5×</span>\n            <h2 class="panel-title">Shorter Time to Answer</h2>\n            <p class="panel-copy" data-bind-html="cs05.card_speed_copy">4m 25s with CodeExplorer vs 11m 04s with ripgrep + view_file across 334 projects.</p>\n            <code class="panel-code" data-bind="cs05.card_turns_footer">92 turns / 184 steps vs 193 turns / 389 steps</code>\n          </div>\n          <div class="panel panel--sunken">\n            <span class="reading tone-accent" data-bind="cs05.card_tokens">~4.1×</span>\n            <h2 class="panel-title">Fewer Input Tokens</h2>\n            <p class="panel-copy" data-bind-html="cs05.card_tokens_copy">~3.9M cumulative input tokens with CodeExplorer vs ~16.1M with ripgrep (~2.9× lower API cost).</p>\n            <code class="panel-code" data-bind="cs05.card_cost_footer">$0.65 vs $1.91 (~1.9× less tool output)</code>\n          </div>\n          <div class="panel panel--sunken">\n            <span class="reading tone-cool" data-bind="cs05.card_reads">~3.3×</span>\n            <h2 class="panel-title">Fewer File Reads</h2>\n            <p class="panel-copy" data-bind-html="cs05.card_reads_copy">36 view_file reads (31 C# files + 5 schema/spill reads) vs 117 C# reads by ripgrep.</p>\n            <code class="panel-code" data-bind="cs05.card_reads_footer">287 KB vs 560 KB tool output</code>\n          </div>\n          <div class="panel panel--sunken">\n            <span class="reading" data-bind="cs05.card_recall">100% vs ~93%</span>\n            <h2 class="panel-title">Symbol Recall &amp; Context</h2>\n            <p class="panel-copy" data-bind-html="cs05.card_recall_copy">CodeExplorer matched all 331/331 items; ripgrep guessed or missed 24 Task 4a caller methods from raw grep lines.</p>\n            <code class="panel-code" data-bind="cs05.card_recall_footer">331/331 vs 307/331 verified items</code>\n          </div>\n        </div>\n\n        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs05.participants_html">Tested participants:<br>\n          1. CodeExplorer (ce): v1.78.1 (.NET 10, SQLite + OpenCypher MCP server, 134,067 nodes, 288,527 edges)<br>\n          2. Lexical CLI Baseline: ripgrep (rg v14.1.1) + view_file (pure lexical search + file reading; custom AST scripts disallowed, complementing Case 02)<br>\n          3. LLM Model: Gemini 3.8 Flash (matched subagent prompts without write_to_file; see methodological note below)<br>\n          4. Target Repository: OrchardCMS/OrchardCore (commit 05ad2d9a, 334 projects, 7,837 files)</div>\n      </div>\n    </section>\n\n    <section class="band">\n      <div class="wrap">\n        <div class="band-head">\n          <h2 class="band-title">1. What the agents had to solve on OrchardCore.</h2>\n          <p class="band-lede">\n            In a modular .NET monolith like OrchardCore, architectural rules span 334 projects: verifying security invariants, layer boundaries, reactive event pipelines, and breaking-change blast radius requires joining attributes, inheritance hierarchies, member access, and multi-hop call chains.\n          </p>\n        </div>\n\n        <div class="panels panels--4">\n          <div class="panel">\n            <div class="panel-head">\n              <h3 class="panel-title">AppSec AuthorizeAsync Audit</h3>\n              <span class="badge badge--accent">Task 1</span>\n            </div>\n            <p class="panel-copy">Audit all 88 non-anonymous [HttpPost], [HttpPut], and [HttpDelete] actions across 5 security modules to find which actions call AuthorizeAsync within 1 to 2 hops vs which do not.</p>\n            <code class="panel-code">67 calling vs 21 missing AuthorizeAsync</code>\n          </div>\n          <div class="panel">\n            <div class="panel-head">\n              <h3 class="panel-title">YesSql.ISession Boundary Audit</h3>\n              <span class="badge badge--win">Task 2</span>\n            </div>\n            <p class="panel-copy">Identify every Controller and Display Driver across src/OrchardCore.Modules/* that bypasses domain services to invoke operations directly on an injected YesSql.ISession field.</p>\n            <code class="panel-code">25 classes across 13 modules (ACCESSED_BY)</code>\n          </div>\n          <div class="panel">\n            <div class="panel-head">\n              <h3 class="panel-title">PublishedAsync Event Fan-Out</h3>\n              <span class="badge badge--cool">Task 3</span>\n            </div>\n            <p class="panel-copy">Locate all PublishingAsync and PublishedAsync content handlers across the monorepo and trace their 1 to 2 hop downstream calls into DB/index stores, tag caches, and workflows.</p>\n            <code class="panel-code">16 concrete + 6 base handler methods</code>\n          </div>\n          <div class="panel">\n            <div class="panel-head">\n              <h3 class="panel-title">IContentManager Blast Radius</h3>\n              <span class="badge badge--accent">Task 4</span>\n            </div>\n            <p class="panel-copy">Find all projects calling GetAsync, CreateAsync, or PublishAsync (including via DI service locators) and isolate over-coupled DEPENDS_ON projects with 0 calls.</p>\n            <code class="panel-code">19 caller projects + 38 over-coupled projects</code>\n          </div>\n        </div>\n      </div>\n    </section>\n\n    <section class="band band--deep">\n      <div class="wrap">\n        <div class="band-head">\n          <h2 class="band-title">2. Execution telemetry scorecard.</h2>\n          <p class="band-lede">\n            Measured from paired A/B agent runs on the same OrchardCMS/OrchardCore commit with matched subagent system prompts.\n          </p>\n        </div>\n\n        <div class="table-surface">\n          <div class="table-caption-bar">\n            <span>Paired A/B Agent Telemetry on OrchardCMS/OrchardCore (334 Projects, 7,837 Files)</span>\n            <span class="measured">Source: benchmarks/05-polyglot-architecture-and-blast-radius/results/metrics.json</span>\n          </div>\n          <table class="bench-table">\n            <thead>\n              <tr>\n                <th>Metric</th>\n                <th>ripgrep + File Reads</th>\n                <th>CodeExplorer</th>\n                <th>Delta</th>\n              </tr>\n            </thead>\n            <tbody>\n              <tr>\n                <td>Time to Answer (<code>send_message</code>)</td>\n                <td class="cell-loss" data-bind="cs05.tbl_time_rg">664 s (11 min 04 sec) / 701 s wall</td>\n                <td class="cell-win" data-bind="cs05.tbl_time_ce">265 s (4 min 25 sec) / 299 s wall</td>\n                <td class="cell-win" data-bind="cs05.tbl_time_delta">2.51× faster (2.34× wall)</td>\n              </tr>\n              <tr>\n                <td>LLM Turns, Steps &amp; Compactions</td>\n                <td class="cell-loss" data-bind="cs05.tbl_turns_rg">193 turns / 389 steps (0 compactions)</td>\n                <td class="cell-win" data-bind="cs05.tbl_turns_ce">92 turns / 184 steps (0 compactions)</td>\n                <td class="cell-win" data-bind="cs05.tbl_turns_delta">2.10× fewer turns (2.11× fewer steps)</td>\n              </tr>\n              <tr>\n                <td>Total Tool Calls &amp; File Reads</td>\n                <td class="cell-loss" data-bind-html="cs05.tbl_calls_rg">192 (74 <code>rg</code> + 117 <code>view_file</code>, 1 <code>send_message</code>)</td>\n                <td class="cell-win" data-bind-html="cs05.tbl_calls_ce">91 (54 <code>mcp</code> + 36 <code>view_file</code>, 1 <code>send_message</code>)</td>\n                <td class="cell-win" data-bind="cs05.tbl_calls_delta">2.11× fewer calls (3.25× fewer reads)</td>\n              </tr>\n              <tr>\n                <td>Raw Tool Output Ingested</td>\n                <td class="cell-loss" data-bind="cs05.tbl_payload_rg">573,355 chars (~143,339 tokens)</td>\n                <td class="cell-win" data-bind="cs05.tbl_payload_ce">294,355 chars (~73,589 tokens)</td>\n                <td class="cell-win" data-bind="cs05.tbl_payload_delta">1.95× less tool payload</td>\n              </tr>\n              <tr>\n                <td>Cumulative Input Tokens</td>\n                <td class="cell-loss" data-bind="cs05.tbl_in_rg">16,098,025 est. (25,878,074 API tokens)</td>\n                <td class="cell-win" data-bind="cs05.tbl_in_ce">3,910,897 est. (6,000,146 API tokens)</td>\n                <td class="cell-win" data-bind="cs05.tbl_in_delta">4.12× fewer input tokens (4.31× API tokens)</td>\n              </tr>\n              <tr>\n                <td>API Cost w/ 90% Cache Discount (Gemini Flash)</td>\n                <td class="cell-num" data-bind="cs05.tbl_cost_rg">$1.91 Flash ($7.51 Pro)</td>\n                <td class="cell-win" data-bind="cs05.tbl_cost_ce">$0.65 Flash ($2.52 Pro)</td>\n                <td class="cell-win" data-bind="cs05.tbl_cost_delta">2.94× cheaper</td>\n              </tr>\n              <tr>\n                <td>Symbol Recall &amp; Context Health (Tasks 1 to 4)</td>\n                <td class="cell-num" data-bind="cs05.tbl_recall_rg">92.7% (307 / 331; 24 Task 4a caller errors)</td>\n                <td class="cell-win" data-bind="cs05.tbl_recall_ce">100% (331 / 331 verified items)</td>\n                <td data-bind-html="cs05.tbl_recall_delta"><strong>+24 caller edges resolved via AST</strong></td>\n              </tr>\n            </tbody>\n          </table>\n        </div>\n\n        <div class="tradeoff-note" style="margin-top: var(--space-lg);">\n          <strong>Methodological &amp; impartiality notes:</strong><br>\n          1. <strong>Controlled baseline scope (pure <code>rg</code> + <code>view_file</code>):</strong> Unlike Case Studies 01 and 02 (where baseline agents had <code>write_to_file</code> access and wrote custom TypeScript/Python AST parsers), neither subagent here had <code>write_to_file</code>, and <code>baseline-agent.md</code> explicitly forbade writing custom AST/graph-walking scripts. This isolates the interactive <code>rg</code> + <code>view_file</code> workflow most coding agents run out of the box.<br>\n          2. <strong>Why <code>CodeExplorer</code> finished in <code>92</code> turns (<code>184</code> steps) vs <code>ripgrep</code>\'s <code>193</code> turns (<code>389</code> steps):</strong> The <code>CodeExplorer</code> MCP agent executed <code>54</code> MCP calls (<code>51</code> <code>execute_custom_read_cypher</code> queries, <code>2</code> <code>get_taxonomy</code> calls, and <code>1</code> <code>get_workspace_status</code> call) plus <code>36</code> targeted <code>view_file</code> calls (<code>31</code> C# source files, <code>1</code> MCP schema, and <code>4</code> spilled outputs). By contrast, <code>ripgrep</code> ran <code>74</code> <code>rg</code> searches and <code>117</code> C# <code>view_file</code> reads, burning <code>4.12×</code> more cumulative input tokens (<code>16.10M</code> vs <code>3.91M</code>).<br>\n          3. <strong>Source-verified output audit (<code>331</code> ground-truth items):</strong> <code>CodeExplorer</code> matched <code>331 / 331</code> items (<code>100%</code>). <code>ripgrep</code> matched <code>307 / 331</code> (<code>92.7%</code>): while it reached <code>100%</code> on Tasks 1, 2, 3, and 4b, on Task 4a (<code>94 / 118</code>) raw <code>rg</code> call-site lines lacked enclosing method headers, leading the agent to guess non-existent method names (<code>AddCreateContentEndpoint</code> instead of <code>HandleAsync</code> on <code>CreateEndpoint</code>, <code>ExecuteAsync</code> instead of <code>SaveAsync</code> on <code>SaveContentItemsStep</code>, <code>BuildMenuShapeAsync</code> instead of <code>DiscoverAsync</code> on <code>MenuShapes</code>, and <code>MetaWeblogGetUserBlogsAsync</code> in place of 5 distinct <code>MetaWeblog*</code> methods on <code>MetaWeblogHandler</code>).\n        </div>\n      </div>\n    </section>\n\n    <section class="band" style="border-bottom: none;">\n      <div class="wrap">\n        <div id="prompts" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">\n          <div class="prompt-stack-head">\n            <h2 class="prompt-stack-title">3. Reproducibility: matched prompts &amp; task specification.</h2>\n            <span class="prompt-stack-note">Click any drawer below to expand verbatim prompt text</span>\n          </div>\n          <details class="prompt-drawer">\n            <summary>\n              <span class="prompt-drawer-title">\n                <span class="badge badge--accent">Task Prompt</span>\n                <span>prompts/task-prompt.md: Shared 4-Part OrchardCore Audit Task Given Identically to Both Agents</span>\n              </span>\n              <span class="prompt-drawer-toggle"></span>\n            </summary>\n            <div class="prompt-drawer-body">\n              <pre class="code-block" data-bind-file="benchmarks/05-polyglot-architecture-and-blast-radius/prompts/task-prompt.md">Perform a comprehensive, 4-part architectural governance, security, and blast-radius audit on the `OrchardCMS/OrchardCore` modular .NET monolith located at `./repos/OrchardCore` (`src/OrchardCore/OrchardCore.*` and `src/OrchardCore.Modules/OrchardCore.*`, excluding `test/` projects, `src/Templates/`, and `*Tests*` files). Answer all four engineering questions below with 100% completeness (do not guess, sample, or truncate items). To keep both query outputs and your final report concise, **do not include file path columns** and **use compact grouped bullet lists** rather than wide multi-line Markdown tables:\n\n1. **Task 1 — AppSec Authorization Audit: State-Mutating Controller Actions Missing `AuthorizeAsync` (`Negative Graph Query / NOT EXISTS`)**:\n   Across the following 5 security-sensitive modules:\n   - `OrchardCore.Users`\n   - `OrchardCore.Roles`\n   - `OrchardCore.Tenants`\n   - `OrchardCore.ContentTypes`\n   - `OrchardCore.Workflows`\n   Identify every public action method (`ControllerClass.ActionMethod`) on an MVC/API controller class (inheriting from `Controller` or `ControllerBase`, or decorated with `[Controller]` / `[ApiController]`) that is decorated with `[HttpPost]`, `[HttpPut]`, or `[HttpDelete]` (excluding `[AllowAnonymous]` actions) and:\n   - **(1a)** Calls `IAuthorizationService.AuthorizeAsync` directly (`1` hop) or via a helper method within `2` hops (`1..2` hops), vs\n   - **(1b)** **Does NOT call `AuthorizeAsync` within `1..2` hops** (indicating whether class-level attributes like `[Admin]` or `[Authorize]` are present on the controller).\n   Group by module and report the exact counts and compact list of `ControllerClass.ActionMethod` for both **(1a)** and **(1b)**.\n\n2. **Task 2 — Architectural Boundary Violations: Web &amp; UI Layer Bypassing Services to Access `YesSql.ISession` Directly (`Semantic Invariant Audit`)**:\n   In a clean modular architecture, MVC controllers and UI display drivers should interact with domain services (`IContentManager`, `ISiteService`, stores) rather than querying or mutating the low-level `YesSql.ISession` document session directly.\n   Across all modules under `src/OrchardCore.Modules/*`, identify every non-test **Controller** class (inheriting from `Controller` or `ControllerBase`, or decorated with `[Controller]` / `[ApiController]`) and **Display Driver** class (inheriting from or implementing any `*DisplayDriver*` contract such as `DisplayDriver`, `ContentPartDisplayDriver`, `ContentFieldDisplayDriver`, `SectionDisplayDriver`, or `IDisplayDriver`) whose methods **directly invoke methods on an injected `YesSql.ISession` member** (such as `Query`, `QueryIndex`, `SaveAsync`, `GetAsync`, `CancelAsync`, `CreateConnectionAsync`).\n   Group by module (`.csproj` project name), report the exact count of violating classes and method-to-session operation pairs per module, and list `ClassName.MethodName -&gt; ISession.Operation`.\n\n3. **Task 3 — Event Lifecycle Side-Effect Fan-Out: What Happens Across Modules When Content Is Published? (`Cross-Module Reactive Pipeline`)**:\n   When content is published in `OrchardCore`, the pipeline broadcasts `PublishingAsync` and `PublishedAsync` across content handlers (`IContentHandler`, `ContentHandlerBase`, `ContentPartHandler`, `ContentFieldHandler`, or `IScopedIndexProvider` implementations).\n   Identify every non-test class across `src/OrchardCore/` and `src/OrchardCore.Modules/` that implements or overrides `PublishingAsync` or `PublishedAsync`, and trace its **downstream calls (`1 to 2` hops)** to classify what side-effects each handler triggers:\n   - **Database / Session / Index / Content / Store mutation** (`ISession`, `*Entries`, `*IndexProvider`, `*TaskManager`, `*Store`, `IContentManager`, `IContentDefinitionManager`, `ISiteService`, `IAuditTrailManager`, `ISitemapUpdateHandler`)\n   - **Cache invalidation** (`ITagCache`, `RemoveTagAsync`, `IMemoryCache`, `IDistributedCache`)\n   - **Workflow / Deferred Task / Template trigger** (`IWorkflowManager`, `TriggerEventAsync`, `AddDeferredTask`, `ILiquidTemplateManager`)\n   Report the total count of handler methods (`ClassName.PublishingAsync` / `ClassName.PublishedAsync`) and list `Module -&gt; HandlerClass.MethodName -&gt; [Triggered Downstream Services/Methods]`.\n\n4. **Task 4 — Breaking-Change Blast Radius &amp; Unused Project Dependencies (`Layer 1–3 Cross-Layer Coupling`)**:\n   Suppose we introduce a breaking signature change to these 3 core contract methods on `IContentManager` in `OrchardCore.ContentManagement.Abstractions`:\n   - `IContentManager.GetAsync`\n   - `IContentManager.CreateAsync`\n   - `IContentManager.PublishAsync`\n   Determine across all non-test projects (`!is_test`, excluding `test/` and `src/Templates/`):\n   - **(4a) Direct Call Blast Radius**: Which projects contain classes/methods that invoke at least one of these 3 `IContentManager` methods directly (`1` hop), and how many distinct caller methods (`CallerClass.CallerMethod`) invoke each of `GetAsync`, `CreateAsync`, and `PublishAsync` per project?\n   - **(4b) Over-Coupled Projects (`DEPENDS_ON` Without Call Usage)**: Which non-test projects declare a direct project dependency (`ProjectReference` / `DEPENDS_ON`) on `OrchardCore.ContentManagement.Abstractions`, yet contain **zero calls (`0` call sites)** to any of `GetAsync`, `CreateAsync`, or `PublishAsync`?\n   Report exact project counts and compact lists for both **(4a)** and **(4b)**.</pre>\n            </div>\n          </details>\n\n          <details class="prompt-drawer">\n            <summary>\n              <span class="prompt-drawer-title">\n                <span class="badge badge--win">System Prompt (ce)</span>\n                <span>prompts/graph-agent.md: Symmetric System Instructions for CodeExplorer Agent</span>\n              </span>\n              <span class="prompt-drawer-toggle"></span>\n            </summary>\n            <div class="prompt-drawer-body">\n              <pre class="code-block" data-bind-file="benchmarks/05-polyglot-architecture-and-blast-radius/prompts/graph-agent.md"># Agent System Instructions\n\nYou are a senior software engineer analyzing the `OrchardCMS/OrchardCore` repository (`334` projects, `7,837` files).\nAnswer all architectural questions with 100% accuracy and completeness based on the repository.\n\nCRITICAL BENCHMARK RULES:\n1. Use the **CodeExplorer MCP server (`codeexplorer`)** (passing `workspacePath: "./repos/OrchardCore"` to query the pre-indexed `.codeexplorer/graph.db` semantic graph) and `read_file` to inspect source files when needed. Do not invoke `ce` or `ce.exe` via `run_command`; call the `codeexplorer` MCP tools directly.\n2. To inspect the graph schema (node labels, properties, and relationship types) and query the graph, use the `codeexplorer` MCP tools (`get_taxonomy`, `execute_custom_read_cypher`, `find_symbol`, `get_call_chain`, `get_project_dependencies`, etc.).\n3. You are STRICTLY FORBIDDEN from reading or referencing anything inside `benchmarks/`.\n4. Do NOT modify or create any files inside the repository.</pre>\n            </div>\n          </details>\n\n          <details class="prompt-drawer">\n            <summary>\n              <span class="prompt-drawer-title">\n                <span class="badge badge--cool">System Prompt (ripgrep)</span>\n                <span>prompts/baseline-agent.md: Symmetric System Instructions for ripgrep Baseline Agent</span>\n              </span>\n              <span class="prompt-drawer-toggle"></span>\n            </summary>\n            <div class="prompt-drawer-body">\n              <pre class="code-block" data-bind-file="benchmarks/05-polyglot-architecture-and-blast-radius/prompts/baseline-agent.md"># Agent System Instructions\n\nYou are a senior software engineer analyzing the `OrchardCMS/OrchardCore` repository (`334` projects, `7,837` files).\nAnswer all architectural questions with 100% accuracy and completeness based on the repository.\n\nCRITICAL BENCHMARK RULES:\n1. You have access to the `ripgrep` CLI (`rg`) via `run_command` and `read_file` to inspect source files when needed. Do not write custom AST/graph-walking scripts; use `rg` searches and `read_file`.\n2. You are STRICTLY FORBIDDEN from running `ce`, `ce.exe`, `codeexplorer`, or accessing `.codeexplorer/graph.db` (always pass `-g "!.codeexplorer/**"` if needed).\n3. You are STRICTLY FORBIDDEN from reading or referencing anything inside `benchmarks/`.\n4. Do NOT modify or create any files inside the repository.</pre>\n            </div>\n          </details>\n        </div>\n      </div>\n    </section>'},"/benchmarks/06-precommit-architectural-guard":{routePath:"/benchmarks/06-precommit-architectural-guard",pageKind:"benchmark-case",title:"Case Study 06: Pre-Commit Architectural Guard (CodeExplorer)",description:"",canonical:"https://code-explorer.app/benchmarks/06-precommit-architectural-guard",ogUrl:"https://code-explorer.app/benchmarks/06-precommit-architectural-guard",ogTitle:"Case Study 06: Pre-Commit Architectural Guard (CodeExplorer)",ogDescription:"",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 06: Pre-Commit Architectural Guard</span>
          </div>
          <span class="measured" data-bind="cs06.crumb_scale">18 subsystems, 4,216 files</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 06: Automated Pre-Commit Architectural Guard.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Pre-commit architectural guard walkthrough on argoproj/argo-cd (Kubernetes continuous delivery platform with 18 subsystems and 4,216 files). Demonstrates how CodeExplorer Semantic Invariant Gates (SIG) evaluate 6 production rules in 422 ms with zero LLM tokens ($0.00), freeze legacy technical debt into line-shift-immune SHA-256 baselines, block single-line PR regressions, and monotonically tighten architectural ceilings.
            </p>
            <div class="hero-controls">
              <a href="#scorecard" class="btn btn--primary">View Invariant Scorecard</a>
              <a href="#ratchet-lifecycle" class="btn">4-Phase Ratchet Lifecycle</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-ok" data-bind="cs06.card_speedup">422 ms</span>
            <h2 class="panel-title">Pre-Commit Latency</h2>
            <p class="panel-copy" data-bind-html="cs06.card_speed_copy">Evaluates all 6 production architectural gates across 4,216 files in 422 ms (330 ms pure query execution).</p>
            <code class="panel-code" data-bind="cs06.card_turns_footer">55 ms average query time per gate</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs06.card_tokens">0</span>
            <h2 class="panel-title">Tokens Consumed</h2>
            <p class="panel-copy" data-bind-html="cs06.card_tokens_copy">0 tokens ($0.00 cost). Runs entirely offline as a compiled native binary with zero network calls.</p>
            <code class="panel-code" data-bind="cs06.card_cost_footer">Zero API cost / local pre-commit execution</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs06.card_cli">Exit 1 / 0</span>
            <h2 class="panel-title">Native CI/CD Blocker</h2>
            <p class="panel-copy" data-bind-html="cs06.card_cli_copy">Blocks Day 0 debt (Exit 1), freezes SHA-256 baselines (Exit 0), and rejects any single new PR leak.</p>
            <code class="panel-code" data-bind="cs06.card_cli_footer">Line-shift immune SHA-256 ratchet</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs06.card_recall">6 of 6</span>
            <h2 class="panel-title">Architectural Invariants</h2>
            <p class="panel-copy" data-bind-html="cs06.card_recall_copy">Evaluated all 6 rules: 2 PASS, 1 WARN, 3 FAIL with exact call sites, line numbers, and AST symbols.</p>
            <code class="panel-code" data-bind="cs06.card_recall_footer">3 layer leaks &amp; 12 god types caught</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs06.participants_html">Evaluated system &amp; configuration:<br>
          1. CodeExplorer (ce): v1.80.2 (.NET 11, SQLite + OpenCypher, Semantic Invariant Gates &amp; SHA-256 Baseline Ratchet, 45,616 nodes, 134,005 edges)<br>
          2. Evaluation Mode: 4-Phase CLI Lifecycle (ce gate check + ce gate ratchet --update + ce gate details --new): 0 tokens, $0.00<br>
          3. Target Repository: argoproj/argo-cd (commit 6d689fa3, 18 subsystems, 4,216 files)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. The 6 pre-commit architectural invariant rules.</h2>
          <p class="band-lede">
            To prevent architecture drift, teams enforce rules at pre-commit and PR review time. CodeExplorer evaluates these invariants deterministically as zero-match OpenCypher gates.
          </p>
        </div>

        <div class="panels panels--3">
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Reverse Layer Inversion Shield</h3>
              <code class="badge badge--win">SIG-P-B-101</code>
            </div>
            <p class="panel-copy">Foundational utility packages (util/**) must remain self-contained and never directly call package functions in server/**, controller/**, reposerver/**, or cmpserver/**.</p>
            <code class="panel-code">FAIL: 3 direct cross-layer call sites</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">God Type Complexity Limit</h3>
              <code class="badge badge--accent">SIG-C-R-102</code>
            </div>
            <p class="panel-copy">No production Go struct or TypeScript class may declare more than 30 methods in a single type unit without an explicit ratchet exemption.</p>
            <code class="panel-code">FAIL: 12 types (11 Go + 1 TS)</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">GitOps Engine Isolation</h3>
              <code class="badge badge--cool">SIG-P-B-103</code>
            </div>
            <p class="panel-copy">The embedded gitops-engine/** subsystem must remain strictly standalone with zero upward package calls into Argo CD host services or util/**.</p>
            <code class="panel-code">PASS: 0 upward host calls</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Storage Abstraction Boundary</h3>
              <code class="badge badge--cool">SIG-P-B-104</code>
            </div>
            <p class="panel-copy">Raw Kubernetes secret repository storage backends (repository_secrets.go) must be encapsulated strictly within util/db.</p>
            <code class="panel-code">PASS: 0 external call sites</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Notification DB Decoupling</h3>
              <code class="badge badge--win">SIG-P-B-105</code>
            </div>
            <p class="panel-copy">Notification utilities in util/notification must not instantiate standalone database instances or call db.NewDB directly.</p>
            <code class="panel-code">FAIL: 2 violation sites in service.go</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Public Auth Bypass Audit</h3>
              <code class="badge badge--accent">SIG-M-B-106</code>
            </div>
            <p class="panel-copy">All server endpoints implementing AuthFuncOverride bypass standard authentication and must be explicitly audited and allowlisted.</p>
            <code class="panel-code">WARN: 4 legitimate overrides audited</code>
          </div>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div id="scorecard" class="band-head">
          <h2 class="band-title">2. Semantic invariant gates evaluation scorecard.</h2>
          <p class="band-lede">
            Evaluated natively on argoproj/argo-cd @ 6d689fa3 via ce gate check in 422 ms total wall-clock time (330 ms pure OpenCypher query execution) across 45,616 graph nodes and 134,005 relationships.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Automated Gate Evaluation on argoproj/argo-cd (18 Subsystems, 4,216 Files)</span>
            <span class="measured">Source: benchmarks/06-precommit-architectural-guard/results/metrics.json</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Gate ID &amp; Invariant Rule</th>
                <th>Scope &amp; Dimension</th>
                <th>Target Subsystems</th>
                <th>Query Time</th>
                <th>Findings Detected</th>
                <th>Status &amp; Action</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>SIG-P-B-101</code><br><strong>Reverse Layer Inversion Shield</strong></td>
                <td>Module &times; Boundaries</td>
                <td><code>util/**</code> &rarr; <code>server/**</code>, <code>controller/**</code>, <code>reposerver/**</code>, <code>cmpserver/**</code></td>
                <td class="cell-num">58 ms</td>
                <td class="cell-loss">3 call sites</td>
                <td class="cell-loss"><code class="badge badge--loss">FAIL</code> (Block PR)</td>
              </tr>
              <tr>
                <td><code>SIG-C-R-102</code><br><strong>God Type Complexity Limit</strong></td>
                <td>Class &times; Resources</td>
                <td>Production Go structs &amp; TS classes (&gt;30 methods)</td>
                <td class="cell-num">206 ms</td>
                <td class="cell-loss">12 types (11 Go + 1 TS)</td>
                <td class="cell-loss"><code class="badge badge--loss">FAIL</code> (Block PR)</td>
              </tr>
              <tr>
                <td><code>SIG-P-B-103</code><br><strong>GitOps Engine Layer Isolation</strong></td>
                <td>Module &times; Boundaries</td>
                <td><code>gitops-engine/**</code> &rarr; Argo CD host packages</td>
                <td class="cell-num">27 ms</td>
                <td class="cell-win">0 upward calls</td>
                <td class="cell-win"><code class="badge badge--win">PASS</code> (Isolated)</td>
              </tr>
              <tr>
                <td><code>SIG-P-B-104</code><br><strong>Storage Abstraction Boundary</strong></td>
                <td>Module &times; Boundaries</td>
                <td><code>repository_secrets.go</code> access outside <code>util/db</code></td>
                <td class="cell-num">29 ms</td>
                <td class="cell-win">0 external calls</td>
                <td class="cell-win"><code class="badge badge--win">PASS</code> (Encapsulated)</td>
              </tr>
              <tr>
                <td><code>SIG-P-B-105</code><br><strong>Notification Subsystem Decoupling</strong></td>
                <td>Module &times; Boundaries</td>
                <td><code>util/notification</code> direct <code>db.NewDB</code> calls</td>
                <td class="cell-num">2 ms</td>
                <td class="cell-loss">2 call sites</td>
                <td class="cell-loss"><code class="badge badge--loss">FAIL</code> (Block PR)</td>
              </tr>
              <tr>
                <td><code>SIG-M-B-106</code><br><strong>Public Authentication Bypass Audit</strong></td>
                <td>Method &times; Boundaries</td>
                <td><code>server/**</code> <code>AuthFuncOverride</code> endpoints</td>
                <td class="cell-num">8 ms</td>
                <td class="cell-num">4 endpoints</td>
                <td class="cell-num"><code class="badge badge--accent">WARN</code> (Audited)</td>
              </tr>
              <tr style="border-top: 1px solid var(--border-subtle);">
                <td><strong>Total Suite (incl. SQLite &amp; YAML init)</strong></td>
                <td>Agnostic</td>
                <td>4,216 files across 18 subsystems</td>
                <td class="cell-win"><strong>422 ms</strong> (330 ms queries)</td>
                <td>17 errors + 4 warnings</td>
                <td class="cell-loss"><strong>Exit 1 (Build Blocked)</strong></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);">
          <strong>Key architectural takeaways: Deterministic pre-commit gate execution</strong><br>
          1. <strong>Compiled native binary execution:</strong> <code>ce gate check</code> evaluates declarative OpenCypher structural invariant rules directly against the local SQLite graph database. All 6 gates complete in <strong>422 milliseconds</strong> wall-clock time (<strong>330 ms</strong> pure query execution) with <strong>0 tokens</strong> and <strong>$0.00 cost</strong>.<br>
          2. <strong>Zero false positives on Dependency Inversion:</strong> By targeting direct exported package-level calls (<code>NOT (:Type)-[:HAS_METHOD]-&gt;(callee)</code>), <code>SIG-P-B-101</code> isolates the exact 3 true upward dependencies in <code>util/session</code>, <code>util/app/discovery</code>, and <code>util/webhook</code> while cleanly passing <code>util/git</code> and <code>util/helm</code> (which properly invert dependencies via local cache interfaces).<br>
          3. <strong>Continuous CI/CD enforcement:</strong> Returns an immutable binary exit code (<code>0</code> for pass, <code>1</code> for fail) suitable for git pre-commit hooks and GitHub Actions without introducing non-deterministic LLM loops into the pull-request path.
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div id="ratchet-lifecycle" class="band-head">
          <h2 class="band-title">3. Brownfield adoption: 4-phase SHA-256 ratchet lifecycle.</h2>
          <p class="band-lede">
            Mature codebases cannot halt feature delivery for weeks to refactor 70-method structs on Day 1. CodeExplorer hashes structural AST tuples instead of volatile line numbers so teams can turn gates on in CI without fixing old violations first.
          </p>
        </div>

        <div class="panels panels--4">
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Day 0 Debt Discovery</h3>
              <span class="badge badge--accent">Phase 1</span>
            </div>
            <p class="panel-copy">Run ce gate check on unbaselined argo-cd: surfaces all 17 existing errors and 4 auth overrides in 422 ms.</p>
            <code class="panel-code">Exit 1: 21 new findings</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Line-Shift-Immune Freeze</h3>
              <span class="badge badge--win">Phase 2</span>
            </div>
            <p class="panel-copy">Run ce gate ratchet --update to snapshot SHA-256 AST hashes. Inserting +15 header lines in service.go triggers zero false alarms.</p>
            <code class="panel-code">Exit 0: 0 new, 21 tolerated (356 ms)</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Surgical PR Blocker</h3>
              <span class="badge badge--cool">Phase 3</span>
            </div>
            <p class="panel-copy">A PR adds GetClusterSecretCount calling db.NewDB. All 21 legacy items stay quiet; ce gate details --new isolates only the 1 new leak.</p>
            <code class="panel-code">Exit 1: 1 new at service.go:188</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Monotonic Debt Ratchet</h3>
              <span class="badge badge--win">Phase 4</span>
            </div>
            <p class="panel-copy">Refactoring GetCommitMetadata drops SIG-P-B-105 from 2 to 1. ce detects the improvement and locks the lower ceiling permanently.</p>
            <code class="panel-code">Exit 0: ceiling locked 2 to 1 (183 ms)</code>
          </div>
        </div>

        <div class="table-surface" style="margin-top: var(--space-xl);">
          <div class="table-caption-bar">
            <span>Measured 4-Phase Ratchet Progression on argoproj/argo-cd (util/notification/argocd/service.go)</span>
            <span class="measured">Source: results/ratchet-lifecycle-output.txt</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Lifecycle Phase</th>
                <th>Engineering Action</th>
                <th>CLI Command</th>
                <th>Check Time</th>
                <th>Gate State</th>
                <th>CI Exit</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Phase 1: Day 0 Audit</strong></td>
                <td>Initial check on unbaselined repository</td>
                <td><code>ce gate check</code></td>
                <td class="cell-num">422 ms</td>
                <td class="cell-loss">17 errors + 4 warnings (<code>21 new</code>)</td>
                <td class="cell-loss"><code class="badge badge--loss">Exit 1</code></td>
              </tr>
              <tr>
                <td><strong>Phase 2: Baseline + Shift</strong></td>
                <td>Snapshot SHA-256 baselines, then insert <code>+15</code> comment lines above violations in <code>service.go</code></td>
                <td><code>ce gate ratchet --update</code><br><code>ce gate check --scan</code></td>
                <td class="cell-num">356 ms<br><span class="tone-muted">2.63 s w/ scan</span></td>
                <td class="cell-win"><code>0 new</code>, <code>21 tolerated</code> (graph preserved)</td>
                <td class="cell-win"><code class="badge badge--win">Exit 0</code></td>
              </tr>
              <tr>
                <td><strong>Phase 3: PR Regression</strong></td>
                <td>Add <code>GetClusterSecretCount</code> calling <code>db.NewDB</code> in <code>service.go:188</code></td>
                <td><code>ce gate check</code><br><code>ce gate details --new</code></td>
                <td class="cell-num">196 ms<br><span class="tone-muted">3 ms details</span></td>
                <td class="cell-loss"><code>1 new</code> on <code>SIG-P-B-105</code>, <code>21 tolerated</code></td>
                <td class="cell-loss"><code class="badge badge--loss">Exit 1</code></td>
              </tr>
              <tr>
                <td><strong>Phase 4: Debt Paydown</strong></td>
                <td>Refactor <code>GetCommitMetadata</code> to injected <code>ArgoDB</code> (violations drop <code>2</code> &rarr; <code>1</code>)</td>
                <td><code>ce gate check</code><br><code>ce gate ratchet --update</code></td>
                <td class="cell-num">183 ms</td>
                <td class="cell-win"><code>0 new</code>, <code>20 tolerated</code> (ceiling locked <code>2</code> &rarr; <code>1</code>)</td>
                <td class="cell-win"><code class="badge badge--win">Exit 0</code></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);">
          <strong>Why SHA-256 AST fingerprinting survives everyday refactoring:</strong><br>
          Each baseline entry in <code>.codeexplorer/baselines/*.json</code> hashes <code>SHA256(GateId || FilePath || CallerSymbol || TargetSymbol || OccurrenceIndex)</code> rather than source line numbers. When <code>+15</code> lines were inserted at the top of <code>service.go</code> (shifting <code>GetCommitMetadata</code> from line <code>66</code> to <code>81</code> and <code>GetAppDetails</code> from line <code>89</code> to <code>104</code>), the incremental scanner recognized a zero-AST-delta edit in <code>2.63 s</code> and preserved both fingerprints with <code>0</code> false positives.
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">4. Source-verified findings on argoproj/argo-cd.</h2>
          <p class="band-lede">
            Every violation reported across the failing and warning gates was verified directly against the Go and TypeScript source files at commit 6d689fa3.
          </p>
        </div>

        <div class="panels panels--2">
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">3 Reverse Layer Leaks &amp; 2 Direct DB Calls</h3>
              <code class="badge badge--loss">SIG-P-B-101 &amp; 105</code>
            </div>
            <p class="panel-copy">
              SIG-P-B-101 (util/** upward calls):<br>
              1. util/session/sessionmanager.go:288: Parse &rarr; GetProjectRoleFromSubject (server/rbacpolicy)<br>
              2. util/app/discovery/discovery.go:171: cmpSupports &rarr; cmpserver/apiclient<br>
              3. util/webhook/webhook.go:500: storePreviouslyCachedManifests &rarr; NewManifestKey (reposerver/cache)<br><br>
              SIG-P-B-105 (util/notification direct DB instantiation):<br>
              1. util/notification/argocd/service.go:66: GetCommitMetadata &rarr; db.NewDB<br>
              2. util/notification/argocd/service.go:89: GetAppDetails &rarr; db.NewDB
            </p>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">12 Polyglot God Types &amp; 4 Public Auth Overrides</h3>
              <code class="badge badge--accent">SIG-C-R-102 &amp; 106</code>
            </div>
            <p class="panel-copy">
              SIG-C-R-102 (&gt;30 methods across Go &amp; TypeScript):<br>
              SettingsManager (70), ApplicationController (64), client (61), Server (59), nativeGitClient (50), store (44), ArgoCDSettings (38), Angular ApplicationsService (36 in applications-service.ts), db (35), Service (35), syncContext (35), and Cache (34).<br><br>
              SIG-M-B-106 (AuthFuncOverride public gRPC endpoints):<br>
              server/session/session.go:103 (login), server/settings/settings.go:202 (UI login config), server/version/version.go:76 (CLI version), and server/server.go:969 (healthServer probes).
            </p>
          </div>
        </div>
      </div>
    </section>

    <section class="band" style="border-bottom: none;">
      <div class="wrap">
        <div id="yaml-suite" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">
          <div class="prompt-stack-head">
            <h2 class="prompt-stack-title">5. Declarative YAML gates and verbatim CLI transcripts.</h2>
            <span class="prompt-stack-note">Click any drawer below to inspect OpenCypher gate rules, Day 0 output, or the 4-phase ratchet transcript</span>
          </div>
          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--win">Declarative YAML Suite</span>
                <span>gates/architectural-gates.yaml: Complete 6-Gate OpenCypher Rule Definitions</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/06-precommit-architectural-guard/gates/architectural-gates.yaml"># ==============================================================================
# CodeExplorer Semantic Invariant Gates (SIG)
# Architectural Governance Suite for argoproj/argo-cd
# Configured in: .codeexplorer/gates/architectural/
# ==============================================================================

# --- Gate 1: Reverse Layer Inversion Shield ---
id: SIG-P-B-101
category: Architectural
platform: agnostic
scope: Module
dimension: Boundaries
name: "Reverse Layer Inversion Shield (util/ calling higher services)"
severity: Error
description: "Foundational utility packages (util/) must remain self-contained and must never directly call package-level functions in server/, controller/, reposerver/, or cmpserver/."
remediation: "Invert dependency using interfaces defined in util/, callbacks, or move the caller to the owning service layer."
message: "Reverse layer inversion: util function '{caller.name}' ({caller.path}) prohibited from calling '{callee.name}' ({callee.path})"
thresholds:
  warning: 1
  error: 1
query: |
  MATCH (caller:Function)-[r:CALLS]-&gt;(callee:Function)
  WHERE caller.path STARTS WITH 'util/'
    AND NOT caller.path CONTAINS '_test.go'
    AND (callee.path STARTS WITH 'server/' OR callee.path STARTS WITH 'controller/' OR callee.path STARTS WITH 'reposerver/' OR callee.path STARTS WITH 'cmpserver/')
    AND NOT (:Type)-[:HAS_METHOD]-&gt;(callee)
    AND callee.name &gt;= 'A' AND callee.name &lt;= 'Z'
  RETURN caller, r, callee AS target

---
# --- Gate 2: God Type Complexity Limit ---
id: SIG-C-R-102
category: Architectural
platform: agnostic
scope: Class
dimension: Resources
name: "God Type Complexity Limit (&gt;30 methods)"
severity: Error
description: "Production structs and classes must declare at most 30 methods. Monolithic God Types degrade maintainability and violate the Single Responsibility Principle."
remediation: "Decompose the struct or class into smaller, cohesive domain components or service delegates."
thresholds:
  warning: 1
  error: 1
query: |
  MATCH (t:Type)
  WHERE coalesce(t.kind, 'class') IN ['class', 'struct']
    AND NOT (coalesce(t.path, t.file_path, '') CONTAINS 'Test' OR coalesce(t.path, t.file_path, '') CONTAINS 'test' OR coalesce(t.path, t.file_path, '') CONTAINS 'mock')
  OPTIONAL MATCH (t)-[:HAS_METHOD]-&gt;(m:Function)
  WITH t, count(m) AS method_count
  WHERE method_count &gt; 30
  RETURN 
    coalesce(t.path, t.file_path) AS file,
    coalesce(t.start_line, 1) AS line,
    coalesce(t.start_col, 1) AS column,
    t.name AS symbol,
    'GodType' AS target,
    'God type detected: ' + t.name + ' has ' + toString(method_count) + ' methods (max allowed: 30)' AS message

---
# --- Gate 3: GitOps Engine Layer Isolation ---
id: SIG-P-B-103
category: Architectural
platform: agnostic
scope: Module
dimension: Boundaries
name: "GitOps Engine Layer Isolation (Zero Host Calls)"
severity: Error
description: "The embedded gitops-engine/ subsystem must remain strictly standalone and never call upward into Argo CD host services or util/ packages."
remediation: "Keep gitops-engine self-contained; pass Argo CD host behavior via interfaces defined inside gitops-engine/pkg."
message: "Engine boundary leak: gitops-engine function '{caller.name}' ({caller.path}) prohibited from calling host symbol '{callee.name}' ({callee.path})"
thresholds:
  warning: 1
  error: 1
query: |
  MATCH (caller:Function)-[r:CALLS]-&gt;(callee:Function)
  WHERE caller.path STARTS WITH 'gitops-engine/'
    AND NOT caller.path CONTAINS '_test.go'
    AND (callee.path STARTS WITH 'server/' OR callee.path STARTS WITH 'controller/' OR callee.path STARTS WITH 'reposerver/' OR callee.path STARTS WITH 'cmpserver/' OR callee.path STARTS WITH 'applicationset/' OR callee.path STARTS WITH 'util/')
    AND NOT (:Type)-[:HAS_METHOD]-&gt;(callee)
    AND callee.name &gt;= 'A' AND callee.name &lt;= 'Z'
  RETURN caller, r, callee AS target

---
# --- Gate 4: Storage Abstraction Boundary ---
id: SIG-P-B-104
category: Architectural
platform: agnostic
scope: Module
dimension: Boundaries
name: "Storage Abstraction Boundary (Raw Secrets Access Shield)"
severity: Error
description: "Raw Kubernetes secret storage backends (repository_secrets.go) must be encapsulated strictly within util/db and never called directly from other packages."
remediation: "Access repository storage exclusively via the high-level ArgoDB interface."
thresholds:
  warning: 1
  error: 1
query: |
  MATCH (caller:Function)-[r:CALLS]-&gt;(callee:Function)
  WHERE callee.path CONTAINS 'repository_secrets.go'
    AND NOT caller.path CONTAINS 'util/db'
  RETURN caller, r, callee AS target

---
# --- Gate 5: Notification Subsystem DB Decoupling ---
id: SIG-P-B-105
category: Architectural
platform: agnostic
scope: Module
dimension: Boundaries
name: "Notification Subsystem DB Decoupling"
severity: Error
description: "Notification utilities in util/notification must not construct standalone database instances or call db.NewDB directly; DB instances must be injected."
remediation: "Inject ArgoDB as a dependency rather than creating a new DB instance inside the notification service."
thresholds:
  warning: 1
  error: 1
query: |
  MATCH (caller:Function)-[r:CALLS]-&gt;(callee:Function)
  WHERE caller.path CONTAINS 'util/notification'
    AND callee.name IN ['NewDB', 'NewSecretDB']
  RETURN caller, r, callee AS target

---
# --- Gate 6: Public Authentication Bypass Audit ---
id: SIG-M-B-106
category: Architectural
platform: agnostic
scope: Method
dimension: Boundaries
name: "Public Authentication Bypass Audit (AuthFuncOverride)"
severity: Warning
description: "All server endpoints implementing AuthFuncOverride bypass standard JWT authentication and must be explicitly audited and documented as intentionally public."
remediation: "Verify that each AuthFuncOverride implementation is intended to be public, or enforce standard JWT authentication."
thresholds:
  warning: 1
  error: 10
query: |
  MATCH (fn:Function)
  WHERE fn.name = 'AuthFuncOverride' AND fn.path CONTAINS 'server/'
  RETURN 
    fn.path AS file,
    coalesce(fn.start_line, 1) AS line,
    coalesce(fn.start_col, 1) AS column,
    fn.name AS symbol,
    'AuthBypass' AS target,
    'Public authentication override detected at ' + fn.path + ':' + toString(coalesce(fn.start_line, 1)) AS message</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--cool">CLI Execution</span>
                <span>results/ce-gate-output.txt: Verbatim ce gate check &amp; ce gate details output on argo-cd</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/06-precommit-architectural-guard/results/ce-gate-output.txt">$ ce gate check --dir ./repos/argo-cd

--- Semantic Invariant Gates Evaluation ---

STATUS         ID              CATEGORY        PLATFORM   SCOPE     VIOLATIONS      NAME
---------------------------------------------------------------------------------------------------------
[FAIL]         SIG-C-R-102     Architectural   agnostic   Class     12 new          God Type Complexity Limit (&gt;30 methods)
[WARN]         SIG-M-B-106     Architectural   agnostic   Method    4 new           Public Authentication Bypass Audit (AuthFuncOverride)
[FAIL]         SIG-P-B-101     Architectural   agnostic   Module    3 new           Reverse Layer Inversion Shield (util/ calling higher services)
[PASS]         SIG-P-B-103     Architectural   agnostic   Module    0               GitOps Engine Layer Isolation (Zero Host Calls)
[PASS]         SIG-P-B-104     Architectural   agnostic   Module    0               Storage Abstraction Boundary (Raw Secrets Access Shield)
[FAIL]         SIG-P-B-105     Architectural   agnostic   Module    2 new           Notification Subsystem DB Decoupling
---------------------------------------------------------------------------------------------------------
Total: 6 gates | Passed: 2 | Warnings: 1 | Failed: 3 | Duration: 422ms

Tip: Run 'ce gate details' to view failure details and remediation.
Exit code: 1

$ ce gate details --dir ./repos/argo-cd --all

--- Semantic Invariant Gate Details ---

[FAIL] SIG-C-R-102 [Architectural/agnostic] - God Type Complexity Limit (&gt;30 methods) (206ms)
  Rule: Production structs and classes must declare at most 30 methods. Monolithic God Types degrade maintainability and violate the Single Responsibility Principle.
  Remediation: Decompose the struct or class into smaller, cohesive domain components or service delegates.

  Violations (12 new, 0 tolerated) | Showing 12 of 12:
  STATUS | LOCATION                                                | SYMBOL                | TARGET / CALL
  -------+---------------------------------------------------------+-----------------------+--------------
  [NEW]  | controller/appcontroller.go:120:6                       | ApplicationController | GodType
  [NEW]  | gitops-engine/pkg/cache/cluster.go:243:6                | store                 | GodType
  [NEW]  | gitops-engine/pkg/sync/sync_context.go:364:1            | syncContext           | GodType
  [NEW]  | pkg/apiclient/apiclient.go:164:6                        | client                | GodType
  [NEW]  | reposerver/cache/cache.go:32:6                          | Cache                 | GodType
  [NEW]  | reposerver/repository/repository.go:93:6                | Service               | GodType
  [NEW]  | server/application/application.go:103:6                 | Server                | GodType
  [NEW]  | ui/src/app/shared/services/applications-service.ts:57:8 | ApplicationsService   | GodType
  [NEW]  | util/db/repository.go:58:1                              | db                    | GodType
  [NEW]  | util/git/client.go:202:6                                | nativeGitClient       | GodType
  [NEW]  | util/settings/settings.go:87:6                          | ArgoCDSettings        | GodType
  [NEW]  | util/settings/settings.go:632:6                         | SettingsManager       | GodType
  -------+---------------------------------------------------------+-----------------------+--------------

[WARN] SIG-M-B-106 [Architectural/agnostic] - Public Authentication Bypass Audit (AuthFuncOverride) (8ms)
  Rule: All server endpoints implementing AuthFuncOverride bypass standard JWT authentication and must be explicitly audited and documented as intentionally public.
  Remediation: Verify that each AuthFuncOverride implementation is intended to be public, or enforce standard JWT authentication.

  Violations (4 new, 0 tolerated) | Showing 4 of 4:
  STATUS | LOCATION                          | SYMBOL           | TARGET / CALL
  -------+-----------------------------------+------------------+--------------
  [NEW]  | server/session/session.go:103:1   | AuthFuncOverride | AuthBypass
  [NEW]  | server/settings/settings.go:202:1 | AuthFuncOverride | AuthBypass
  [NEW]  | server/version/version.go:76:1    | AuthFuncOverride | AuthBypass
  [NEW]  | server/server.go:969:1            | AuthFuncOverride | AuthBypass
  -------+-----------------------------------+------------------+--------------

[FAIL] SIG-P-B-101 [Architectural/agnostic] - Reverse Layer Inversion Shield (util/ calling higher services) (58ms)
  Rule: Foundational utility packages (util/) must remain self-contained and must never directly call package-level functions in server/, controller/, reposerver/, or cmpserver/.
  Remediation: Invert dependency using interfaces defined in util/, callbacks, or move the caller to the owning service layer.

  Violations (3 new, 0 tolerated) | Showing 3 of 3:
  STATUS | LOCATION                               | SYMBOL                         | TARGET / CALL
  -------+----------------------------------------+--------------------------------+-----------------------------------
  [NEW]  | util/session/sessionmanager.go:288:27  | Parse                          | GetProjectRoleFromSubject
  [NEW]  | util/app/discovery/discovery.go:171:18 | cmpSupports                    | NewConfigManagementPluginClientSet
  [NEW]  | util/webhook/webhook.go:500:20         | storePreviouslyCachedManifests | NewManifestKey
  -------+----------------------------------------+--------------------------------+-----------------------------------

[FAIL] SIG-P-B-105 [Architectural/agnostic] - Notification Subsystem DB Decoupling (2ms)
  Rule: Notification utilities in util/notification must not construct standalone database instances or call db.NewDB directly; DB instances must be injected.
  Remediation: Inject ArgoDB as a dependency rather than creating a new DB instance inside the notification service.

  Violations (2 new, 0 tolerated) | Showing 2 of 2:
  STATUS | LOCATION                                  | SYMBOL            | TARGET / CALL
  -------+-------------------------------------------+-------------------+--------------
  [NEW]  | util/notification/argocd/service.go:66:14 | GetCommitMetadata | NewDB
  [NEW]  | util/notification/argocd/service.go:89:14 | GetAppDetails     | NewDB
  -------+-------------------------------------------+-------------------+--------------</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Ratchet Lifecycle</span>
                <span>results/ratchet-lifecycle-output.txt: Verbatim Phases 2 to 4 CLI &amp; SHA-256 Baseline Transcript</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/06-precommit-architectural-guard/results/ratchet-lifecycle-output.txt"># ==============================================================================
# Phase 2A: Freeze Legacy Debt into SHA-256 Baselines (Day 1 CI Adoption)
# ==============================================================================
$ ce gate ratchet --update --dir ./repos/argo-cd
Successfully ratcheted down and updated baselines for 6 gate(s).

# Generated snapshot: .codeexplorer/baselines/SIG-P-B-105.json
{
  "gateId": "SIG-P-B-105",
  "allowedViolations": 2,
  "entries": [
    {
      "fingerprint": "45d53d82dfc5293dcd8391c8a4a541653fd994b1199d68ebc3df085a21f61879",
      "file": "util/notification/argocd/service.go",
      "line": 66,
      "column": 14,
      "symbol": "GetCommitMetadata",
      "target": "NewDB",
      "acknowledgedAt": "2026-11-11T13:31:15.0720811+00:00",
      "reason": "Baseline snapshot captured at 2026-11-11 13:31:15"
    },
    {
      "fingerprint": "30f65d321503411ce4332641c5d5503b044daa3c85bea47a9bd5598474b9e8b9",
      "file": "util/notification/argocd/service.go",
      "line": 89,
      "column": 14,
      "symbol": "GetAppDetails",
      "target": "NewDB",
      "acknowledgedAt": "2026-11-11T13:31:15.0720811+00:00",
      "reason": "Baseline snapshot captured at 2026-11-11 13:31:15"
    }
  ]
}

# ==============================================================================
# Phase 2B: Line-Shift Immunity (+15 comment lines inserted at top of service.go,
# shifting GetCommitMetadata 66 -&gt; 81 and GetAppDetails 89 -&gt; 104)
# ==============================================================================
$ ce gate check --scan --dir ./repos/argo-cd
[Incremental] Changes detected: 0 added, 1 modified, 0 deleted.
[Incremental:Decision] Logic-only change in 'util/notification/argocd/service.go' (0 logic edits, 0 line shifts, 0 graph modifications). Preserving graph.

--- Semantic Invariant Gates Evaluation ---

STATUS         ID              CATEGORY        PLATFORM   SCOPE     VIOLATIONS      NAME
---------------------------------------------------------------------------------------------------------
[TOLERATED]    SIG-C-R-102     Architectural   agnostic   Class     12 tol          God Type Complexity Limit (&gt;30 methods)
[TOLERATED]    SIG-M-B-106     Architectural   agnostic   Method    4 tol           Public Authentication Bypass Audit (AuthFuncOverride)
[TOLERATED]    SIG-P-B-101     Architectural   agnostic   Module    3 tol           Reverse Layer Inversion Shield (util/ calling higher services)
[PASS]         SIG-P-B-103     Architectural   agnostic   Module    0               GitOps Engine Layer Isolation (Zero Host Calls)
[PASS]         SIG-P-B-104     Architectural   agnostic   Module    0               Storage Abstraction Boundary (Raw Secrets Access Shield)
[TOLERATED]    SIG-P-B-105     Architectural   agnostic   Module    2 tol           Notification Subsystem DB Decoupling
---------------------------------------------------------------------------------------------------------
Total: 6 gates | Passed: 6 | Warnings: 0 | Failed: 0 | Duration: 356ms

All architectural semantic gates passed successfully.
Exit code: 0

# ==============================================================================
# Phase 3: PR Regression Catch (New method GetClusterSecretCount calls db.NewDB)
# ==============================================================================
$ ce gate check --dir ./repos/argo-cd

--- Semantic Invariant Gates Evaluation ---

STATUS         ID              CATEGORY        PLATFORM   SCOPE     VIOLATIONS      NAME
---------------------------------------------------------------------------------------------------------
[TOLERATED]    SIG-C-R-102     Architectural   agnostic   Class     12 tol          God Type Complexity Limit (&gt;30 methods)
[TOLERATED]    SIG-M-B-106     Architectural   agnostic   Method    4 tol           Public Authentication Bypass Audit (AuthFuncOverride)
[TOLERATED]    SIG-P-B-101     Architectural   agnostic   Module    3 tol           Reverse Layer Inversion Shield (util/ calling higher services)
[PASS]         SIG-P-B-103     Architectural   agnostic   Module    0               GitOps Engine Layer Isolation (Zero Host Calls)
[PASS]         SIG-P-B-104     Architectural   agnostic   Module    0               Storage Abstraction Boundary (Raw Secrets Access Shield)
[FAIL]         SIG-P-B-105     Architectural   agnostic   Module    1 new           Notification Subsystem DB Decoupling
---------------------------------------------------------------------------------------------------------
Total: 6 gates | Passed: 5 | Warnings: 0 | Failed: 1 | Duration: 196ms

Tip: Run 'ce gate details' to view failure details and remediation.
Exit code: 1

$ ce gate details --new --dir ./repos/argo-cd

--- Semantic Invariant Gate Details ---

[FAIL] SIG-P-B-105 [Architectural/agnostic] - Notification Subsystem DB Decoupling (3ms)
  Rule: Notification utilities in util/notification must not construct standalone database instances or call db.NewDB directly; DB instances must be injected.
  Remediation: Inject ArgoDB as a dependency rather than creating a new DB instance inside the notification service.

  Violations (1 new, 0 tolerated) | Showing 1 of 1:
  STATUS | LOCATION                                   | SYMBOL                | TARGET / CALL
  -------+--------------------------------------------+-----------------------+--------------
  [NEW]  | util/notification/argocd/service.go:188:14 | GetClusterSecretCount | NewDB
  -------+--------------------------------------------+-----------------------+--------------

# ==============================================================================
# Phase 4: Monotonic Ratchet Tightening (GetCommitMetadata refactored: 2 -&gt; 1)
# ==============================================================================
$ ce gate check --dir ./repos/argo-cd

--- Semantic Invariant Gates Evaluation ---

STATUS         ID              CATEGORY        PLATFORM   SCOPE     VIOLATIONS      NAME
---------------------------------------------------------------------------------------------------------
[TOLERATED]    SIG-C-R-102     Architectural   agnostic   Class     12 tol          God Type Complexity Limit (&gt;30 methods)
[TOLERATED]    SIG-M-B-106     Architectural   agnostic   Method    4 tol           Public Authentication Bypass Audit (AuthFuncOverride)
[TOLERATED]    SIG-P-B-101     Architectural   agnostic   Module    3 tol           Reverse Layer Inversion Shield (util/ calling higher services)
[PASS]         SIG-P-B-103     Architectural   agnostic   Module    0               GitOps Engine Layer Isolation (Zero Host Calls)
[PASS]         SIG-P-B-104     Architectural   agnostic   Module    0               Storage Abstraction Boundary (Raw Secrets Access Shield)
[TOLERATED]    SIG-P-B-105     Architectural   agnostic   Module    1 tol           Notification Subsystem DB Decoupling
---------------------------------------------------------------------------------------------------------
Total: 6 gates | Passed: 6 | Warnings: 0 | Failed: 0 | Duration: 183ms

1 gate(s) have fewer violations than allowed by baseline.
   Run 'ce gate ratchet --update' to lock down resolved technical debt.
All architectural semantic gates passed successfully.
Exit code: 0

$ ce gate ratchet --update --dir ./repos/argo-cd
Successfully ratcheted down and updated baselines for 6 gate(s).</pre>
            </div>
          </details>
        </div>
      </div>
    </section>`},"/privacy":{routePath:"/privacy",pageKind:"privacy",title:"Privacy Policy | CodeExplorer",description:"CodeExplorer runs locally on your machine and CI runners with no telemetry, cloud dependencies, or source code uploads.",canonical:"https://code-explorer.app/privacy",ogUrl:"https://code-explorer.app/privacy",ogTitle:"Privacy Policy | CodeExplorer",ogDescription:"CodeExplorer runs locally on your machine and CI runners with no telemetry, cloud dependencies, or source code uploads.",mainHtml:`<div class="privacy-wrap">
      <h1>Privacy Policy</h1>
      <div class="meta">Effective Date: October 29, 2026 · Standalone Open Source Software</div>

      <div class="card">
        <h2>1. Summary</h2>
        <p>
          CodeExplorer (the <code>ce</code> CLI and the <code>code-explorer-vscode</code> extension) runs locally on your machine and CI runners without sending source code or telemetry to external servers.
        </p>
        <ul>
          <li>CodeExplorer does not collect, record, or transmit usage statistics, crash dumps, or telemetry.</li>
          <li>Your source code, syntax trees, git metadata, and architecture graphs stay on your machine or CI runner.</li>
          <li>Parsed graph data is stored in a local SQLite file (<code>.codeexplorer/graph.db</code>) inside your workspace.</li>
          <li>No account, registration, or sign-in is required.</li>
        </ul>
      </div>

      <div class="card">
        <h2>2. Website (code-explorer.app)</h2>
        <p>
          The website <code>code-explorer.app</code> is a static documentation and download site.
        </p>
        <p>
          Standard web server access logs (client IP address, user-agent, requested URL, and timestamp) are retained only for operational security, DDoS mitigation, and aggregate request counts. No advertising cookies or third-party tracking scripts are loaded.
        </p>
      </div>

      <div class="card">
        <h2>3. Local Execution</h2>
        <p>
          When you run <code>ce scan</code>, <code>ce map</code>, <code>ce context</code>, or <code>ce gate</code>, or use the VS Code extension or MCP server:
        </p>
        <ul>
          <li>Parsing runs locally on your CPU using Tree-sitter, SQL, and HCL parsers.</li>
          <li>No external network requests are made during scanning, querying, or gate checks.</li>
          <li>MCP sessions communicate with your local coding client over standard input/output (<code>stdio</code>).</li>
        </ul>
      </div>

      <div class="card">
        <h2>4. Open Source License</h2>
        <p>
          CodeExplorer is open-source software released under the <a href="https://github.com/vmikhailov/code-explorer/blob/main/LICENSE" target="_blank" rel="noopener" style="color: var(--accent);">MIT License</a>. Source code is available on <a href="https://github.com/vmikhailov/code-explorer" target="_blank" rel="noopener" style="color: var(--accent);">GitHub</a>.
        </p>
      </div>

      <div class="card">
        <h2>5. Contact</h2>
        <p>
          For questions about this privacy policy or security practices, open an issue at <a href="https://github.com/vmikhailov/code-explorer/issues" target="_blank" rel="noopener" style="color: var(--accent);">github.com/vmikhailov/code-explorer/issues</a>.
        </p>
      </div>
    </div>`},"/benchmarks/07-agent-feature-ce-vs-cbm-on-twenty":{routePath:"/benchmarks/07-agent-feature-ce-vs-cbm-on-twenty",pageKind:"benchmark-case",title:"Case Study 07: Real-Life Feature Coding & Architectural Guardrails on twentyhq/twenty (ce vs CBM)",description:"Autonomous feature implementation benchmark on twentyhq/twenty (46 packages, 24,527 files): CodeExplorer Agent (12m 59s wall / 10m 3s initial code + tests, 43 turns, pre-configured gate caught copied legacy debt & agent refactored in 1 turn, 4/4 gates PASS) vs codebase-memory-mcp Agent (20m 25s wall / 15m 17s initial code + tests, 54 turns, 1 new SIG-TWENTY-003 cross-module table coupling).",descriptionMetaBind:"cs07.meta_desc",canonical:"https://code-explorer.app/benchmarks/07-agent-feature-ce-vs-cbm-on-twenty",ogUrl:"https://code-explorer.app/benchmarks/07-agent-feature-ce-vs-cbm-on-twenty",ogTitle:"Case Study 07: Real-Life Feature Coding & Architectural Guardrails on twentyhq/twenty (ce vs CBM)",ogDescription:"Autonomous feature implementation benchmark on twentyhq/twenty (46 packages, 24,527 files): CodeExplorer Agent (12m 59s wall / 10m 3s initial code + tests, 43 turns, pre-configured gate caught copied legacy debt & agent refactored in 1 turn, 4/4 gates PASS) vs codebase-memory-mcp Agent (20m 25s wall / 15m 17s initial code + tests, 54 turns, 1 new SIG-TWENTY-003 cross-module table coupling).",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 07: Real-Life Feature Coding on twentyhq/twenty</span>
          </div>
          <span class="measured" data-bind="cs07.crumb_scale">46 packages, 24,527 files, 2.49M LOC</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 07: Real-life feature implementation &amp; self-healing on twentyhq/twenty (ce vs CBM).</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Two identical Gemini 3.8 Flash agents implemented a cross-cutting suspendWorkspaceAdminPanel feature and unit test suite on twentyhq/twenty under 100% symmetric system prompts with zero rule hints. While researching how emailing domains are cleaned up, both agents found a legacy shortcut in workspace.service.ts:989 and copied direct cross-module ORM table access into AdminPanelModule. Native oxlint and CBM reported 0 errors; ce gate check caught the leak and the CodeExplorer agent refactored in 1 turn.
            </p>
            <div class="hero-controls">
              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/07-agent-feature-ce-vs-cbm-on-twenty/results" target="_blank" rel="noopener" class="btn btn--primary">Inspect Patches &amp; metrics.json</a>
              <a href="#prompts" class="btn">Inspect Tested Prompts</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-ok" data-bind="cs07.card_speedup">~1.6×</span>
            <h2 class="panel-title">Faster End-to-End</h2>
            <p class="panel-copy" data-bind-html="cs07.card_speed_copy">CodeExplorer finished in 12m 59s (incl. scan, gate check &amp; 1-turn refactor) vs 20m 25s for CBM.</p>
            <code class="panel-code" data-bind="cs07.card_turns_footer">43 turns vs 54 turns (31 vs 43 file reads)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs07.card_init_speed">~1.5×</span>
            <h2 class="panel-title">Faster Initial Draft</h2>
            <p class="panel-copy" data-bind-html="cs07.card_init_copy">Initial feature + unit tests took 10m 3s (36 turns) with ce vs 15m 17s (48 turns) with CBM.</p>
            <code class="panel-code" data-bind="cs07.card_init_footer">603 s vs 917 s (-5m 14s before gates)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs07.card_gates">4/4 vs 3/4</span>
            <h2 class="panel-title">Architectural Gates</h2>
            <p class="panel-copy" data-bind-html="cs07.card_gates_copy">Pre-configured gate SIG-TWENTY-003 caught the copied legacy table coupling ([TOL] in WorkspaceService, [NEW] in AdminPanelBillingService); oxlint &amp; CBM did not.</p>
            <code class="panel-code" data-bind="cs07.card_gates_footer">0 new violations vs 1 new cross-module table coupling</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs07.card_heal">1 Turn vs 0</span>
            <h2 class="panel-title">Autonomous Self-Heal</h2>
            <p class="panel-copy" data-bind-html="cs07.card_heal_copy">Both agents initially copied the legacy shortcut from workspace.service.ts:989. ce gate check flagged the new copy and ce refactored in 1 editing turn (20s).</p>
            <code class="panel-code" data-bind="cs07.card_heal_footer">CBM index_repository (300.6s) reported 0 issues</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs07.participants_html">Tested participants:<br>
          1. CodeExplorer (ce): v1.87.2 (.NET 11, SQLite + OpenCypher + Incremental ce scan + Semantic Invariant Gates, 464,794 nodes, 1,113,891 edges)<br>
          2. codebase-memory-mcp (CBM): v0.12.1 (win32-x64 Go + SQLite binary, index_repository + query_graph + trace_path)<br>
          3. LLM Model: Gemini 3.8 Flash (100% word-for-word symmetric system prompts with zero rule hints)<br>
          4. Target Repository: twentyhq/twenty (commit 12e8fae4, 46 packages, 24,527 files, 2.49M LOC)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Execution telemetry: two independent wins.</h2>
          <p class="band-lede">
            All metrics are extracted directly from the runtime logs (transcript_full.jsonl) of conversations <code data-bind="cs07.conv_ce">385986ab</code> (ce_twenty_feature_agent) and <code data-bind="cs07.conv_cbm">775b7008</code> (cbm_twenty_feature_agent). Even before running a single architectural gate, CodeExplorer wrote the feature and unit test suite 1.52× faster by resolving NestJS constructor Dependency Injection callers that codebase-memory-mcp missed.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>End-to-End Feature Implementation Telemetry on twentyhq/twenty (24,527 files, 100% Symmetric Prompts)</span>
            <span class="measured">Source: benchmarks/07-agent-feature-ce-vs-cbm-on-twenty/results/metrics.json</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Metric</th>
                <th>CBM Agent (cbm_twenty_feature_agent)</th>
                <th>CodeExplorer Agent (ce_twenty_feature_agent)</th>
                <th>Delta</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Total Wall-Clock Time (<code>send_message</code>)</td>
                <td class="cell-loss" data-bind="cs07.tbl_time_cbm">1,225 s (20m 25s)</td>
                <td class="cell-win" data-bind="cs07.tbl_time_ce">779 s (12m 59s)</td>
                <td class="cell-win" data-bind="cs07.tbl_time_delta">1.57× faster (-7m 26s)</td>
              </tr>
              <tr>
                <td>Phase 1: Time to Initial Code + Unit Tests</td>
                <td class="cell-loss" data-bind="cs07.tbl_init_cbm">917 s (15m 17s, 48 turns)</td>
                <td class="cell-win" data-bind="cs07.tbl_init_ce">603 s (10m 3s, 36 turns)</td>
                <td class="cell-win" data-bind="cs07.tbl_init_delta">1.52× faster (-5m 14s)</td>
              </tr>
              <tr>
                <td>Phase 2: Post-Edit Re-Index &amp; Verification</td>
                <td class="cell-loss">308 s (5m 8s; 300.6s full rebuild)</td>
                <td class="cell-win">176 s (2m 56s; 2 incremental scans + 1-turn fix)</td>
                <td class="cell-win">1.75× faster verification + self-repair</td>
              </tr>
              <tr>
                <td>LLM Reasoning Turns &amp; Steps</td>
                <td class="cell-loss" data-bind="cs07.tbl_turns_cbm">54 turns (132 steps)</td>
                <td class="cell-win" data-bind="cs07.tbl_turns_ce">43 turns (118 steps)</td>
                <td class="cell-win" data-bind="cs07.tbl_turns_delta">1.26× fewer turns (1.12× fewer steps)</td>
              </tr>
              <tr>
                <td>Total Tool Calls &amp; File Reads</td>
                <td class="cell-loss" data-bind-html="cs07.tbl_calls_cbm">75 (20 <code>cbm</code>, 5 <code>rg</code>, 43 <code>read_file</code>, 5 edits)</td>
                <td class="cell-win" data-bind-html="cs07.tbl_calls_ce">71 (20 <code>ce</code>, 8 <code>rg</code>, 31 <code>read_file</code>, 12 edits)</td>
                <td class="cell-win" data-bind="cs07.tbl_calls_delta">1.06× fewer calls (1.39× fewer file reads)</td>
              </tr>
              <tr>
                <td>Raw Tool Output Ingested</td>
                <td class="cell-loss" data-bind="cs07.tbl_payload_cbm">399,803 chars (~99,951 est. tokens)</td>
                <td class="cell-win" data-bind="cs07.tbl_payload_ce">399,749 chars (~99,937 est. tokens)</td>
                <td class="cell-win" data-bind="cs07.tbl_payload_delta">1.00× (31 vs 43 file reads)</td>
              </tr>
              <tr>
                <td>Cumulative Input Tokens (Across All Turns)</td>
                <td class="cell-loss" data-bind="cs07.tbl_in_cbm">4,230,784 est. tokens (chars/4 across 54 turns incl. cache)</td>
                <td class="cell-win" data-bind="cs07.tbl_in_ce">3,387,918 est. tokens (chars/4 across 43 turns incl. cache)</td>
                <td class="cell-win" data-bind="cs07.tbl_in_delta">1.25× fewer input tokens (-843K)</td>
              </tr>
              <tr>
                <td>Native <code>twenty</code> Linter (<code>npx oxlint -c .oxlintrc.json</code>)</td>
                <td class="cell-loss">0 errors, 2 warnings (Exit 0)</td>
                <td class="cell-win">0 errors, 2 warnings (Exit 0)</td>
                <td>Native AST linter missed cross-module ORM leak</td>
              </tr>
              <tr class="row-total">
                <td>Architectural Invariant Gates (<code>4</code> Rules)</td>
                <td class="cell-loss" data-bind-html="cs07.tbl_gates_cbm">3 / 4 passed (1 new in <code>SIG-TWENTY-003</code>; evaluated post-session)</td>
                <td class="cell-win" data-bind="cs07.tbl_gates_ce">4 / 4 passed (0 new violations)</td>
                <td class="cell-win" data-bind-html="cs07.tbl_gates_delta">Pre-configured gate caught copied debt; refactored in 1 turn</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Who did what and why: inside the two agent trajectories.</h2>
          <p class="band-lede">
            Instead of treating the agents as black boxes, we traced every tool call in both transcripts to see how their graph tools shaped their reasoning, why both walked into the same brownfield trap at workspace.service.ts:989, and why neither CBM nor twenty's native oxlint caught it.
          </p>
        </div>

        <div class="panels panels--3" style="margin-top: var(--space-lg);">
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Act I: NestJS DI Navigation</h3>
              <span class="badge badge--accent">0s to 603s vs 917s</span>
            </div>
            <p class="panel-copy">
              To check whether WorkspaceService.suspendWorkspace already revokes Google/Microsoft webhooks, ce ran ce callers "suspendWorkspace" at Step 18 (+78s), resolved the NestJS constructor DI callers, and saw it emits WORKSPACE_SUSPENDED_EVENT. In CBM, trace_path returned callers_total: 0 at Step 64 (+424s), forcing 43 file reads via rg.
            </p>
            <code class="panel-code">ce: 10m 03s (36 turns) vs CBM: 15m 17s (48 turns)</code>
          </div>

          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Act II: The Legacy Trap</h3>
              <span class="badge badge--loss">workspace.service.ts:989</span>
            </div>
            <p class="panel-copy">
              Requirement 4 asked only to trigger emailing domain cleanup via EmailingDomainWorkspaceCleanupJob. Both agents found the sole existing caller in workspace.service.ts:989, which queries this.coreDataSource.getRepository(EmailingDomainEntity).find(...) directly. Both copied that direct table read into AdminPanelBillingService using @InjectWorkspaceScopedRepository(EmailingDomainEntity).
            </p>
            <code class="panel-code">Both drafts failed SIG-TWENTY-003</code>
          </div>

          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Act III: 300s Blind Rebuild vs 28s Fix</h3>
              <span class="badge badge--win">Turns 37 to 43 vs 49 to 54</span>
            </div>
            <p class="panel-copy">
              In post-edit verification, CBM ran a full index_repository rebuild (300.6s) and reported 0 issues. CodeExplorer ran incremental ce scan (~28s) + ce gate check, flagged suspendWorkspace &rarr; emailingDomain, and refactored in 1 turn (Step 105) to call EmailingDomainService.getEmailingDomains(workspace).
            </p>
            <code class="panel-code">PASS: 4/4 gates (12m 59s total wall time)</code>
          </div>
        </div>

        <div class="sub-block">
          <h3 class="sub-title">Act II Verbatim Code: The Legacy Shortcut Both Agents Found in workspace.service.ts:989</h3>
          <pre class="code-block">// packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts (commit 12e8fae4)
// Line 140 (suppresses twenty's file-naming convention rule):
// oxlint-disable-next-line twenty/inject-workspace-repository
export class WorkspaceService {
  ...
  // Lines 989 to 1005 (the only existing caller of EmailingDomainWorkspaceCleanupJob in twenty-server):
  const emailingDomains = await this.coreDataSource
    .getRepository(EmailingDomainEntity)
    .find({ where: { workspaceId: workspace.id } });

  await this.deleteCascadeMessageQueueService.add&lt;EmailingDomainWorkspaceCleanupJobData&gt;(
    EmailingDomainWorkspaceCleanupJob.name,
    {
      workspaceId: workspace.id,
      domains: emailingDomains.map((emailingDomain) =&gt; emailingDomain.domain),
    },
    { workspaceId: workspace.id, isWorkspaceJob: true },
  );</pre>
        </div>

        <div class="sub-block">
          <h3 class="sub-title">Why twenty’s Native Linter (oxlint) Passed CBM’s Broken Patch With 0 Errors</h3>
          <p class="panel-copy" style="margin-bottom: var(--space-sm);">
            We tested results/cbm-agent.patch directly against twenty-server’s native oxlint configuration (packages/twenty-server/.oxlintrc.json with packages/twenty-oxlint-rules). Because twenty/prefer-workspace-scoped-repository only checks that @InjectWorkspaceScopedRepository is used instead of raw @InjectRepository (with zero concept of module ownership), and twenty/enforce-module-boundaries only checks top-level Nx packages (if (sourcePackage === targetPackage) return true; at line 54), native oxlint passes cbm-agent.patch with 0 errors:
          </p>
          <pre class="code-block">$ npx oxlint -c .oxlintrc.json     src/engine/core-modules/admin-panel/admin-panel.module.ts     src/engine/core-modules/admin-panel/admin-panel.resolver.ts     src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts     src/engine/core-modules/admin-panel/services/__tests__/admin-panel-billing.service.spec.ts

  ! twenty(inject-workspace-repository): Class name should end with 'WorkspaceService'.
     ,-[src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:46:39]
  46 | ,-&gt; export class AdminPanelBillingService {
...
  61 | |       private readonly workspaceService: WorkspaceService,
  62 | |       @InjectWorkspaceScopedRepository(EmailingDomainEntity)
  63 | |       private readonly emailingDomainRepository: WorkspaceScopedRepository&lt;EmailingDomainEntity&gt;,

  ! twenty(inject-workspace-repository): File name should end with '.workspace-service.ts'.
     ,-[src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:46:39]

Found 2 warnings and 0 errors.
Finished in 1.1s on 4 files with 24 rules using 12 threads.
EXIT CODE: 0</pre>
        </div>

        <div class="table-surface" style="margin-top: var(--space-xl);">
          <div class="table-caption-bar">
            <span>The 4 Semantic Invariant Gates Evaluated on twenty-server (.codeexplorer/gates/architectural/*.yaml)</span>
            <span class="measured">Zero-Hint Architectural Verification</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Gate ID &amp; Scope</th>
                <th>Architectural Invariant Enforced</th>
                <th>CBM Final Patch</th>
                <th>CodeExplorer Final Patch</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>SIG-TWENTY-001</code><br><strong>GraphQL Resolver &amp; Controller Transport Purity</strong></td>
                <td>Resolvers (<code>*.resolver.ts</code>) and Controllers (<code>*.controller.ts</code>) in <code>admin-panel</code>, <code>workspace</code>, and <code>billing</code> must never directly mutate database tables (<code>WRITES_TO</code>)</td>
                <td class="cell-win"><code class="badge badge--win">PASS</code> (0 violations)</td>
                <td class="cell-win"><code class="badge badge--win">PASS</code> (0 violations)</td>
              </tr>
              <tr>
                <td><code>SIG-TWENTY-002</code><br><strong>Core-Engine to Feature-Modules Isolation</strong></td>
                <td>Core engine modules (<code>src/engine/core-modules/{admin-panel,workspace,billing}</code>) must never directly call functions in workspace feature modules (<code>src/modules/**</code>)</td>
                <td class="cell-win"><code class="badge badge--win">PASS</code> (0 violations)</td>
                <td class="cell-win"><code class="badge badge--win">PASS</code> (0 violations)</td>
              </tr>
              <tr>
                <td><code>SIG-TWENTY-003</code><br><strong>Admin Panel Cross-Module Table Encapsulation</strong></td>
                <td>Services in <code>admin-panel/**</code> must not introduce direct reads or writes (<code>READS_FROM|WRITES_TO</code>) against tables owned by external domain modules (such as <code>emailingDomain</code>)</td>
                <td class="cell-loss"><code class="badge badge--loss">FAIL</code> (1 new: <code>suspendWorkspace &rarr; emailingDomain</code>)</td>
                <td class="cell-win"><code class="badge badge--win">PASS</code> (caught at Step 99 &amp; fixed in 1 turn at Step 105)</td>
              </tr>
              <tr>
                <td><code>SIG-TWENTY-004</code><br><strong>Async Message Queue Boundary</strong></td>
                <td>Application code must never invoke <code>@Processor</code> background job <code>.handle()</code> methods directly; jobs in <code>/jobs/</code> must be enqueued via <code>MessageQueueService.add()</code></td>
                <td class="cell-win"><code class="badge badge--win">PASS</code> (0 violations)</td>
                <td class="cell-win"><code class="badge badge--win">PASS</code> (0 violations)</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="sub-block">
          <h3 class="sub-title">How SIG-TWENTY-003 Freezes Existing Module Debt (Explicit Baseline vs 100% Generic Entity-to-Table Ratchet)</h3>
          <p class="panel-copy" style="margin-bottom: var(--space-sm);">
            On baseline commit 12e8fae4, AdminPanelModule (admin-panel.module.ts:59 to 66) already registered 6 legacy entities (user, workspace, userWorkspace, featureFlag, billingCustomer, billingPrice). In CodeExplorer, you can freeze those existing couplings either inline (Form A, used during the benchmark run) or with zero table names in Cypher by traversing (t:Table)-[:IMPLEMENTED_BY]-&gt;(e:Type) to the owning TypeORM @Entity class and running ce gate ratchet --update -g SIG-TWENTY-003 (Form B):
          </p>
          <pre class="code-block"># Form B: 100% Generic Cross-Module Table Encapsulation (zero hardcoded table names in Cypher)
target: "{t.name}"
ratchet:
  enabled: true
  baseline: baselines/SIG-TWENTY-003.json
query: |
  MATCH (c:Type)-[:HAS_METHOD]-&gt;(fn:Function)-[:READS_FROM|WRITES_TO]-&gt;(t:Table)-[:IMPLEMENTED_BY]-&gt;(e:Type)
  WHERE c.path STARTS WITH 'packages/twenty-server/src/engine/core-modules/admin-panel/'
    AND NOT coalesce(fn.is_test, false)
    AND NOT e.path STARTS WITH 'packages/twenty-server/src/engine/core-modules/admin-panel/'
  RETURN DISTINCT c, t, e

# Verbatim output on cbm-agent.patch (existing workspace/billingCustomer couplings are [TOL], emailingDomain is [NEW]):
$ ce gate details -g SIG-TWENTY-003
[FAIL] SIG-TWENTY-003 [Architectural/agnostic] - Admin Panel Cross-Module Table Encapsulation (428ms)
  Violations (1 new, 13 tolerated) | Showing 14 of 14:
  STATUS | LOCATION                                                                                                | SYMBOL                   | TARGET / CALL
  -------+---------------------------------------------------------------------------------------------------------+--------------------------+----------------
  [TOL]  | packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:46:8 | AdminPanelBillingService | billingCustomer
  [TOL]  | packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:46:8 | AdminPanelBillingService | billingPrice
  [NEW]  | packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:46:8 | AdminPanelBillingService | emailingDomain
  [TOL]  | packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:46:8 | AdminPanelBillingService | workspace</pre>
        </div>
      </div>
    </section>

    <section class="band" id="git-diffs">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">3. Side-by-side code comparison: shipped git diffs.</h2>
          <p class="band-lede">
            Compare the exact production code shipped by both agents in admin-panel.module.ts and admin-panel-billing.service.ts.
          </p>
        </div>

        <div class="split-bench">
          <div class="bench-pane">
            <div class="bench-pane-head">
              <span>CBM Agent Patch (<code>results/cbm-agent.patch</code>)</span>
              <span class="measured tone-bad">FAIL: SIG-TWENTY-003</span>
            </div>
            <pre class="code-view"><span class="syn-cmt">// 1. admin-panel.module.ts (CBM registers EmailingDomainEntity in AdminPanelModule):</span>
TypeOrmModule.forFeature([
  UserEntity,
  WorkspaceEntity,
  UserWorkspaceEntity,
  FeatureFlagEntity,
  BillingCustomerEntity,
  BillingPriceEntity,
  <span class="tone-bad">EmailingDomainEntity, // &lt;-- Leaks emailingDomain table into admin-panel</span>
]),
...
provideWorkspaceScopedRepository(FeatureFlagEntity),
provideWorkspaceScopedRepository(BillingCustomerEntity),
<span class="tone-bad">provideWorkspaceScopedRepository(EmailingDomainEntity),</span>

<span class="syn-cmt">// 2. admin-panel-billing.service.ts (CBM queries emailingDomain table directly):</span>
private readonly workspaceService: WorkspaceService,
<span class="tone-bad">@InjectWorkspaceScopedRepository(EmailingDomainEntity)</span>
<span class="tone-bad">private readonly emailingDomainRepository: WorkspaceScopedRepository&lt;EmailingDomainEntity&gt;,</span>
@InjectMessageQueue(MessageQueue.deleteCascadeQueue)
private readonly deleteCascadeMessageQueueService: MessageQueueService,
...
const emailingDomains =
  <span class="tone-bad">await this.emailingDomainRepository.find(workspaceId);</span></pre>
          </div>

          <div class="bench-pane">
            <div class="bench-pane-head">
              <span>CodeExplorer Agent Patch (<code>results/ce-agent.patch</code>)</span>
              <span class="measured tone-ok">PASS: 4/4 Gates</span>
            </div>
            <pre class="code-view"><span class="syn-cmt">// 1. admin-panel.module.ts (ce imports EmailingDomainModule cleanly):</span>
TypeOrmModule.forFeature([
  UserEntity,
  WorkspaceEntity,
  UserWorkspaceEntity,
  FeatureFlagEntity,
  BillingCustomerEntity,
  BillingPriceEntity,
]),
BillingModule,
<span class="tone-ok">EmailingDomainModule, // &lt;-- Clean domain module boundary import</span>
FileModule,

<span class="syn-cmt">// 2. admin-panel-billing.service.ts (ce delegates to EmailingDomainService):</span>
private readonly workspaceService: WorkspaceService,
<span class="tone-ok">private readonly emailingDomainService: EmailingDomainService,</span>
@InjectMessageQueue(MessageQueue.deleteCascadeQueue)
private readonly deleteCascadeMessageQueueService: MessageQueueService,
...
const emailingDomains =
  <span class="tone-ok">await this.emailingDomainService.getEmailingDomains(workspace);</span></pre>
          </div>
        </div>
      </div>
    </section>

    <section class="band band--deep" style="border-bottom: none;">
      <div class="wrap">
        <div id="prompts" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">
          <div class="prompt-stack-head">
            <h2 class="prompt-stack-title">4. Reproducibility: neutral task prompt &amp; 100% symmetric system prompts.</h2>
            <span class="prompt-stack-note">Click any drawer below to expand verbatim prompt text</span>
          </div>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Task Prompt</span>
                <span>prompts/task-prompt.md: Neutral Feature Specification Given Identically to Both Agents (Zero Rule Hints)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/07-agent-feature-ce-vs-cbm-on-twenty/prompts/task-prompt.md"># Feature Specification: Admin Panel Workspace Suspension (\`suspendWorkspaceAdminPanel\`)

Implement a new \`suspendWorkspaceAdminPanel\` GraphQL mutation in the Admin Panel of \`twenty-server\` (\`packages/twenty-server/src/engine/core-modules/admin-panel/\`) so a server admin can suspend an active workspace for abuse or non-payment and clean up its active billing, webhook, and emailing domain resources.

## Requirements

1. **GraphQL Mutation (\`AdminPanelResolver\`)**:
   - Add a \`suspendWorkspaceAdminPanel\` mutation returning \`Boolean\` in \`packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts\`, guarded with \`@UseGuards(AccessPolicyGuard(ADMIN_PANEL_ACCESS_POLICY), AdminPanelGuard)\`.
   - Accept \`@Args('workspaceId', { type: () =&gt; UUIDScalarType }) workspaceId: string\` and \`@AuthUser() actor: AuthContextUser\`.
   - Delegate the operation to \`AdminPanelBillingService.suspendWorkspace({ workspaceId, actorUserId: actor.id })\` in \`packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts\` (updating \`AdminPanelModule\` imports/providers in \`admin-panel.module.ts\` if needed).

2. **Workspace Validation, Suspension &amp; Connected-Account Webhook Revocation**:
   - Verify the target workspace exists and is not soft-deleted (\`deletedAt\` is null); if not found or deleted, throw \`BillingException\` with \`BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND\`.
   - Suspend the workspace (\`activationStatus = WorkspaceActivationStatus.SUSPENDED\`, \`suspendedAt\` timestamp, and workspace entity cache invalidation) and ensure all connected-account webhook subscriptions (\`MessageChannel\` / \`CalendarChannel\` for Google and Microsoft) for that workspace are revoked.

3. **Billing Subscription Cancellation &amp; Credit Grant Revocation**:
   - When billing is enabled (\`IS_BILLING_ENABLED\`), cancel the workspace's active billing subscription and revoke all currently active credit grants (\`isActive\`) for the workspace on behalf of \`actorUserId\`.

4. **Emailing Domain Cleanup**:
   - Trigger cleanup of the workspace's configured emailing domains via \`EmailingDomainWorkspaceCleanupJob\`.

5. **Unit Tests**:
   - Create unit tests in \`packages/twenty-server/src/engine/core-modules/admin-panel/services/__tests__/admin-panel-billing.service.spec.ts\` covering \`suspendWorkspace\` (workspace not found error, billing enabled flow, and billing disabled flow).

6. **Completion Report**:
   - Once the code changes and unit test file are written and verified with your assigned graph CLI, send a concise summary of all modified/created files and how each requirement was satisfied via \`send_message\` to parent conversation \`517d38de-0260-4402-81a4-a016ed53ecb9\`.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--win">System Prompt (ce)</span>
                <span>prompts/ce-agent.md: System Instructions for CodeExplorer Agent (ce_twenty_feature_agent)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/07-agent-feature-ce-vs-cbm-on-twenty/prompts/ce-agent.md"># Agent System Instructions (\`ce_twenty_feature_agent\`)

You are an expert software engineer equipped with the **CodeExplorer CLI (\`ce\`)** plus standard filesystem/search/editing tools (\`rg\`, \`read_file\`, \`edit_file\`, \`write_file\`).
You are implementing a backend feature in the \`twenty\` monorepo at \`./repos/twenty\` (\`24,527\` tracked files, \`46\` packages), which is pre-indexed in \`./repos/twenty/.codeexplorer/graph.db\`.

CRITICAL BENCHMARK RULES:
1. Use your assigned graph CLI (\`ce\`) as your primary tool for exploring the codebase architecture, dependencies, and call flows before editing files. You may also use \`read_file\`, \`edit_file\`, \`write_file\`, and \`rg\` as needed.
2. Available graph CLI commands (run via \`run_command\`):
   - Inspect schema &amp; ontology:
     \`ce taxonomy -d "./repos/twenty"\` (or \`ce --help\`)
   - Query graph (\`Cypher\`):
     \`ce query "&lt;CYPHER&gt;" -d "./repos/twenty" --no-truncate\`
   - Search symbols:
     \`ce search "&lt;QUERY&gt;" -d "./repos/twenty"\`
   - Trace callers/callees:
     \`ce callers "&lt;NAME&gt;" -d "./repos/twenty"\`
   - Incremental re-index &amp; verify architectural gates after making code edits:
     \`ce scan "./repos/twenty"\` and \`ce gate check "./repos/twenty"\` (and \`ce gate details "./repos/twenty"\` if any gate fails)
3. You are STRICTLY FORBIDDEN from running \`codebase-memory-mcp\` or accessing any CBM cache files. Always pass \`-g "!.codeexplorer/**"\` when running \`rg\`.
4. Do NOT run \`yarn\`, \`npm\`, \`npx\`, or \`jest\` (dependencies are not installed in this checkout). Verify your implementation using your assigned graph CLI and code inspection, then send your final summary report via \`send_message\`.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--cool">System Prompt (CBM)</span>
                <span>prompts/cbm-agent.md: System Instructions for codebase-memory-mcp Agent (cbm_twenty_feature_agent)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/07-agent-feature-ce-vs-cbm-on-twenty/prompts/cbm-agent.md"># Agent System Instructions (\`cbm_twenty_feature_agent\`)

You are an expert software engineer equipped with the **\`codebase-memory-mcp\` CLI (\`codebase-memory-mcp\`)** plus standard filesystem/search/editing tools (\`rg\`, \`read_file\`, \`edit_file\`, \`write_file\`).
You are implementing a backend feature in the \`twenty\` monorepo at \`./repos/twenty\` (\`24,527\` tracked files, \`46\` packages), which is pre-indexed in \`codebase-memory-mcp\` under project name \`twenty\`.

CRITICAL BENCHMARK RULES:
1. Use your assigned graph CLI (\`codebase-memory-mcp\`) as your primary tool for exploring the codebase architecture, dependencies, and call flows before editing files. You may also use \`read_file\`, \`edit_file\`, \`write_file\`, and \`rg\` as needed.
2. Available graph CLI commands (run via \`run_command\`):
   - Inspect schema &amp; ontology:
     \`codebase-memory-mcp cli get_graph_schema --project twenty --format json\` (or \`--help\`)
   - Query graph (\`Cypher\`):
     \`codebase-memory-mcp cli query_graph --project twenty --query "&lt;CYPHER&gt;" --max-rows 5000 --max-output-tokens 100000 --format json\`
   - Search symbols:
     \`codebase-memory-mcp cli search_graph --project twenty --name-pattern "&lt;REGEX&gt;" --format json\`
   - Trace callers/callees:
     \`codebase-memory-mcp cli trace_path --project twenty --function-name "&lt;NAME&gt;" --direction inbound --format json\`
   - Incremental re-index &amp; verify architecture after making code edits:
     \`codebase-memory-mcp cli index_repository --repo-path "./repos/twenty"\` and \`codebase-memory-mcp cli get_architecture --project twenty --format json\`
3. You are STRICTLY FORBIDDEN from running \`ce\`, \`codeexplorer\`, or accessing any \`.codeexplorer\` / \`graph.db\` files. Always pass \`-g "!.codeexplorer/**"\` when running \`rg\`.
4. Do NOT run \`yarn\`, \`npm\`, \`npx\`, or \`jest\` (dependencies are not installed in this checkout). Verify your implementation using your assigned graph CLI and code inspection, then send your final summary report via \`send_message\`.</pre>
            </div>
          </details>
        </div>
      </div>
    </section>`},"/benchmarks/08-multi-task-architecture-suite-on-twenty":{routePath:"/benchmarks/08-multi-task-architecture-suite-on-twenty",pageKind:"benchmark-case",title:"Case Study 08: 4-Task Brownfield Architecture & Feature Suite on twentyhq/twenty",description:"Case Study 08: 4-task brownfield feature implementation suite on twentyhq/twenty (24,527 files, 2.49M LOC) comparing CodeExplorer (v1.87.2) against CBM (v0.12.1) across 4 real backend tasks. CodeExplorer finished 1.29× faster (34m 14s vs 44m 6s), caught 4/4 copied legacy shortcuts via pre-configured ratchet gates, and passed 14/14 unit tests with 0 gate-tampering attempts.",descriptionMetaBind:"cs08.meta_desc",canonical:"https://code-explorer.app/benchmarks/08-multi-task-architecture-suite-on-twenty",ogUrl:"https://code-explorer.app/benchmarks/08-multi-task-architecture-suite-on-twenty",ogTitle:"Case Study 08: 4-Task Brownfield Architecture & Feature Suite on twentyhq/twenty",ogDescription:"Case Study 08: 4-task brownfield feature implementation suite on twentyhq/twenty (24,527 files, 2.49M LOC) comparing CodeExplorer (v1.87.2) against CBM (v0.12.1) across 4 real backend tasks. CodeExplorer finished 1.29× faster (34m 14s vs 44m 6s), caught 4/4 copied legacy shortcuts via pre-configured ratchet gates, and passed 14/14 unit tests with 0 gate-tampering attempts.",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 08: 4-Task Brownfield Feature Suite on twentyhq/twenty</span>
          </div>
          <span class="measured" data-bind="cs08.crumb_scale">46 packages, 24,527 files, 2.49M LOC · 4 brownfield feature tasks</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 08: 4-task brownfield feature suite on twentyhq/twenty.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              A 4-task brownfield feature implementation study on unmodified twentyhq/twenty comparing codebase-memory-mcp (CBM) against CodeExplorer Phase 1 (Navigation Only) and CodeExplorer Phase 2 (Navigation + Semantic Invariant Gates). Without gates, both agents copied adjacent [TOL] legacy shortcuts in 4 out of 4 tasks (100%); with ce gate check, 4 out of 4 self-corrected on the first iteration with 0 gate-tampering attempts and 14/14 passing unit tests.
            </p>
            <div class="hero-controls">
              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/08-multi-task-architecture-suite-on-twenty" target="_blank" rel="noopener" class="btn btn--primary">Inspect All Patches &amp; Test Runner</a>
              <a href="#prompts" class="btn">Inspect Tested Prompts</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-ok" data-bind="cs08.card_violations">0/4 vs 4/4</span>
            <h2 class="panel-title">Violations Shipped</h2>
            <p class="panel-copy" data-bind-html="cs08.card_violations_copy">Without gates, both CBM and CE Phase 1 copied adjacent [TOL] legacy shortcuts in 4/4 tasks (100%). With ce gate check, 0/4 shipped.</p>
            <code class="panel-code" data-bind="cs08.card_violations_footer">0 final violations vs 4 in CBM (256 tolerated baseline)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs08.card_speedup">~1.3×</span>
            <h2 class="panel-title">Faster Suite Total</h2>
            <p class="panel-copy" data-bind-html="cs08.card_speed_copy">Across all 4 tasks combined, CodeExplorer finished in 34m 14s (incl. scan, gate check &amp; 1-turn fixes) vs 44m 6s for CBM.</p>
            <code class="panel-code" data-bind="cs08.card_speed_footer">2,054 s vs 2,646 s (-9m 52s across 4 tasks)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs08.card_tests">14/14 Pass</span>
            <h2 class="panel-title">Verified Unit Tests</h2>
            <p class="panel-copy" data-bind-html="cs08.card_tests_copy">Both CodeExplorer and CBM passed 14/14 unit tests (4/4 tasks), verified via scripts/run-acceptance-tests.mjs; only CodeExplorer passed 4/4 architectural gates.</p>
            <code class="panel-code" data-bind="cs08.card_tests_footer">14/14 unit tests + 4/4 architectural gates</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs08.card_tamper">0 Bypasses</span>
            <h2 class="panel-title">Zero Gate Tampering</h2>
            <p class="panel-copy" data-bind-html="cs08.card_tamper_copy">In 4/4 tasks, the CodeExplorer agent refactored to the proper NestJS domain service in 1 iteration with 0 attempts to run ratchet --update or edit gate files.</p>
            <code class="panel-code" data-bind="cs08.card_tamper_footer">1 self-correction iteration per task, 0 gate tampering</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs08.participants_html">Tested participants (4 brownfield feature tasks on unmodified twentyhq/twenty @ 12e8fae4):<br>
          1. CodeExplorer (ce): v1.87.2 (.NET 11, SQLite + OpenCypher + Incremental ce scan + 4 repo-wide Semantic Invariant Gates, 464,794 nodes, 1,113,891 edges, evaluated both pre-gate Phase 1: Navigation Only and post-gate Phase 2: Navigation + Gates)<br>
          2. codebase-memory-mcp (CBM): v0.12.1 (win32-x64 Go + SQLite binary, index_repository + query_graph + trace_path)<br>
          3. LLM Model: Gemini 3.8 Flash (100% symmetric system prompts on unmodified repository CLAUDE.md; Tasks 01, 02 &amp; 04 executed for this suite + Task 03 from Case 07)<br>
          4. Target Repository: twentyhq/twenty @ 12e8fae4 (46 packages, 24,527 files, 2.49M LOC)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Suite-wide scorecard (4 tasks combined).</h2>
          <p class="band-lede">
            Recording CodeExplorer both before running ce gate check (Phase 1: Navigation Only) and after running ce gate check (Phase 2: Navigation + Gates) alongside CBM isolates what structural graph navigation achieves versus what deterministic architectural gates enforce.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Suite-wide scorecard across 4 tasks">
          <div class="table-caption-bar">
            <span>Table 1: 4-Task Combined Scorecard (twentyhq/twenty @ 12e8fae4)</span>
            <span class="measured">4 brownfield tasks · 0 context compactions</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Metric (All 4 Tasks Combined)</th>
                <th>CBM<br><span class="tone-muted">(Call Graph MCP)</span></th>
                <th>CE Phase 1<br><span class="tone-muted">(Navigation Only, Pre-Gate)</span></th>
                <th>CE Phase 2<br><span class="tone-muted">(Navigation + Gates, Final)</span></th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Tasks Introducing Violation in Initial Draft</strong></td>
                <td class="cell-num tone-warn">4 / 4 (100%)</td>
                <td class="cell-num tone-warn">4 / 4 (100%)</td>
                <td class="cell-num tone-ok">4 / 4 caught by <code>ce gate check</code></td>
              </tr>
              <tr>
                <td><strong>Tasks Shipping Final Violations</strong></td>
                <td class="cell-num tone-warn"><strong>4 / 4 (100%)</strong></td>
                <td class="cell-num tone-warn">4 / 4 (100%)</td>
                <td class="cell-win"><strong>0 / 4 (0%)</strong></td>
              </tr>
              <tr>
                <td>Total Final [NEW] Gate Violations</td>
                <td class="cell-num tone-warn">4 new (256 tol)</td>
                <td class="cell-num tone-warn">4 new (pre-gate)</td>
                <td class="cell-win"><strong>0 new (4/4 PASS)</strong></td>
              </tr>
              <tr>
                <td><strong>Self-Correction Iterations Required</strong></td>
                <td class="cell-num tone-muted">&minus;</td>
                <td class="cell-num tone-muted">&minus;</td>
                <td class="cell-win"><strong>1 iteration per task</strong></td>
              </tr>
              <tr>
                <td><strong>Gate Bypass / Tampering Attempts</strong></td>
                <td class="cell-num tone-muted">&minus;</td>
                <td class="cell-num tone-muted">&minus;</td>
                <td class="cell-win"><strong>0 / 4 (0%)</strong></td>
              </tr>
              <tr>
                <td>Verified Unit Tests (run-acceptance-tests.mjs)</td>
                <td class="cell-num tone-ok">14 / 14 (100%, 4/4 tasks)</td>
                <td class="cell-num tone-muted">&minus;</td>
                <td class="cell-win"><strong>14 / 14 (100%, 4/4 tasks)</strong></td>
              </tr>
              <tr>
                <td><strong>Total Wall-Clock Time (Sum of 4 Tasks)</strong></td>
                <td class="cell-num">2,646 s (44m 06s)</td>
                <td class="cell-num tone-muted">&minus;</td>
                <td class="cell-win"><strong>2,054 s (34m 14s, 1.29&times; faster)</strong></td>
              </tr>
              <tr>
                <td>Total LLM Turns / Steps</td>
                <td class="cell-num">127 turns (320 steps)</td>
                <td class="cell-num tone-muted">&minus;</td>
                <td class="cell-win"><strong>125 turns (338 steps)</strong></td>
              </tr>
              <tr>
                <td>Total Source File Reads (read_file)</td>
                <td class="cell-num">96 reads</td>
                <td class="cell-num tone-muted">&minus;</td>
                <td class="cell-win"><strong>92 reads</strong></td>
              </tr>
              <tr>
                <td><strong>Cumulative Input Tokens (All 4 Tasks)</strong></td>
                <td class="cell-num">10,030,169&nbsp;tokens<br><span class="tone-muted">($0.48&nbsp;total)</span></td>
                <td class="cell-num tone-muted">&minus;</td>
                <td class="cell-win"><strong>9,745,267&nbsp;tokens</strong><br><span class="tone-muted">($0.48&nbsp;total)</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Per-task breakdown &amp; adjacent brownfield traps.</h2>
          <p class="band-lede">
            Each task targets a different module (auth, messaging, admin-panel, domain) where the nearest existing method in the edited file or sibling service contains a tolerated ([TOL]) legacy shortcut. Tasks 01, 02, and 04 were executed specifically for this multi-task suite; Task 03 incorporates the clean 12e8fae4 workspace suspension run analyzed in detail in <a href="/benchmarks/07-agent-feature-ce-vs-cbm-on-twenty">Case Study 07</a>.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Per-task breakdown across all 4 tasks">
          <div class="table-caption-bar">
            <span>Table 2: Per-Task Telemetry, Adjacent Legacy Traps &amp; Verification Outcomes</span>
            <span class="measured">4 Tasks · 256 frozen [TOL] baseline exceptions</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Task &amp; Target Symbol</th>
                <th>Repo-Wide Gate &amp; Adjacent [TOL] Trap</th>
                <th>CBM<br><span class="tone-muted">(Final Result)</span></th>
                <th>CodeExplorer<br><span class="tone-muted">(Phase 1 Pre-Gate &rarr; Phase 2 Final)</span></th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Task 01: Admin 2FA Reset</strong><br><code>TwoFactorAuthenticationResolver</code><br><code>.resetTwoFactorAuthenticationMethodsForUser</code></td>
                <td><code>SIG-TWENTY-001</code> (5 tol): Resolver Transport Purity.<br><span class="tone-muted">Trap: <code>deleteTwoFactorAuthenticationMethod</code> (line 215) calls <code>repository.delete(...)</code> in resolver.</span></td>
                <td class="cell-num">424 s (7m 04s), 22 turns, 19 reads<br><strong class="tone-warn">[FAIL] (1 new in SIG-TWENTY-001)</strong><br>3/3 unit tests pass ($0.07)</td>
                <td class="cell-win"><strong>508 s (8m 28s), 30 turns, 19 reads</strong><br>1 new &rarr; <strong class="tone-ok">[PASS] (0 new, 1 iter)</strong><br><strong>3/3 unit tests pass ($0.10)</strong></td>
              </tr>
              <tr>
                <td><strong>Task 02: Reconnect Banner</strong><br><code>TimelineMessagingResolver</code><br><code>.triggerReconnectAccountBanner</code></td>
                <td><code>SIG-TWENTY-002</code> (101 tol): Core-Engine to Feature-Modules Isolation.<br><span class="tone-muted">Trap: <code>dismissReconnectAccountBanner</code> (line 282) calls <code>AccountsToReconnectService</code> in <code>src/modules/</code>.</span></td>
                <td class="cell-num">455 s (7m 35s), 27 turns, 20 reads<br><strong class="tone-warn">[FAIL] (1 new in SIG-TWENTY-002)</strong><br>4/4 unit tests pass ($0.06)</td>
                <td class="cell-win"><strong>384 s (6m 24s, 1.18&times; faster), 22 turns, 20 reads</strong><br>1 new &rarr; <strong class="tone-ok">[PASS] (0 new, 1 iter)</strong><br><strong>3/3 unit tests pass ($0.06)</strong></td>
              </tr>
              <tr>
                <td><strong>Task 03: Workspace Suspension</strong> <span class="tone-muted">(<a href="/benchmarks/07-agent-feature-ce-vs-cbm-on-twenty">Case 07</a>)</span><br><code>AdminPanelBillingService</code><br><code>.suspendWorkspace</code></td>
                <td><code>SIG-TWENTY-003</code> (150 tol): Cross-Module Table Encapsulation.<br><span class="tone-muted">Trap: <code>workspace.service.ts:989</code> directly queries <code>EmailingDomainEntity</code> across modules.</span></td>
                <td class="cell-num">1,225 s (20m 25s), 54 turns, 43 reads<br><strong class="tone-warn">[FAIL] (1 new in SIG-TWENTY-003)</strong><br>3/3 unit tests pass ($0.29)</td>
                <td class="cell-win"><strong>779 s (12m 59s, 1.57&times; faster), 43 turns, 31 reads</strong><br>1 new &rarr; <strong class="tone-ok">[PASS] (0 new, 1 iter)</strong><br><strong>4/4 unit tests pass ($0.24)</strong></td>
              </tr>
              <tr>
                <td><strong>Task 04: Subdomain Collision Check</strong><br><code>SubdomainManagerService</code><br><code>.isSubdomainAvailable</code></td>
                <td><code>SIG-TWENTY-003</code> (150 tol): Cross-Module Table Encapsulation.<br><span class="tone-muted">Trap: <code>CustomDomainManagerService</code> injects <code>@InjectRepository(PublicDomainEntity)</code>.</span></td>
                <td class="cell-num">542 s (9m 02s), 24 turns, 14 reads<br><strong class="tone-warn">[FAIL] (1 new in SIG-TWENTY-003)</strong><br>4/4 unit tests pass ($0.07)</td>
                <td class="cell-win"><strong>383 s (6m 23s, 1.42&times; faster), 30 turns, 22 reads</strong><br>1 new &rarr; <strong class="tone-ok">[PASS] (0 new, 1 iter)</strong><br><strong>4/4 unit tests pass ($0.09)</strong></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">3. Why not just put all architectural rules in CLAUDE.md?</h2>
          <p class="band-lede">
            Neither agent read CLAUDE.md during these runs: LLMs naturally copy the nearest method in the file they are editing. Relying on prose instructions in CLAUDE.md instead of executable graph gates fails in real engineering teams for four reasons.
          </p>
        </div>

        <div class="panels panels--4">
          <div class="panel panel--sunken">
            <span class="reading tone-warn">0 Rules</span>
            <h2 class="panel-title">Unwritten in Real Repos</h2>
            <p class="panel-copy">
              In twentyhq/twenty's actual CLAUDE.md, none of these module-encapsulation rules exist, and 256 legacy violations have already accumulated across the codebase.
            </p>
            <code class="panel-code">5 + 101 + 150 = 256 legacy shortcuts</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent">24,527 Files</span>
            <h2 class="panel-title">No Hindsight in Production</h2>
            <p class="panel-copy">
              You cannot predict which of 24,527 files an agent will open next or maintain a prose encyclopedia of every forbidden cross-module edge inside the prompt context window.
            </p>
            <code class="panel-code">46 packages &amp; 464,794 graph nodes</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool">256 Frozen</span>
            <h2 class="panel-title">Prose Has No Ratchet</h2>
            <p class="panel-copy">
              The file the agent edits <em>already</em> imports foreign repositories ([TOL]). Prose cannot freeze 256 legacy exceptions while failing strictly on 1 newly added call site ([NEW]).
            </p>
            <code class="panel-code">SHA-256 baseline ratchet</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-ok">Exit 1 / 0</span>
            <h2 class="panel-title">Deterministic CI Gate</h2>
            <p class="panel-copy">
              Prompts are advisory and cannot block a pull request. ce gate check runs the exact same Cypher rules in the agent loop and in CI with zero LLM tokens.
            </p>
            <code class="panel-code">ce gate check &rarr; Exit Code 1</code>
          </div>
        </div>
      </div>
    </section>

    <section class="band band--deep" style="border-bottom: none;">
      <div class="wrap">
        <div id="prompts" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">
          <div class="prompt-stack-head">
            <h2 class="prompt-stack-title">4. Verbatim prompts &amp; reproducible test runner.</h2>
            <span class="prompt-stack-note">Click any drawer below to expand verbatim prompt text</span>
          </div>
          <p class="band-lede" style="margin-bottom: var(--space-md);">
            To avoid spawning full-monorepo Jest workers on Windows, agents wrote .spec.ts unit tests without invoking Jest directly; every patch was then verified against a clean worktree using benchmarks/08-multi-task-architecture-suite-on-twenty/scripts/run-acceptance-tests.mjs (esbuild + NestJS Test.createTestingModule, 14/14 passing tests for both tools).
          </p>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Task 01 Prompt</span>
                <span>prompts/task-01-prompt.md: Admin Reset of Target User 2FA Methods (SIG-TWENTY-001)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/08-multi-task-architecture-suite-on-twenty/prompts/task-01-prompt.md"># Task 01 Prompt — Admin Reset of Target User 2FA Methods (\`twentyhq/twenty\`)

Target Repository: \`C:\\Work\\bench-pilot\\repos\\twenty\`

## Feature Request: \`resetTwoFactorAuthenticationMethodsForUser\`

Implement a workspace security-admin GraphQL mutation \`resetTwoFactorAuthenticationMethodsForUser\` in the \`two-factor-authentication\` module (\`packages/twenty-server/src/engine/core-modules/two-factor-authentication/\`) so a workspace administrator with \`PermissionFlagType.SECURITY\` can reset all two-factor authentication methods and recovery codes for a target user in the current workspace.

### Functional Requirements

1. **GraphQL Mutation (\`TwoFactorAuthenticationResolver\`)**:
   - Add a \`@Mutation(() =&gt; DeleteTwoFactorAuthenticationMethodDTO)\` named \`resetTwoFactorAuthenticationMethodsForUser\` on \`TwoFactorAuthenticationResolver\` (\`packages/twenty-server/src/engine/core-modules/two-factor-authentication/two-factor-authentication.resolver.ts\`).
   - Guard it with \`@UseGuards(AccessPolicyGuard(TWO_FACTOR_AUTHENTICATION_RECOVERY_ACCESS_POLICY))\` (matching \`twoFactorAuthenticationRecoveryStatus\` and \`revokeTwoFactorAuthenticationRecoveryCode\` in the same resolver).
   - Accept \`@Args() { userId }: TwoFactorAuthenticationRecoveryTargetInput\` and \`@AuthWorkspace() workspace: WorkspaceEntity\`.
2. **Deletion &amp; Recovery Code Revocation Logic**:
   - Resolve the target user's \`userWorkspace\` in \`workspace.id\` via \`UserWorkspaceService.getUserWorkspaceForUserOrThrow({ userId, workspaceId: workspace.id })\`.
   - Delete all \`TwoFactorAuthenticationMethodEntity\` records belonging to \`{ userWorkspaceId: userWorkspace.id }\` in \`workspace.id\`.
   - Revoke all two-factor authentication recovery codes for \`(workspace.id, userId)\` with \`includeRedeemed: true\`.
   - Return \`{ success: true }\`.
3. **Unit Tests**:
   - Add or update unit tests in \`packages/twenty-server/src/engine/core-modules/two-factor-authentication/__tests__/two-factor-authentication.resolver.spec.ts\` covering the new \`resetTwoFactorAuthenticationMethodsForUser\` flow.

### Constraints
- Do NOT run \`yarn\`, \`nx\`, \`jest\`, or \`tsc\` via \`run_command\` (no database/Redis containers are running in the benchmark sandbox).
- When finished, send your final summary report via \`send_message\` listing all modified/created files and explaining how \`resetTwoFactorAuthenticationMethodsForUser\` is wired.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Task 02 Prompt</span>
                <span>prompts/task-02-prompt.md: Flag Connected Account Reconnect Banner (SIG-TWENTY-002)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/08-multi-task-architecture-suite-on-twenty/prompts/task-02-prompt.md"># Task 02 Prompt — Flag Connected Account Reconnect Banner (\`twentyhq/twenty\`)

Target Repository: \`C:\\Work\\bench-pilot\\repos\\twenty\`

## Feature Request: \`triggerReconnectAccountBanner\`

Implement a GraphQL mutation \`triggerReconnectAccountBanner\` in the \`messaging\` module (\`packages/twenty-server/src/engine/core-modules/messaging/\`) alongside \`dismissReconnectAccountBanner\` on \`TimelineMessagingResolver\` (\`packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.resolver.ts\`) so the client or a permission error handler can mark a \`connectedAccountId\` as requiring reconnection due to insufficient permissions for the authenticated user and workspace.

### Functional Requirements

1. **GraphQL Mutation (\`TimelineMessagingResolver\`)**:
   - Add a \`@Mutation(() =&gt; Boolean)\` named \`triggerReconnectAccountBanner\` on \`TimelineMessagingResolver\` (\`packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.resolver.ts\`).
   - Guard it with the exact same \`AccessPolicyGuard\` (\`PermissionFlagType.CONNECTED_ACCOUNTS\`) and \`CustomPermissionGuard\` as \`dismissReconnectAccountBanner\`.
   - Accept \`@AuthUser() user: AuthContextUser\`, \`@AuthWorkspace() workspace: WorkspaceEntity\`, and \`@Args() { connectedAccountId }: DismissReconnectAccountBannerInput\`.
2. **Reconnect Banner State Update**:
   - Record \`connectedAccountId\` under the \`AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS\` user-var key for \`(user.id, workspace.id)\` if \`connectedAccountId\` is not already present in that array.
   - Return \`true\`.
3. **Unit Tests**:
   - Add unit tests in \`packages/twenty-server/src/engine/core-modules/messaging/__tests__/timeline-messaging.resolver.spec.ts\` verifying \`triggerReconnectAccountBanner\` (adding a new \`connectedAccountId\` and idempotently skipping when already present).

### Constraints
- Do NOT run \`yarn\`, \`nx\`, \`jest\`, or \`tsc\` via \`run_command\` (no database/Redis containers are running in the benchmark sandbox).
- When finished, send your final summary report via \`send_message\` listing all modified/created files and explaining how \`triggerReconnectAccountBanner\` is wired.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Task 03 Prompt</span>
                <span>prompts/task-03-prompt.md: Admin Panel Workspace Suspension (SIG-TWENTY-003)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/08-multi-task-architecture-suite-on-twenty/prompts/task-03-prompt.md"># Task 03 Prompt — Admin Panel Workspace Suspension (\`twentyhq/twenty\`)

Target Repository: \`C:\\Work\\bench-pilot\\repos\\twenty\`

## Feature Specification: Admin Panel Workspace Suspension (\`suspendWorkspaceAdminPanel\`)

Implement a new \`suspendWorkspaceAdminPanel\` GraphQL mutation in the Admin Panel of \`twenty-server\` (\`packages/twenty-server/src/engine/core-modules/admin-panel/\`) so a server admin can suspend an active workspace for abuse or non-payment and clean up its active billing, webhook, and emailing domain resources.

### Functional Requirements

1. **GraphQL Mutation (\`AdminPanelResolver\`)**:
   - Add a \`suspendWorkspaceAdminPanel\` mutation returning \`Boolean\` in \`packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts\`, guarded with \`@UseGuards(AccessPolicyGuard(ADMIN_PANEL_ACCESS_POLICY), AdminPanelGuard)\`.
   - Accept \`@Args('workspaceId', { type: () =&gt; UUIDScalarType }) workspaceId: string\` and \`@AuthUser() actor: AuthContextUser\`.
   - Delegate the operation to \`AdminPanelBillingService.suspendWorkspace({ workspaceId, actorUserId: actor.id })\` in \`packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts\` (updating \`AdminPanelModule\` imports/providers in \`admin-panel.module.ts\` if needed).

2. **Workspace Validation, Suspension &amp; Connected-Account Webhook Revocation**:
   - Verify the target workspace exists and is not soft-deleted (\`deletedAt\` is null); if not found or deleted, throw \`BillingException\` with \`BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND\`.
   - Suspend the workspace (\`activationStatus = WorkspaceActivationStatus.SUSPENDED\`, \`suspendedAt\` timestamp, and workspace entity cache invalidation) and ensure all connected-account webhook subscriptions (\`MessageChannel\` / \`CalendarChannel\` for Google and Microsoft) for that workspace are revoked.

3. **Billing Subscription Cancellation &amp; Credit Grant Revocation**:
   - When billing is enabled (\`IS_BILLING_ENABLED\`), cancel the workspace's active billing subscription and revoke all currently active credit grants (\`isActive\`) for the workspace on behalf of \`actorUserId\`.

4. **Emailing Domain Cleanup**:
   - Trigger cleanup of the workspace's configured emailing domains via \`EmailingDomainWorkspaceCleanupJob\`.

5. **Unit Tests**:
   - Create unit tests in \`packages/twenty-server/src/engine/core-modules/admin-panel/services/__tests__/admin-panel-billing.service.spec.ts\` covering \`suspendWorkspace\` (workspace not found error, billing enabled flow, and billing disabled flow).

### Constraints
- Do NOT run \`yarn\`, \`nx\`, \`jest\`, or \`tsc\` via \`run_command\` (no database/Redis containers are running in the benchmark sandbox).
- When finished, send your final summary report via \`send_message\` listing all modified/created files and explaining how \`suspendWorkspace\` is wired.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--accent">Task 04 Prompt</span>
                <span>prompts/task-04-prompt.md: Subdomain Collision Check Against Public Domains (SIG-TWENTY-003)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/08-multi-task-architecture-suite-on-twenty/prompts/task-04-prompt.md"># Task 04 Prompt — Subdomain Collision Check Against Registered Public Domains (\`twentyhq/twenty\`)

Target Repository: \`C:\\Work\\bench-pilot\\repos\\twenty\`

## Feature Request: Public Domain Collision Check in \`SubdomainManagerService\`

In \`SubdomainManagerService\` (\`packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service.ts\`), workspace subdomains are currently checked only against \`WorkspaceEntity.subdomain\` and \`DEFAULT_SUBDOMAIN\`, while \`CustomDomainManagerService\` (\`packages/twenty-server/src/engine/core-modules/domain/custom-domain-manager/services/custom-domain-manager.service.ts\`) also verifies that a domain does not collide with an existing registered \`PublicDomain\`.

Update \`SubdomainManagerService\` so that subdomain availability also checks whether \`&lt;subdomain&gt;.&lt;baseHostname&gt;\` is already registered as a \`PublicDomain\` (preventing a workspace subdomain from colliding with an existing registered public domain).

### Functional Requirements

1. **\`SubdomainManagerService\` (\`packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service.ts\`)**:
   - Derive the base hostname from \`this.twentyConfigService.get('FRONTEND_URL')\` (using \`new URL(frontendUrl).hostname\` when defined).
   - In \`isSubdomainAvailable(subdomain: string)\`: in addition to checking \`this.workspaceRepository.findOne({ where: { subdomain }, withDeleted: true })\`, if a base hostname is configured, verify that \`\${subdomain}.\${baseHostname}\` is not already registered as a \`PublicDomain\` (returning \`false\` if a matching public domain exists).
   - Update \` domain/subdomain-manager/subdomain-manager.module.ts\` if any module imports are required.
2. **Unit Tests**:
   - Add or update unit tests in \`packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/__tests__/subdomain-manager.service.spec.ts\` verifying that \`isSubdomainAvailable\` returns \`false\` when \`\${subdomain}.\${baseHostname}\` is registered as a public domain, and \`true\` when neither a workspace subdomain nor a public domain matches.

### Constraints
- Do NOT run \`yarn\`, \`nx\`, \`jest\`, or \`tsc\` via \`run_command\` (no database/Redis containers are running in the benchmark sandbox).
- When finished, send your final summary report via \`send_message\` listing all modified/created files and explaining how the public domain check in \`SubdomainManagerService\` is wired.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--win">System Prompt (ce)</span>
                <span>prompts/ce-agent.md: CodeExplorer Agent System Instructions</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/08-multi-task-architecture-suite-on-twenty/prompts/ce-agent.md"># Agent System Instructions (\`ce_twenty_feature_agent\`)

You are an expert software engineer equipped with the **CodeExplorer CLI (\`ce\`)** plus standard filesystem/search/editing tools (\`rg\`, \`read_file\`, \`edit_file\`, \`write_file\`).
You are implementing a backend feature in the \`twenty\` monorepo at \`./repos/twenty\` (\`24,527\` tracked files, \`46\` packages), which is pre-indexed in \`./repos/twenty/.codeexplorer/graph.db\`.

CRITICAL BENCHMARK RULES:
1. Use your assigned graph CLI (\`ce\`) as your primary tool for exploring the codebase architecture, dependencies, and call flows before editing files. You may also use \`read_file\`, \`edit_file\`, \`write_file\`, and \`rg\` as needed.
2. Available graph CLI commands (run via \`run_command\`):
   - Inspect schema &amp; ontology:
     \`ce taxonomy -d "./repos/twenty"\` (or \`ce --help\`)
   - Query graph (\`Cypher\`):
     \`ce query "&lt;CYPHER&gt;" -d "./repos/twenty" --no-truncate\`
   - Search symbols:
     \`ce search "&lt;QUERY&gt;" -d "./repos/twenty"\`
   - Trace callers/callees:
     \`ce callers "&lt;NAME&gt;" -d "./repos/twenty"\`
   - Incremental re-index &amp; verify architectural gates after making code edits:
     \`ce scan "./repos/twenty"\` and \`ce gate check "./repos/twenty"\` (and \`ce gate details "./repos/twenty"\` if any gate fails)
3. You are STRICTLY FORBIDDEN from running \`codebase-memory-mcp\` or accessing any CBM cache files. Always pass \`-g "!.codeexplorer/**"\` when running \`rg\`.
4. Do NOT run \`yarn\`, \`npm\`, \`npx\`, or \`jest\` (dependencies are not installed in this checkout). Verify your implementation using your assigned graph CLI and code inspection, then send your final summary report via \`send_message\`.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--cool">System Prompt (CBM)</span>
                <span>prompts/cbm-agent.md: CBM Baseline Agent System Instructions</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/08-multi-task-architecture-suite-on-twenty/prompts/cbm-agent.md"># Agent System Instructions (\`cbm_twenty_feature_agent\`)

You are an expert software engineer equipped with the **\`codebase-memory-mcp\` CLI (\`codebase-memory-mcp\`)** plus standard filesystem/search/editing tools (\`rg\`, \`read_file\`, \`edit_file\`, \`write_file\`).
You are implementing a backend feature in the \`twenty\` monorepo at \`./repos/twenty\` (\`24,527\` tracked files, \`46\` packages), which is pre-indexed in \`codebase-memory-mcp\` under project name \`twenty\`.

CRITICAL BENCHMARK RULES:
1. Use your assigned graph CLI (\`codebase-memory-mcp\`) as your primary tool for exploring the codebase architecture, dependencies, and call flows before editing files. You may also use \`read_file\`, \`edit_file\`, \`write_file\`, and \`rg\` as needed.
2. Available graph CLI commands (run via \`run_command\`):
   - Inspect schema &amp; ontology:
     \`codebase-memory-mcp cli get_graph_schema --project twenty --format json\` (or \`--help\`)
   - Query graph (\`Cypher\`):
     \`codebase-memory-mcp cli query_graph --project twenty --query "&lt;CYPHER&gt;" --max-rows 5000 --max-output-tokens 100000 --format json\`
   - Search symbols:
     \`codebase-memory-mcp cli search_graph --project twenty --name-pattern "&lt;REGEX&gt;" --format json\`
   - Trace callers/callees:
     \`codebase-memory-mcp cli trace_path --project twenty --function-name "&lt;NAME&gt;" --direction inbound --format json\`
   - Incremental re-index &amp; verify architecture after making code edits:
     \`codebase-memory-mcp cli index_repository --repo-path "./repos/twenty"\` and \`codebase-memory-mcp cli get_architecture --project twenty --format json\`
3. You are STRICTLY FORBIDDEN from running \`ce\`, \`codeexplorer\`, or accessing any \`.codeexplorer\` / \`graph.db\` files. Always pass \`-g "!.codeexplorer/**"\` when running \`rg\`.
4. Do NOT run \`yarn\`, \`npm\`, \`npx\`, or \`jest\` (dependencies are not installed in this checkout). Verify your implementation using your assigned graph CLI and code inspection, then send your final summary report via \`send_message\`.</pre>
            </div>
          </details>
        </div>
      </div>
    </section>`},"/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent":{routePath:"/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent",pageKind:"benchmark-case",title:"Case Study 09: Zero-Spoiler Coding Benchmark (One-Shot Context Pack vs Bare Agent)",description:"Zero-spoiler constructive coding benchmark across 3 multi-module tickets (6 paired runs) on twentyhq/twenty (36,543 indexed files) with symmetric architectural rules: CodeExplorer One-Shot Context Pack (19m 38s, 59 turns, ~3.1M input tokens, 55 file reads) vs Bare Agent (28m 02s, 84 turns, ~6.9M input tokens, 104 file reads).",descriptionMetaBind:"cs09.meta_desc",canonical:"https://code-explorer.app/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent",ogUrl:"https://code-explorer.app/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent",ogTitle:"Case Study 09: Zero-Spoiler Coding Benchmark (One-Shot Context Pack vs Bare Agent)",ogDescription:"Zero-spoiler constructive coding benchmark across 3 multi-module tickets (6 paired runs) on twentyhq/twenty (36,543 indexed files) with symmetric architectural rules: CodeExplorer One-Shot Context Pack (19m 38s, 59 turns, ~3.1M input tokens, 55 file reads) vs Bare Agent (28m 02s, 84 turns, ~6.9M input tokens, 104 file reads).",ogDescriptionMetaBind:"cs09.meta_desc",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 09: Zero-Spoiler One-Shot Context Pack vs Bare Agent</span>
          </div>
          <span class="measured" data-bind="cs09.crumb_scale">46 packages, 36,543 indexed files, 3 zero-spoiler tasks (6 paired runs)</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 09: zero-spoiler coding benchmark (One-Shot Context Pack vs Bare Agent).</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              When an AI coding agent receives a realistic, zero-spoiler bug or feature ticket and both agents share the exact same architectural rules in their system prompt, does a code graph beat a bare <code>ripgrep</code> + <code>read_file</code> agent? Across 3 multi-module tickets (6 paired runs) on <code>twentyhq/twenty</code>, compiling a one-shot Turn-1 context pack cut input tokens by ~2.2×, file reads by ~1.9×, and wall-clock time by ~1.4× with 15/15 Jest assertions and 21/21 gates passing.
            </p>
            <div class="hero-controls">
              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent" target="_blank" rel="noopener" class="btn btn--primary">Inspect Diffs &amp; Harness</a>
              <a href="#prompts" class="btn">Inspect Prompts &amp; Context Pack</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs09.card_tokens">~2.2×</span>
            <h2 class="panel-title">Fewer Input Tokens</h2>
            <p class="panel-copy" data-bind="cs09.card_tokens_copy">Across all 3 zero-spoiler tasks, CodeExplorer used ~3.1M cumulative input tokens vs ~6.9M for the Bare Agent (~1.6× fewer uncached tokens).</p>
            <code class="panel-code" data-bind="cs09.card_tokens_footer">3,080,276 vs 6,897,615 total input tokens</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-ok" data-bind="cs09.card_reads">~1.9×</span>
            <h2 class="panel-title">Fewer File Reads</h2>
            <p class="panel-copy" data-bind="cs09.card_reads_copy">One-shot task context compilation cut source file reads from 104 down to 55 and shell commands from 41 down to 20 (0 rg calls in Task 2).</p>
            <code class="panel-code" data-bind="cs09.card_reads_footer">55 vs 104 file reads (95 vs 165 tool calls)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs09.card_speedup">~1.4×</span>
            <h2 class="panel-title">Faster Completion</h2>
            <p class="panel-copy" data-bind="cs09.card_speed_copy">Completed all 3 backend tasks in 19m 38s (59 turns) with CodeExplorer vs 28m 02s (84 turns) for the Bare Agent.</p>
            <code class="panel-code" data-bind="cs09.card_speed_footer">1,178 s (59 turns) vs 1,682 s (84 turns)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs09.card_pass">15/15 Pass</span>
            <h2 class="panel-title">Behavioral &amp; Gate Parity</h2>
            <p class="panel-copy" data-bind="cs09.card_pass_copy">Every run is verified against an external held-out suite of 15 Jest behavioral assertions (5 per task), tsgo typechecking, and 7 Semantic Invariant Gates (21/21 passed).</p>
            <code class="panel-code" data-bind="cs09.card_pass_footer">15/15 Jest assertions, 21/21 SIG gates (0 new)</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs09.participants_html">Tested participants (3 zero-spoiler constructive coding tasks = 6 paired runs on twentyhq/twenty @ 12e8fae4):<br>
          1. CodeExplorer (ce): v1.90.0 (.NET 11, SQLite + OpenCypher, one-shot task context pack + semantic invariant gates, 465,090 nodes, 1,068,128 edges)<br>
          2. Bare Agent Baseline: ripgrep (rg v14.1.1) + read_file + edit_file + write_file (identical sandbox and identical 7 architectural rules in system prompt)<br>
          3. LLM Model: Gemini 4 Argon (Medium) (100% paired same-model runs across all 3 tasks)<br>
          4. Target Repository: twentyhq/twenty @ 12e8fae4 (46 packages, 36,543 indexed files, TypeScript, NestJS, GraphQL, TypeORM)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Eliminating the three classic agent benchmark traps.</h2>
          <p class="band-lede">
            Most coding benchmarks accidentally measure prompt spoilers, asymmetric instructions, or chatty tool overhead rather than genuine architectural intelligence. This suite eliminates all three.
          </p>
        </div>

        <div class="panels panels--3">
          <div class="panel">
            <span class="reading tone-bad">Trap 01</span>
            <h3 class="panel-title">Zero-Spoiler Symptom Prompts</h3>
            <p class="panel-copy">Naming target files (<code>api-key.service.ts</code>) or method signatures (<code>revokeApiKey</code>) turns a 36,543-file monorepo into a 2-file read task where search is unnecessary. Every ticket here contains <strong>zero file paths, class names, or method names</strong>.</p>
            <code class="panel-code">100% behavior-only bug &amp; feature tickets</code>
          </div>
          <div class="panel">
            <span class="reading tone-bad">Trap 02</span>
            <h3 class="panel-title">Symmetric Architectural Rules</h3>
            <p class="panel-copy">Hiding repository rules from the baseline agent while enforcing them on the graph agent measures prompt secrecy rather than code intelligence. Here, <strong>both agents receive the exact same 7 architectural invariants verbatim</strong> in their system prompt.</p>
            <code class="panel-code">Identical rules in both system prompts</code>
          </div>
          <div class="panel">
            <span class="reading tone-bad">Trap 03</span>
            <h3 class="panel-title">One-Shot Context Over Chatty MCP</h3>
            <p class="panel-copy">Making 15 to 20 sequential MCP calls resends the growing 40K-token conversation history on every turn, burning 500K+ extra input tokens. Instead, CodeExplorer compiles the full task subgraph in <strong>one Turn-1 call</strong> (<code>ce context</code>).</p>
            <code class="panel-code">1 context call on Turn 1 + 1 final gate check</code>
          </div>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. What the Turn-1 context pack resolves before a single file read.</h2>
          <p class="band-lede">
            Finding an entrypoint takes 1 to 2 turns even with <code>ripgrep</code>. Most wasted agent turns go into discovering who owns a database table, what caches or external services must be updated, and whether a cross-module import is safe.
          </p>
        </div>

        <div class="panels panels--3">
          <div class="panel">
            <span class="reading tone-accent">Layer 01</span>
            <h3 class="panel-title">Canonical Table Ownership</h3>
            <p class="panel-copy">Ranks every <code>READS_FROM</code> and <code>WRITES_TO</code> edge across SQL and ORM queries to pinpoint the single owning domain service (<code>UsageLimitService</code> for <code>usageLimit</code>, <code>PublicDomainService</code> for <code>publicDomain</code>) on Turn 1.</p>
            <code class="panel-code">tablesAndCanonicalOwners + readPlan</code>
          </div>
          <div class="panel">
            <span class="reading tone-cool">Layer 02</span>
            <h3 class="panel-title">Downstream Side Effects</h3>
            <p class="panel-copy">Deleting a row is rarely just <code>repo.delete()</code>. Revoking an API key must recompute <code>WorkspaceCacheService</code> and drop Redis quota counters; deleting an application must tear down external DNS hostnames via <code>DnsManagerService</code>.</p>
            <code class="panel-code">outboundSideEffects + inboundCallersAndRoutes</code>
          </div>
          <div class="panel">
            <span class="reading tone-ok">Layer 03</span>
            <h3 class="panel-title">Dependency &amp; Legacy Debt Guards</h3>
            <p class="panel-copy">Flags ratcheted legacy violations inside touched files before the agent reads them so it does not imitate legacy shortcuts, and verifies module dependency acyclicity upfront without manual repository-wide file reads.</p>
            <code class="panel-code">legacyTrapsInSubgraphFiles + cycle verdicts</code>
          </div>
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">3. Suite-wide scorecard across all 3 zero-spoiler tasks (6 paired runs).</h2>
          <p class="band-lede">
            Both agents pass 100% of the held-out Jest behavioral assertions (15/15) and all 7 Semantic Invariant Gates (21/21 across 3 tasks). One-shot context compilation cuts cumulative input tokens by 2.24× and file reads by 1.89×.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Suite-wide scorecard across 3 tasks">
          <div class="table-caption-bar">
            <span>Table 1: 3-Task Combined Totals (CodeExplorer vs Bare Agent, 6 Runs)</span>
            <span class="measured">15 Jest behavioral assertions, 7 SIG gates per task</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Metric (Sum of Tasks 1, 2 &amp; 3)</th>
                <th>Bare Agent (rg + read_file)</th>
                <th>CodeExplorer (1-Shot Context Pack)</th>
                <th>CodeExplorer Advantage</th>
              </tr>
            </thead>
            <tbody data-bind-html="cs09.suite_tbody">              <tr>
                <td><strong>Held-Out Jest Behavioral Specs</strong></td>
                <td class="cell-num">15 / 15 (100%)</td>
                <td class="cell-win">15 / 15 (100%)</td>
                <td class="cell-win">Parity (15/15 pass)</td>
              </tr>
              <tr>
                <td><strong>Architectural Gates (SIG-TWENTY-001..007)</strong></td>
                <td class="cell-num">21 / 21 (0&nbsp;new)</td>
                <td class="cell-win">21 / 21 (0&nbsp;new)</td>
                <td class="cell-win">0 new violations</td>
              </tr>
              <tr>
                <td><strong>LLM Turns</strong></td>
                <td class="cell-num">84&nbsp;turns</td>
                <td class="cell-win">59&nbsp;turns</td>
                <td class="cell-win">1.42× fewer turns (-30%)</td>
              </tr>
              <tr>
                <td><strong>Total Tool Calls</strong></td>
                <td class="cell-num">165&nbsp;calls</td>
                <td class="cell-win">95&nbsp;calls</td>
                <td class="cell-win">1.74× fewer calls (-42%)</td>
              </tr>
              <tr>
                <td><strong>Source File Reads (read_file)</strong></td>
                <td class="cell-num">104&nbsp;reads</td>
                <td class="cell-win">55&nbsp;reads</td>
                <td class="cell-win">1.89× fewer reads (-47%)</td>
              </tr>
              <tr>
                <td><strong>Shell Commands (run_command)</strong></td>
                <td class="cell-num">41&nbsp;commands</td>
                <td class="cell-win">20&nbsp;commands</td>
                <td class="cell-win">2.05× fewer commands (-51%)</td>
              </tr>
              <tr>
                <td><strong>Wall-Clock Time</strong></td>
                <td class="cell-num">1,682&nbsp;s (28m 02s)</td>
                <td class="cell-win">1,178&nbsp;s (19m 38s)</td>
                <td class="cell-win">1.43× faster (-8m 24s)</td>
              </tr>
              <tr>
                <td><strong>Uncached Input Tokens</strong></td>
                <td class="cell-num">491,682&nbsp;tokens</td>
                <td class="cell-win">316,255&nbsp;tokens</td>
                <td class="cell-win">1.55× fewer uncached (-36%)</td>
              </tr>
              <tr>
                <td><strong>Cumulative Input Tokens (Total)</strong></td>
                <td class="cell-num">6,897,615&nbsp;tokens</td>
                <td class="cell-win">3,080,276&nbsp;tokens</td>
                <td class="cell-win">2.24× fewer input tokens (-55%)</td>
              </tr>
              <tr>
                <td><strong>Output Tokens</strong></td>
                <td class="cell-num">61,150&nbsp;tokens</td>
                <td class="cell-win">41,472&nbsp;tokens</td>
                <td class="cell-win">1.47× fewer output tokens (-32%)</td>
              </tr></tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">4. Per-task breakdown across Tasks 1, 2, and 3.</h2>
          <p class="band-lede">
            Detailed telemetry for each paired run. In Task 2, CodeExplorer executed only 2 shell commands in the entire session (1 call to <code>/api/context</code> on Turn 1 and 1 verification call on the final turn, with 0 <code>ripgrep</code> invocations).
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Per-task breakdown">
          <div class="table-caption-bar">
            <span>Table 2: Per-Task Scorecard on twentyhq/twenty</span>
            <span class="measured">Paired runs on identical commit</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Task &amp; Requirement</th>
                <th>Agent</th>
                <th>Jest &amp; Gates</th>
                <th>Turns &amp; Tools (cmd/read/edit)</th>
                <th>Wall-Clock Time</th>
                <th>Uncached Input</th>
                <th>Cumulative Input</th>
                <th>Output Tokens</th>
              </tr>
            </thead>
            <tbody data-bind-html="cs09.tasks_tbody">              <tr>
                <td><strong>Task 1: API Key Revocation</strong><br><span class="tone-muted">Delete scoped usageLimit rows, recompute cache &amp; drop quota counters</span></td>
                <td><strong>CodeExplorer</strong></td>
                <td class="cell-win">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-win">16&nbsp;turns<br><span class="tone-muted">25&nbsp;tools (7/12/5)</span></td>
                <td class="cell-win">322&nbsp;s (5m 22s)</td>
                <td class="cell-win">77,390&nbsp;tokens</td>
                <td class="cell-win">772,814&nbsp;tokens<br><span class="tone-muted">2.85× fewer</span></td>
                <td class="cell-win">11,782&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 1: API Key Revocation</strong><br><span class="tone-muted">Baseline ripgrep + read_file</span></td>
                <td>Bare</td>
                <td class="cell-num">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-num">26&nbsp;turns<br><span class="tone-muted">55&nbsp;tools (11/39/4)</span></td>
                <td class="cell-num">587&nbsp;s (9m 47s)</td>
                <td class="cell-num">142,325&nbsp;tokens</td>
                <td class="cell-num">2,203,949&nbsp;tokens</td>
                <td class="cell-num">23,368&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 2: Application Deletion</strong><br><span class="tone-muted">Cascade publicDomain teardown + external DNS custom hostname removal</span></td>
                <td><strong>CodeExplorer</strong></td>
                <td class="cell-win">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-win">16&nbsp;turns<br><span class="tone-muted">28&nbsp;tools (2/19/6)</span></td>
                <td class="cell-win">252&nbsp;s (4m 12s)</td>
                <td class="cell-win">85,483&nbsp;tokens</td>
                <td class="cell-win">881,006&nbsp;tokens<br><span class="tone-muted">2.34× fewer</span></td>
                <td class="cell-win">8,811&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 2: Application Deletion</strong><br><span class="tone-muted">Baseline ripgrep + read_file</span></td>
                <td>Bare</td>
                <td class="cell-num">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-num">26&nbsp;turns<br><span class="tone-muted">48&nbsp;tools (15/26/6)</span></td>
                <td class="cell-num">425&nbsp;s (7m 05s)</td>
                <td class="cell-num">127,830&nbsp;tokens</td>
                <td class="cell-num">2,062,682&nbsp;tokens</td>
                <td class="cell-num">13,997&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 3: Subdomain Collisions</strong><br><span class="tone-muted">Prevent subdomain &amp; suggestion collisions with registered PublicDomain</span></td>
                <td><strong>CodeExplorer</strong></td>
                <td class="cell-win">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-win">27&nbsp;turns<br><span class="tone-muted">42&nbsp;tools (11/24/6)</span></td>
                <td class="cell-win">604&nbsp;s (10m 04s)</td>
                <td class="cell-win">153,382&nbsp;tokens</td>
                <td class="cell-win">1,426,456&nbsp;tokens<br><span class="tone-muted">1.84× fewer</span></td>
                <td class="cell-win">20,879&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 3: Subdomain Collisions</strong><br><span class="tone-muted">Baseline ripgrep + read_file</span></td>
                <td>Bare</td>
                <td class="cell-num">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-num">32&nbsp;turns<br><span class="tone-muted">62&nbsp;tools (15/39/7)</span></td>
                <td class="cell-num">670&nbsp;s (11m 10s)</td>
                <td class="cell-num">221,527&nbsp;tokens</td>
                <td class="cell-num">2,630,984&nbsp;tokens</td>
                <td class="cell-num">23,785&nbsp;tokens</td>
              </tr></tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band" id="prompts" style="border-bottom: none;">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">5. Verbatim zero-spoiler prompts &amp; compiled Turn-1 context pack.</h2>
          <p class="band-lede">
            Inspect the exact system prompts, zero-spoiler tickets, and the compiled Turn-1 context pack delivered by CodeExplorer.
          </p>
        </div>

        <div class="prompt-stack">
          <details class="prompt-drawer" open>
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge badge--accent">Turn 1 Output</code>
                <span>Compiled Context Pack for Task 1 (task-01-context.yaml)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/09-zero-spoiler-context-pack-vs-bare-agent/context-packs/task-01-context.yaml">query: api key revoke usage limit
anchors:
  - symbol: "MUTATION:createWorkspaceUsageLimit"
    kind: Endpoint
    location: "packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel-usage-limit.resolver.ts:64"
    concept: usageLimit
  - symbol: "MUTATION:updateWorkspaceUsageLimit"
    kind: Endpoint
    location: "packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel-usage-limit.resolver.ts:77"
    concept: usageLimit
  - symbol: ApiKeyService.revoke
    kind: Function
    location: "packages/twenty-server/src/engine/core-modules/api-key/services/api-key.service.ts:121"
    concept: revoke
  - symbol: "MUTATION:revokeApiKey"
    kind: Endpoint
    location: "packages/twenty-server/src/engine/core-modules/api-key/api-key.resolver.ts:118"
    concept: apiKey
  - symbol: SigningKeyUsage
    kind: Type
    location: "packages/twenty-server/src/engine/core-modules/jwt/services/signing-key-verify-counter.service.ts:21"
    concept: keyUsage
  - symbol: "MUTATION:assignRoleToApiKey"
    kind: Endpoint
    location: "packages/twenty-server/src/engine/core-modules/api-key/api-key.resolver.ts:128"
    concept: apiKey
  - symbol: "MUTATION:revokeSigningKey"
    kind: Endpoint
    location: "packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts:866"
    concept: key
  - symbol: key_column_usage
    kind: Table
    location: packages/twenty-server/src/engine/twenty-orm/workspace-schema-manager/services/workspace-schema-foreign-key-manager.service.ts
    concept: key
serviceMethods:
  - serviceOrClass: ApiKeyService
    filePath: packages/twenty-server/src/engine/core-modules/api-key/services/api-key.service.ts
    methods: [create (L34-L63), findById (L65-L72), findByWorkspaceId (L74-L78), findActiveByWorkspaceId (L80-L85), update (L87-L119), revoke (L121-L123), validateApiKey (L125-L156), generateApiKeyToken (L158-L192), isExpired (L194-L196), isRevoked (L198-L200)]
outboundSideEffects: ["ApiKeyService.create -&gt; RoleTargetService.create (packages/twenty-server/src/engine/metadata-modules/role-target/services/role-target.service.ts:22)", "ApiKeyService.generateApiKeyToken -&gt; JwtWrapperService.signAsyncOrThrow (packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts:50)", "UsageLimitService.create -&gt; WorkspaceCacheService.invalidateAndRecompute (packages/twenty-server/src/engine/workspace-cache/services/workspace-cache.service.ts:420)", "UsageLimitService.delete -&gt; WorkspaceCacheService.invalidateAndRecompute (packages/twenty-server/src/engine/workspace-cache/services/workspace-cache.service.ts:420)", "UsageLimitService.update -&gt; WorkspaceCacheService.invalidateAndRecompute (packages/twenty-server/src/engine/workspace-cache/services/workspace-cache.service.ts:420)"]
inboundCallersAndRoutes: ["GenerateApiKeyCommand.run -&gt; ApiKeyService.create (packages/twenty-server/src/engine/core-modules/api-key/commands/generate-api-key.command.ts:79)", "ApiKeyController.create -&gt; ApiKeyService.create (packages/twenty-server/src/engine/core-modules/api-key/controllers/api-key.controller.ts:74)", "ApiKeyResolver.createApiKey -&gt; ApiKeyService.create (packages/twenty-server/src/engine/core-modules/api-key/api-key.resolver.ts:77)", "ApiKeyController.findAll -&gt; ApiKeyService.findActiveByWorkspaceId (packages/twenty-server/src/engine/core-modules/api-key/controllers/api-key.controller.ts:48)", "ApiKeyResolver.apiKeys -&gt; ApiKeyService.findActiveByWorkspaceId (packages/twenty-server/src/engine/core-modules/api-key/api-key.resolver.ts:38)", "ApiKeyController.findOne -&gt; ApiKeyService.findById (packages/twenty-server/src/engine/core-modules/api-key/controllers/api-key.controller.ts:63)", "update -&gt; ApiKeyService.findById (packages/twenty-server/src/engine/core-modules/api-key/services/api-key.service.ts:87)", "validateApiKey -&gt; ApiKeyService.findById (packages/twenty-server/src/engine/core-modules/api-key/services/api-key.service.ts:125)"]
tablesAndCanonicalOwners:
  - table: apiKey
    canonicalOwner: ApiKeyService (packages/twenty-server/src/engine/core-modules/api-key/services/api-key.service.ts)
    ownerMethods: ["create [W] (L34)", "findById [R] (L65)", "findByWorkspaceId [R] (L74)", "findActiveByWorkspaceId [R] (L80)", "update [W] (L87)"]
    otherDirectAccessors: [ApiKeyRoleService (packages/twenty-server/src/engine/core-modules/api-key/services/api-key-role.service.ts), UsageLimitService (packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-limit.service.ts), UsageQuotaConsumptionService (packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-quota-consumption.service.ts)]
  - table: usageLimit
    canonicalOwner: UsageLimitService (packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-limit.service.ts)
    ownerMethods: ["findAll [R] (L59)", "create [RW] (L63)", "update [RW] (L102)", "assertScopeIsFree [R] (L185)", "delete [RW] (L254)"]
moduleWiringAndCycleSafety:
  owningModules:
    - module: ApiKeyModule
      filePath: packages/twenty-server/src/engine/core-modules/api-key/api-key.module.ts
      provides: [ApiKeyService]
      directModuleImports: [ApiKeyRoleModule, RoleTargetModule, TokenModule, WorkspaceCacheModule, WorkspaceCacheStorageModule]
    - module: UsageLimitModule
      filePath: packages/twenty-server/src/engine/core-modules/usage-limit/usage-limit.module.ts
      provides: [UsageLimitService]
      directModuleImports: [BillingModule, MetricsModule, PermissionsModule, ThrottlerModule, UsageAnalyticsModule, UsageModule, WorkspaceCacheModule]
  importSafetyVerdicts: ["SAFE_DIRECT_IMPORT: ApiKeyModule can import UsageLimitModule directly (0 reverse paths in module graph; no need to inspect other *.module.ts files).", "SAFE_DIRECT_IMPORT: UsageLimitModule can import ApiKeyModule directly (0 reverse paths in module graph; no need to inspect other *.module.ts files)."]
guardrails:
  suiteStatus: passed
  newViolationsCount: 0
  activeRules:
    - id: SIG-TWENTY-001
      status: PassedTolerated
      rule: Transport State Purity (No Direct DB Access in Resolvers/Controllers)
      remediation: Remove @InjectRepository / Repository / DataSource usage from the resolver or controller and delegate the persistence operation to the owning domain service.
    - id: SIG-TWENTY-002
      status: PassedTolerated
      rule: Metadata Table Ownership
      remediation: Import the owning metadata module and call its exported service instead of querying metadata TypeORM entities directly from core-modules.
    - id: SIG-TWENTY-003
      status: PassedTolerated
      rule: Core Domain &amp; Application Sub-Domain Table Ownership
      remediation: Do not inject another module's TypeORM Entity repository or query its table directly; import the owning NestJS module and call its exported domain service.
    - id: SIG-TWENTY-004
      status: PassedTolerated
      rule: Core-to-Feature Layer Isolation
      remediation: Decouple side effects from src/engine/core-modules/** via WorkspaceEventBatch / EventEmitter2 or MessageQueueService instead of calling src/modules/** directly.
    - id: SIG-TWENTY-005
      status: Passed
      rule: Async Queue Worker Boundary (No Direct Job .handle() Invocation)
      remediation: "Inject MessageQueueService via @InjectMessageQueue(MessageQueue.&lt;queue&gt;) and call .add&lt;JobData&gt;(JobClass.name, payload) instead of calling job.handle() synchronously."
    - id: SIG-TWENTY-006
      status: PassedTolerated
      rule: Transport Handler Thinness Ceiling (&lt;= 45 LOC)
      remediation: "Extract business, validation, and orchestration logic out of the resolver/controller method into a domain service so the transport method stays &lt;= 45 LOC."
    - id: SIG-TWENTY-007
      status: PassedTolerated
      rule: No Silent Exception Swallowing
      remediation: "Handle, rethrow, or log the caught error, or document why the exception is intentionally ignored."
  legacyTrapsInSubgraphFiles:
    - file: packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts
      gateId: SIG-TWENTY-001
      legacySymbols: [getAdminAiUsageByWorkspace]
      note: "TOLERATED LEGACY DEBT: do NOT imitate this pattern in new/modified methods; follow activeRules remediation."
    - file: packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-limit.service.ts
      gateId: SIG-TWENTY-002
      legacySymbols: [spenderExists]
      note: "TOLERATED LEGACY DEBT: do NOT imitate this pattern in new/modified methods; follow activeRules remediation."
    - file: packages/twenty-server/src/engine/core-modules/api-key/commands/generate-api-key.command.ts
      gateId: SIG-TWENTY-002
      legacySymbols: [run]
      note: "TOLERATED LEGACY DEBT: do NOT imitate this pattern in new/modified methods; follow activeRules remediation."
    - file: packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-limit.service.ts
      gateId: SIG-TWENTY-003
      legacySymbols: [spenderExists]
      note: "TOLERATED LEGACY DEBT: do NOT imitate this pattern in new/modified methods; follow activeRules remediation."
    - file: packages/twenty-server/src/engine/core-modules/api-key/commands/generate-api-key.command.ts
      gateId: SIG-TWENTY-003
      legacySymbols: [run]
      note: "TOLERATED LEGACY DEBT: do NOT imitate this pattern in new/modified methods; follow activeRules remediation."
    - file: packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts
      gateId: SIG-TWENTY-003
      legacySymbols: [getAdminAiUsageByWorkspace]
      note: "TOLERATED LEGACY DEBT: do NOT imitate this pattern in new/modified methods; follow activeRules remediation."
readPlan:
  - filePath: packages/twenty-server/src/engine/core-modules/api-key/services/api-key.service.ts
    startLine: 1
    reason: "Canonical owner of table 'apiKey': ApiKeyService"
  - filePath: packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-limit.service.ts
    startLine: 1
    reason: "Canonical owner of table 'usageLimit': UsageLimitService"
  - filePath: packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel-usage-limit.resolver.ts
    startLine: 64
    reason: "Anchor Endpoint: MUTATION:createWorkspaceUsageLimit"
  - filePath: packages/twenty-server/src/engine/core-modules/api-key/api-key.resolver.ts
    startLine: 118
    reason: "Anchor Endpoint: MUTATION:revokeApiKey"
  - filePath: packages/twenty-server/src/engine/core-modules/jwt/services/signing-key-verify-counter.service.ts
    startLine: 21
    reason: "Anchor Type: SigningKeyUsage"
  - filePath: packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts
    startLine: 866
    reason: "Anchor Endpoint: MUTATION:revokeSigningKey"</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge">System Prompt</code>
                <span>CodeExplorer Agent Instructions (ce-agent.md)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/09-zero-spoiler-context-pack-vs-bare-agent/prompts/ce-agent.md"># Agent System Instructions

You are a Senior Backend Engineer working inside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (\`packages/twenty-server\`).

## Strict Sandbox Rules
- Only read, search, edit, or create files inside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (excluding \`.codeexplorer/\`).
- Never read or access any path outside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (no access to parent benchmark folders, \`code-explorer\`, or \`antigravity/brain\`).
- Do not create \`.spec.ts\` or \`.test.ts\` files.

## Workspace Architectural Invariants (Strictly Enforced)
1. **Thin Transport Handlers (\`&lt;= 45 LOC\`, Zero Direct DB Access)**: GraphQL \`@Resolver\` and REST \`@Controller\` classes must keep handler methods \`&lt;= 45 LOC\` and must NEVER inject TypeORM \`@InjectRepository\` / \`Repository\` / \`DataSource\` directly — delegate persistence and domain logic to domain services.
2. **Strict Module Table Ownership**: Never inject another module's TypeORM Entity repository or query foreign domain/metadata tables directly across module boundaries (even if older legacy methods in the same file do so!). Import the owning NestJS module (avoiding circular NestJS module dependencies — extract a focused sub-module/service inside the owning domain if needed) and call its exported domain service so caches, counters, and external side-effects stay consistent.
3. **Async Queue &amp; Exception Hygiene**: Enqueue background jobs via \`MessageQueueService\` (never call job \`.handle()\` directly), and never leave empty \`catch\` blocks without handling/rethrowing/documenting.

## Workflow (Minimal Turns, Maximum Precision)
1. **Turn 1 — One-Shot Graph Context Pack**: Call the warm CodeExplorer context compiler ONCE via \`run_command\`:
   \`Invoke-RestMethod "http://127.0.0.1:19580/api/context?q=&lt;plus-separated-keywords&gt;"\`
   (Pass 3–6 domain keywords from the task, e.g. \`api+key+revoke+usage+limit\` or \`application+delete+public+domain+dns\` or \`subdomain+availability+public+domain\`).
   This returns in one shot:
   - \`anchors\`, \`serviceMethods\`, \`outboundSideEffects\`, \`inboundCallersAndRoutes\`, \`tablesAndCanonicalOwners\`
   - \`moduleWiringAndCycleSafety\`: lists the exact \`owningModules\` (\`*.module.ts\` file paths, \`provides\`, \`directModuleImports\`) and pre-computed \`importSafetyVerdicts\` (\`SAFE_DIRECT_IMPORT\` vs \`CYCLE_WARNING\` with the exact cycle path).
   - \`guardrails\` (\`activeRules\` and \`legacyTrapsInSubgraphFiles\`) and \`readPlan\`.
   **IMPORTANT**: Trust \`moduleWiringAndCycleSafety.importSafetyVerdicts\`! When it says \`SAFE_DIRECT_IMPORT\`, import the module directly without grepping or reading unrelated \`*.module.ts\` files across the repo. When it says \`CYCLE_WARNING\`, follow its guidance or extract a focused cycle-free sub-module.
2. **Turn 2 — Batch File Reads**: Read the files in \`readPlan\` and the 1–2 \`owningModules\` \`*.module.ts\` files in parallel \`read_file\` calls within a SINGLE turn. Only use \`rg\` if a specific constant/enum name is still needed.
3. **Implement**: Apply clean, minimal edits via \`edit_file\` / \`write_file\`.
4. **Final Turn — Single Verification Command**: Verify TypeScript compilation AND architectural gates in one command:
   \`&amp; "C:/Work/bench-pilot/repos/twenty-ctx-exp/node_modules/@typescript/native-preview-win32-x64/lib/tsgo.exe" -p "C:/Work/bench-pilot/repos/twenty-ctx-exp/packages/twenty-server/tsconfig.json" --declaration false --noEmit; Invoke-RestMethod "http://127.0.0.1:19580/api/gates/check"\`
   Ensure \`0\` TypeScript errors and \`newViolations: 0\` (\`status: passed\`), then return a concise summary.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge">System Prompt</code>
                <span>Bare Agent Baseline Instructions (bare-agent.md)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/09-zero-spoiler-context-pack-vs-bare-agent/prompts/bare-agent.md"># Agent System Instructions

You are a Senior Backend Engineer working inside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (\`packages/twenty-server\`).

## Strict Sandbox Rules
- Only read, search, edit, or create files inside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (excluding \`.codeexplorer/\`).
- Never read or access any path outside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (no access to parent benchmark folders, \`code-explorer\`, or \`antigravity/brain\`), and never connect to local ports or \`ce\` CLI.
- Do not create \`.spec.ts\` or \`.test.ts\` files.

## Workspace Architectural Invariants (Strictly Enforced)
1. **Thin Transport Handlers (\`&lt;= 45 LOC\`, Zero Direct DB Access)**: GraphQL \`@Resolver\` and REST \`@Controller\` classes must keep handler methods \`&lt;= 45 LOC\` and must NEVER inject TypeORM \`@InjectRepository\` / \`Repository\` / \`DataSource\` directly — delegate persistence and domain logic to domain services.
2. **Strict Module Table Ownership**: Never inject another module's TypeORM Entity repository or query foreign domain/metadata tables directly across module boundaries (even if older legacy methods in the same file do so!). Import the owning NestJS module (avoiding circular NestJS module dependencies — extract a focused sub-module/service inside the owning domain if needed) and call its exported domain service so caches, counters, and external side-effects stay consistent.
3. **Async Queue &amp; Exception Hygiene**: Enqueue background jobs via \`MessageQueueService\` (never call job \`.handle()\` directly), and never leave empty \`catch\` blocks without handling/rethrowing/documenting.

## Workflow (Minimal Turns, Maximum Precision)
1. Locate relevant modules and services in \`C:/Work/bench-pilot/repos/twenty-ctx-exp/packages/twenty-server/src\` using \`rg\` (\`run_command\`) and batch \`read_file\` calls in parallel where possible.
2. Implement clean, minimal edits via \`edit_file\` / \`write_file\`.
3. Verify TypeScript compilation cleanly before finishing:
   \`&amp; "C:/Work/bench-pilot/repos/twenty-ctx-exp/node_modules/@typescript/native-preview-win32-x64/lib/tsgo.exe" -p "C:/Work/bench-pilot/repos/twenty-ctx-exp/packages/twenty-server/tsconfig.json" --declaration false --noEmit\`
   Ensure \`0\` TypeScript errors, then return a concise summary.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge">Task 1 Prompt</code>
                <span>Zero-Spoiler Ticket: API Key Revocation Lifecycle (task-01-api-key-revoke.md)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/09-zero-spoiler-context-pack-vs-bare-agent/prompts/task-01-api-key-revoke.md"># Bug Report: API Key Revocation Leaves Scoped Usage Limits, Cache &amp; Counters Behind

Implement the fix in \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (\`packages/twenty-server\`).

## Problem Description
Workspaces can configure intra-workspace usage limits scoped to a specific API key (\`spenderType === 'apiKey'\` and \`spenderId === &lt;apiKeyId&gt;\`), including both workspace-admin limits (\`isInstanceOverride: false\`) and operator instance overrides (\`isInstanceOverride: true\`).

Currently, when a workspace API key is revoked in \`twenty-server\`, the API key record is marked as revoked (\`revokedAt\`), but all usage limits scoped to that API key remain persisted in the database, cached in the workspace cache, and active in usage counters.

## Required Behavior
Update the workspace API key revocation flow (taking an API key \`id\` and \`workspaceId\`) so that:
1. If no API key with \`id\` exists in \`workspaceId\`, return \`null\` without mutating any usage limits.
2. If the API key in \`workspaceId\` was already revoked prior to the call (\`revokedAt\` is already non-null), return the existing API key as-is without overwriting its original \`revokedAt\` timestamp or mutating usage limits.
3. When revoking a previously active (\`revokedAt == null\`) API key in \`workspaceId\`, mark it as revoked (\`revokedAt = new Date()\`) and remove all usage limits in \`workspaceId\` scoped to that API key (\`spenderType === 'apiKey'\` and \`spenderId === id\`, including both \`isInstanceOverride: false\` and \`isInstanceOverride: true\`) through Twenty's full usage-limit removal lifecycle (including workspace cache recomputation and dropping active usage-limit counters), while leaving usage limits belonging to other API keys, other spender types, or other workspaces untouched. Return the updated revoked API key.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge">Task 2 Prompt</code>
                <span>Zero-Spoiler Ticket: Application Deletion &amp; Public Domain DNS Teardown (task-02-app-delete-public-domains.md)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/09-zero-spoiler-context-pack-vs-bare-agent/prompts/task-02-app-delete-public-domains.md"># Bug Report: Deleting an Application Orphans Its Public Domains and External DNS Hostnames

Implement the fix in \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (\`packages/twenty-server\`).

## Problem Description
Custom applications installed in a workspace can own registered public domains (\`publicDomain\` rows linked by \`applicationId\` and \`workspaceId\`).

Currently, when a workspace application is deleted (by \`universalIdentifier\` and \`workspaceId\`) in \`twenty-server\`, any public domains owned by that application are left orphaned in external DNS infrastructure or removed without running Twenty's full public-domain teardown lifecycle.

## Required Behavior
Update the workspace application deletion flow (taking \`universalIdentifier\` and \`workspaceId\`) so that:
1. If no application with \`universalIdentifier\` exists in \`workspaceId\`, throw the existing \`APPLICATION_NOT_FOUND\` exception without deleting any public domains or calling external DNS teardown.
2. When deleting an existing application in \`workspaceId\`, remove all public domains owned by that application in \`workspaceId\` through Twenty's canonical public-domain removal lifecycle — including tearing down each domain's external custom hostname (\`{ isPublicDomain: true }\`) and recomputing the relevant workspace caches — while leaving public domains and DNS hostnames belonging to other applications or other workspaces untouched.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge">Task 3 Prompt</code>
                <span>Zero-Spoiler Ticket: Subdomain Validation &amp; Suggestions vs Public Domains (task-03-subdomain-collision.md)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/09-zero-spoiler-context-pack-vs-bare-agent/prompts/task-03-subdomain-collision.md"># Bug Report: Workspace Subdomain Validation &amp; Suggestions Allow Collisions With Registered Public Domains

Implement the fix in \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (\`packages/twenty-server\`).

## Problem Description
In multi-workspace mode, workspaces choose a \`subdomain\`, and Twenty also allows registering custom \`PublicDomain\` hostnames. When the server configuration setting \`PUBLIC_DOMAIN_URL\` is a valid URL (for example \`https://twenty.app\` or \`https://Twenty.App:443\`), a workspace \`&lt;subdomain&gt;\` derives the public hostname \`&lt;subdomain&gt;.&lt;publicBaseHostname&gt;\` (case-insensitive, stripping any port).

Currently, Twenty's workspace subdomain validation, availability check (\`checkWorkspaceSubdomainAvailability\`), and available-subdomain suggestion generator only check collisions against existing \`Workspace.subdomain\` rows — allowing a workspace to claim or be suggested a subdomain whose derived \`&lt;subdomain&gt;.&lt;publicBaseHostname&gt;\` collides with an already-registered \`PublicDomain\`!

## Required Behavior
Update Twenty's workspace subdomain validation, availability check, and suggestion generation logic so that whenever \`PUBLIC_DOMAIN_URL\` is configured as a valid URL with a non-empty hostname:
1. Any subdomain whose derived \`&lt;subdomain&gt;.&lt;publicBaseHostname&gt;\` (case-insensitive) matches a registered \`PublicDomain\` is treated as already taken:
   - Subdomain validation throws \`SUBDOMAIN_ALREADY_TAKEN\`.
   - Subdomain availability checks mark \`available: false\` (while keeping \`isValid: true\` for syntactically valid subdomains) and return \`3\` collision-free \`suggestedSubdomains\` (\`&lt;subdomain&gt;-2\`, \`&lt;subdomain&gt;-3\`, ... skipping any candidate taken by either \`Workspace.subdomain\` or a registered \`PublicDomain\`), with \`suggestedSubdomain\` set to the first suggestion.
   - Automatic available-subdomain selection skips candidates colliding with either \`Workspace.subdomain\` or a registered \`PublicDomain\`.
2. Non-colliding valid subdomains remain \`available: true\` with \`suggestedSubdomain: &lt;subdomain&gt;\` and \`suggestedSubdomains: [&lt;subdomain&gt;]\`.
3. When \`PUBLIC_DOMAIN_URL\` is unset, empty, or not a valid URL, skip the public-domain hostname collision check without throwing.</pre>
            </div>
          </details>
        </div>
      </div>
    </section>`},"/benchmarks/10-cqrs-read-write-invariants":{routePath:"/benchmarks/10-cqrs-read-write-invariants",pageKind:"benchmark-case",title:"Case Study 10: CQRS Read/Write Separation (CodeExplorer)",description:"Case Study 10: Verifying CQRS read/write separation across 13 open-source codebases (97 query handlers traced to SQL/ORM across 6 repos, 0 LLM tokens, $0.00) using CodeExplorer Semantic Invariant Gates (SIG-CQRS-001 to 003).",descriptionMetaBind:"cs10.meta_desc",canonical:"https://code-explorer.app/benchmarks/10-cqrs-read-write-invariants",ogUrl:"https://code-explorer.app/benchmarks/10-cqrs-read-write-invariants",ogTitle:"Case Study 10: Verifying CQRS Read/Write Separation Across Open-Source Codebases",ogDescription:"Case Study 10: Verifying CQRS read/write separation across 13 open-source codebases (97 query handlers traced to SQL/ORM across 6 repos, 0 LLM tokens, $0.00) using CodeExplorer Semantic Invariant Gates (SIG-CQRS-001 to 003).",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 10: CQRS Read/Write Separation</span>
          </div>
          <span class="measured" data-bind="cs10.crumb_scale">6 storage-traced repos (13 scanned), 97 query handlers</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 10: Verifying CQRS Read/Write Separation.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Most CQRS architecture tests check how classes and folders are named, not whether a read path mutates state. We ran three built-in Cypher gates across open-source .NET and TypeScript CQRS projects, tracing HTTP GET routes and QueryHandler contracts down to SQL and ORM writes.
            </p>
            <div class="hero-controls">
              <a href="#corpus-scorecard" class="btn btn--primary">View Corpus Scorecard</a>
              <a href="#verified-traces" class="btn">Source-Verified Call Chains</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-ok" data-bind="cs10.card_handlers">97</span>
            <h2 class="panel-title">Traced QueryHandlers</h2>
            <p class="panel-copy" data-bind-html="cs10.card_handlers_copy">Contract-identified QueryHandlers traced through DI and mediator dispatch down to SQL and ORM table operations across 6 repositories.</p>
            <code class="panel-code" data-bind="cs10.card_handlers_footer">6 of 13 repos reached SQL/ORM edges</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs10.card_clean">29 / 29</span>
            <h2 class="panel-title">Clean Reference Repo</h2>
            <p class="panel-copy" data-bind-html="cs10.card_clean_copy">In kgrzybek/modular-monolith-with-ddd, all 29 SQL-traced IQueryHandler classes resolved strictly to READS_FROM with 0 write paths.</p>
            <code class="panel-code" data-bind="cs10.card_clean_footer">4 of 6 storage-traced repos had 0 read writes</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs10.card_findings">2 + 8</span>
            <h2 class="panel-title">Read-Path Writes</h2>
            <p class="panel-copy" data-bind-html="cs10.card_findings_copy">Surfaced 2 QueryHandler write paths (SIG-CQRS-001) and 8 HTTP GET write paths (SIG-CQRS-003) invisible to ArchUnit and linters.</p>
            <code class="panel-code" data-bind="cs10.card_findings_footer">dotnet-starter-kit &amp; ever-gauzy verified in source</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs10.card_tokens">0</span>
            <h2 class="panel-title">Tokens Consumed</h2>
            <p class="panel-copy" data-bind-html="cs10.card_tokens_copy">0 LLM tokens ($0.00 cost). Evaluates 3 built-in declarative Cypher gates locally against SQLite graph.db in milliseconds.</p>
            <code class="panel-code" data-bind="cs10.card_tokens_footer">Deterministic CI exit codes + SHA-256 ratchet</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs10.participants_html">Evaluated system &amp; corpus configuration:<br>
          1. CodeExplorer (ce): v1.96.0 (.NET 11, SQLite + Cypher, built-in SIG-CQRS-001, SIG-CQRS-002 &amp; SIG-CQRS-003 starter gates, 0 tokens, $0.00)<br>
          2. Storage-Traced Corpus (6 repos): kgrzybek/modular-monolith-with-ddd @ 91c8ef2, fullstackhero/dotnet-starter-kit @ 2f5989b, ever-co/ever-gauzy @ 0dc826c7, jasontaylordev/CleanArchitecture @ 9e1042b, Sairyss/domain-driven-hexagon @ 5c2d15a, asc-lab/dotnetcore-microservices-poc @ 6500daa<br>
          3. Uncovered Driver / Non-Mediator Corpus (7 repos, reported transparently as vacuous): dotnet/eShop @ ab10e4c, Squidex/squidex @ 1c8a367, simplcommerce/SimplCommerce @ 3472ba0, BottegaIT/ddd-leaven-v2 @ 5225ff7, AleksK1NG/Go-CQRS-Microservices @ 70cda28, ThreeDotsLabs/wild-workouts-go-ddd-example @ 8ecfcdf, CodelyTV/php-ddd-example @ 9271c46</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Three behavioral CQRS rules checked along the call graph.</h2>
          <p class="band-lede">
            The indexer promotes CQRS contracts into Layer 4 Message nodes and links handlers via typed HANDLES_QUERY, HANDLES_COMMAND, and HANDLES_EVENT edges across .NET, TypeScript, Java, Go, PHP, Python, and Rust.
          </p>
        </div>

        <div class="panels panels--3">
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">QueryHandler No-Write Shield</h3>
              <code class="badge badge--win">SIG-CQRS-001</code>
            </div>
            <p class="panel-copy">No QueryHandler function may directly or transitively (0 to 4 call hops across injected services) mutate a table or publish an event.</p>
            <code class="panel-code">(Function)-[:HANDLES_QUERY]-&gt;(Message) + [:CALLS*0..4]-&gt;[:WRITES_TO]</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Query-to-Command Isolation</h3>
              <code class="badge badge--cool">SIG-CQRS-002</code>
            </div>
            <p class="panel-copy">Query handlers remain decoupled from command execution paths and never invoke or dispatch a CommandHandler.</p>
            <code class="panel-code">(Function)-[:HANDLES_QUERY] ... [:CALLS*1..3]-&gt;()-[:HANDLES_COMMAND]</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">HTTP GET Read-Only Guard</h3>
              <code class="badge badge--accent">SIG-CQRS-003</code>
            </div>
            <p class="panel-copy">HTTP GET endpoints stay idempotent across 0 to 5 downstream hops without triggering ORM inserts, SQL writes, or events.</p>
            <code class="panel-code">(Endpoint GET)-[:TRIGGERS]-&gt;()-[:CALLS*0..5]-&gt;()-[:WRITES_TO]-&gt;()</code>
          </div>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div id="corpus-scorecard" class="band-head">
          <h2 class="band-title">2. Results across open-source CQRS repositories.</h2>
          <p class="band-lede">
            Six of 13 scanned repositories had end-to-end SQL or ORM lineage from framework query handlers to database tables.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Storage-Traced CQRS Corpus (6 Repositories, 97 Traced QueryHandlers)</span>
            <span class="measured">Source: benchmarks/10-cqrs-read-write-invariants/results/metrics.json</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Repository</th>
                <th>Stack</th>
                <th>Scan</th>
                <th>Traced Queries</th>
                <th>READS / WRITES</th>
                <th>001 / 002 / 003</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong><a href="https://github.com/kgrzybek/modular-monolith-with-ddd/tree/91c8ef2" target="_blank" rel="noopener">modular-monolith</a></strong><br><span class="tone-muted">kgrzybek</span></td>
                <td>.NET, MediatR, Dapper, EF Core</td>
                <td class="cell-num">4.4&nbsp;s</td>
                <td class="cell-num">29 of 29</td>
                <td class="cell-num">228 / 302</td>
                <td class="cell-win"><code class="badge badge--win">0 / 0 / 0</code> (Read-only)</td>
              </tr>
              <tr>
                <td><strong><a href="https://github.com/fullstackhero/dotnet-starter-kit/tree/2f5989b243d702e0e2f089bd0258117da05f32e7" target="_blank" rel="noopener">dotnet-starter-kit</a></strong><br><span class="tone-muted">fullstackhero</span></td>
                <td>.NET, Mediator, EF Core</td>
                <td class="cell-num">12.0&nbsp;s</td>
                <td class="cell-num">59 of 71</td>
                <td class="cell-num">280 / 123</td>
                <td class="cell-loss"><code class="badge badge--accent">1 / 0 / 1</code> (Wallet insert)</td>
              </tr>
              <tr>
                <td><strong><a href="https://github.com/ever-co/ever-gauzy/tree/0dc826c76e04d6c8803e5933b398aeb4fed8b8b6" target="_blank" rel="noopener">ever-gauzy</a></strong><br><span class="tone-muted">ever-co</span></td>
                <td>TS, NestJS @nestjs/cqrs, TypeORM</td>
                <td class="cell-num">331.4&nbsp;s</td>
                <td class="cell-num">5 of 101</td>
                <td class="cell-num">27,743 / 34,344</td>
                <td class="cell-loss"><code class="badge badge--accent">1 / 0 / 7</code> (Status + init)</td>
              </tr>
              <tr>
                <td><strong><a href="https://github.com/jasontaylordev/CleanArchitecture/tree/9e1042b" target="_blank" rel="noopener">CleanArchitecture</a></strong><br><span class="tone-muted">jasontaylordev</span></td>
                <td>.NET, MediatR, EF Core</td>
                <td class="cell-num">3.4&nbsp;s</td>
                <td class="cell-num">1 of 2</td>
                <td class="cell-num">9 / 5</td>
                <td class="cell-win"><code class="badge badge--win">0 / 0 / 0</code> (Read-only)</td>
              </tr>
              <tr>
                <td><strong><a href="https://github.com/Sairyss/domain-driven-hexagon/tree/5c2d15a" target="_blank" rel="noopener">domain-hexagon</a></strong><br><span class="tone-muted">Sairyss</span></td>
                <td>TS, NestJS @nestjs/cqrs, Slonik</td>
                <td class="cell-num">2.1&nbsp;s</td>
                <td class="cell-num">1 of 1</td>
                <td class="cell-num">4 / 2</td>
                <td class="cell-win"><code class="badge badge--win">0 / 0 / 0</code> (Read-only)</td>
              </tr>
              <tr>
                <td><strong><a href="https://github.com/asc-lab/dotnetcore-microservices-poc/tree/6500daa" target="_blank" rel="noopener">microservices-poc</a></strong><br><span class="tone-muted">asc-lab</span></td>
                <td>.NET, MediatR, EF Core, Marten</td>
                <td class="cell-num">3.8&nbsp;s</td>
                <td class="cell-num">2 of 8</td>
                <td class="cell-num">4 / 2</td>
                <td class="cell-win"><code class="badge badge--win">0 / 0 / 0</code> (Read-only)</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);">
          <strong>Coverage boundary (remaining 7 repos):</strong> Across all 13 repos, CodeExplorer extracted 225 <code>HANDLES_QUERY</code>, 649 <code>HANDLES_COMMAND</code>, and 162 <code>HANDLES_EVENT</code> edges. Projects bypassing mediator queries (<code>eShop</code>, <code>squidex</code>, <code>SimplCommerce</code>, <code>ddd-leaven-v2</code>) or using unmodeled write sinks (Go <code>pgx</code>/Firestore, PHP Doctrine) yielded 0 DB-traced queries and are excluded from clean counts.
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div id="verified-traces" class="band-head">
          <h2 class="band-title">3. Source-verified call chains: deliberate shortcuts made explicit.</h2>
          <p class="band-lede">
            Both findings are intentional product decisions. Graph lineage makes their replica, retry, and concurrency tradeoffs explicit.
          </p>
        </div>

        <div class="panels panels--2">
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Case A: GET /wallet/me (dotnet-starter-kit)</h3>
              <code class="badge badge--accent">SIG-CQRS-001 &amp; 003</code>
            </div>
            <p class="panel-copy">
              1. <a href="https://github.com/fullstackhero/dotnet-starter-kit/blob/2f5989b243d702e0e2f089bd0258117da05f32e7/src/Modules/Billing/Modules.Billing/Features/v1/Wallets/GetMyWallet/GetMyWalletEndpoint.cs#L15-L17" target="_blank" rel="noopener">GetMyWalletEndpoint.cs:15</a> maps <code>GET /wallet/me</code> &rarr; <code>GetMyWalletQuery</code>.<br>
              2. <a href="https://github.com/fullstackhero/dotnet-starter-kit/blob/2f5989b243d702e0e2f089bd0258117da05f32e7/src/Modules/Billing/Modules.Billing/Features/v1/Wallets/GetMyWallet/GetMyWalletQueryHandler.cs#L12-L25" target="_blank" rel="noopener">GetMyWalletQueryHandler.cs:12</a> calls <code>IBillingService.GetOrCreateWalletAsync</code>.<br>
              3. <a href="https://github.com/fullstackhero/dotnet-starter-kit/blob/2f5989b243d702e0e2f089bd0258117da05f32e7/src/Modules/Billing/Modules.Billing/Services/BillingService.cs#L170-L181" target="_blank" rel="noopener">BillingService.cs:170</a> runs <code>_db.Wallets.Add</code> + <code>SaveChangesAsync</code>.<br><br>
              <strong>Tradeoff:</strong> Breaks read-replica routing and races on <code>ux_wallets_tenantid</code>.
            </p>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Case B: FindPublicInvoiceQuery (ever-gauzy)</h3>
              <code class="badge badge--accent">SIG-CQRS-001 &amp; 003</code>
            </div>
            <p class="panel-copy">
              1. <a href="https://github.com/ever-co/ever-gauzy/blob/0dc826c76e04d6c8803e5933b398aeb4fed8b8b6/packages/core/src/lib/public-share/invoice/public-invoice.controller.ts#L46-L52" target="_blank" rel="noopener">public-invoice.controller.ts:46</a> (plus PDF &amp; GraphQL) runs <code>FindPublicInvoiceQuery</code>.<br>
              2. <a href="https://github.com/ever-co/ever-gauzy/blob/0dc826c76e04d6c8803e5933b398aeb4fed8b8b6/packages/core/src/lib/public-share/invoice/queries/handlers/find-public-invoice.handler.ts#L6-L15" target="_blank" rel="noopener">find-public-invoice.handler.ts:6</a> calls <code>findOneByPublicLink</code>.<br>
              3. <a href="https://github.com/ever-co/ever-gauzy/blob/0dc826c76e04d6c8803e5933b398aeb4fed8b8b6/packages/core/src/lib/public-share/invoice/public-invoice.service.ts#L146-L170" target="_blank" rel="noopener">public-invoice.service.ts:146</a> calls <code>markViewed</code> &rarr; <code>repository.update</code>.<br><br>
              <strong>Plus 5 GET routes:</strong> <code>mintPlatformAddress</code> and <code>insertIgnore</code> on <code>/ever-stats/*</code>.
            </p>
          </div>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">4. Why dependency tests and linters miss multi-hop writes.</h2>
          <p class="band-lede">
            Static and runtime behavior when a write sits behind a DI interface.
          </p>
        </div>

        <div class="table-surface">
          <div class="table-caption-bar">
            <span>Tool Comparison on Multi-Hop Query Writes</span>
            <span class="measured">Scope &amp; coverage</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Approach</th>
                <th>Tools</th>
                <th>Scope</th>
                <th>Multi-Hop Query Write Coverage</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Dependency Rules</strong></td>
                <td><code>ArchUnit</code>, <code>NetArchTest</code></td>
                <td>Type imports</td>
                <td class="cell-loss">Stops at <code>IBillingService</code>; DI binds the writer at runtime.</td>
              </tr>
              <tr>
                <td><strong>File Linters</strong></td>
                <td><code>ESLint</code>, Roslyn, <code>oxlint</code></td>
                <td>Single file</td>
                <td class="cell-loss">Sees delegation only; ORM write sits 3 files away.</td>
              </tr>
              <tr>
                <td><strong>Custom SAST</strong></td>
                <td><code>CodeQL</code>, <code>Semgrep Pro</code></td>
                <td>Dataflow</td>
                <td class="cell-num">Needs custom rules per mediator and ORM sink.</td>
              </tr>
              <tr>
                <td><strong>Read-Only Tx</strong></td>
                <td>DB / EF pipeline</td>
                <td>Runtime SQL</td>
                <td class="cell-num">Catches only branches executed during tests.</td>
              </tr>
              <tr>
                <td><strong>Graph Gates</strong></td>
                <td><code>CodeExplorer</code></td>
                <td>Bus + DI + SQL</td>
                <td class="cell-win">Traces all branches statically with SHA-256 baselines.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band" style="border-bottom: none;">
      <div class="wrap">
        <div id="cypher-rules" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">
          <div class="prompt-stack-head">
            <h2 class="prompt-stack-title">5. Built-in Cypher gates &amp; CLI quickstart.</h2>
            <span class="prompt-stack-note">Expand for CLI commands or YAML rules</span>
          </div>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--win">Quickstart</span>
                <span>CLI commands (SIG-CQRS-001..003)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block"># 1. Index your workspace and generate starter gate templates in .codeexplorer/gates/
ce scan

# 2. Activate the three built-in CQRS gates
mv .codeexplorer/gates/cqrs-query-no-writes.yaml.example .codeexplorer/gates/cqrs-query-no-writes.yaml
mv .codeexplorer/gates/cqrs-no-query-to-command-calls.yaml.example .codeexplorer/gates/cqrs-no-query-to-command-calls.yaml
mv .codeexplorer/gates/cqrs-http-get-read-only.yaml.example .codeexplorer/gates/cqrs-http-get-read-only.yaml

# 3. Evaluate all gates (Exit 0 = clean, Exit 1 = unbaselined write path detected)
ce gate check
ce gate details

# Optional: freeze intentional GetOrCreate shortcuts into SHA-256 baselines while blocking future regressions
ce gate ratchet --update</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <span class="badge badge--cool">YAML Pack</span>
                <span>gates/cqrs-gates.yaml (SIG-CQRS-001..003)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block" data-bind-file="benchmarks/10-cqrs-read-write-invariants/gates/cqrs-gates.yaml"># ==============================================================================
# CodeExplorer Semantic Invariant Gates (SIG)
# Built-in CQRS Starter Suite (.codeexplorer/gates/)
# ==============================================================================

# --- Gate 1: Query Handlers Must Be Side-Effect Free ---
id: SIG-CQRS-001
category: Architectural
platform: agnostic
scope: Method
dimension: Responsibility
name: CQRS - Query Handlers Must Be Side-Effect Free
severity: Error
description: In Command Query Responsibility Segregation (CQRS), queries must only return data and never mutate database tables or publish events.
remediation: Move database writes and event publishing out of the query path into an explicit Command Handler.
target: "{w.name} -&gt; {target.name}"
message: "Query handler '{q.name}' ({msg.name}) in '{q.file_path}' triggers state mutation on '{target.name}' via '{w.name}'"
query: |
  MATCH (q:Function)-[:HANDLES_QUERY]-&gt;(msg:Message),
        (q)-[:CALLS*0..4]-&gt;(w:Function)-[r:WRITES_TO|PUBLISHES]-&gt;(target)
  WHERE NOT q.is_test AND NOT w.is_test
  RETURN DISTINCT q, msg, w, r, target
ratchet:
  enabled: true
  baseline: baselines/SIG-CQRS-001.json

---
# --- Gate 2: Queries Must Not Invoke Command Handlers ---
id: SIG-CQRS-002
category: Architectural
platform: agnostic
scope: Method
dimension: Boundaries
name: CQRS - Queries Must Not Invoke Command Handlers
severity: Error
description: Query handlers must remain strictly separated from command execution paths and never dispatch or invoke command handlers.
remediation: Decouple the read model from command execution; execute commands explicitly from the caller or application workflow.
target: "{q.name} -&gt; {c.name}"
message: "Query handler '{q.name}' ({qmsg.name}) in '{q.file_path}' invokes command handler '{c.name}' ({cmd.name}) in '{c.file_path}'"
query: |
  MATCH (q:Function)-[:HANDLES_QUERY]-&gt;(qmsg:Message),
        (q)-[:CALLS*1..3]-&gt;(c:Function)-[:HANDLES_COMMAND]-&gt;(cmd:Message)
  WHERE NOT q.is_test AND NOT c.is_test
  RETURN DISTINCT q, qmsg, c, cmd
ratchet:
  enabled: true
  baseline: baselines/SIG-CQRS-002.json

---
# --- Gate 3: HTTP GET Endpoints Must Be Read-Only ---
id: SIG-CQRS-003
category: Architectural
platform: agnostic
scope: Method
dimension: Boundaries
name: CQRS - HTTP GET Endpoints Must Be Read-Only
severity: Error
description: HTTP GET endpoints represent queries and must be safe and idempotent without mutating database tables or publishing domain/integration events.
remediation: Route state-changing operations through POST/PUT/PATCH/DELETE command endpoints and keep GET endpoints strictly read-only.
target: "{ep.name} -&gt; {w.name} -&gt; {target.name}"
message: "HTTP GET endpoint '{ep.name}' handled by '{fn.name}' in '{fn.file_path}' mutates '{target.name}' via '{w.name}'"
query: |
  MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]-&gt;(fn:Function)
        -[:CALLS*0..5]-&gt;(w:Function)-[r:WRITES_TO|PUBLISHES]-&gt;(target)
  WHERE ep.http_method = 'GET' AND NOT fn.is_test AND NOT w.is_test
  RETURN DISTINCT fn, ep, w, r, target
ratchet:
  enabled: true
  baseline: baselines/SIG-CQRS-003.json</pre>
            </div>
          </details>
        </div>
      </div>
    </section>`}};function hu(){if(typeof window>"u"||typeof document>"u")return()=>{};document.documentElement.classList.add("js");let d;function w(O){const V=document.getElementById("toast");V&&(V.textContent=O,V.classList.add("show"),clearTimeout(d),d=setTimeout(()=>V.classList.remove("show"),2e3))}function h(O,V="Copied to clipboard"){try{const pe=O.includes("brew ")?"/event/copy-brew":O.includes("mcp")?"/event/copy-mcp":"/event/copy-cli";navigator.sendBeacon&&navigator.sendBeacon(pe)}catch{}if(navigator.clipboard&&navigator.clipboard.writeText)navigator.clipboard.writeText(O).then(()=>w(V)).catch(()=>w("Select the command text to copy"));else{const pe=document.createElement("textarea");pe.value=O,document.body.appendChild(pe),pe.select(),document.execCommand("copy"),document.body.removeChild(pe),w(V)}}function Q(O){const V=document.getElementById("lightboxModal"),pe=document.getElementById("lightboxImg");!V||!pe||(pe.src=O,V.classList.add("open"))}function j(){const O=document.getElementById("lightboxModal");O&&O.classList.remove("open")}const x=document.getElementById("lightboxModal"),X=O=>{O.key==="Escape"&&j()};x&&(x.onclick=()=>j()),document.addEventListener("keydown",X),document.querySelectorAll("[data-lightbox-src]").forEach(O=>{O.onclick=()=>{const V=O.getAttribute("data-lightbox-src");V&&Q(V)}}),document.querySelectorAll("[data-copy]").forEach(O=>{O.onclick=()=>{const V=O.getAttribute("data-copy");V&&h(V,"Install command copied")}});function de(O,V){const pe=Array.from(O.querySelectorAll('[role="tab"]')),me=(Re,Ge)=>{pe.forEach(Pe=>{const We=Pe===Re;Pe.setAttribute("aria-selected",String(We)),Pe.classList.toggle("active",We),Pe.tabIndex=We?0:-1}),Ge&&Re.focus(),V(Re)};pe.forEach((Re,Ge)=>{Re.onclick=()=>me(Re,!1),Re.onkeydown=Pe=>{const We=O.getAttribute("aria-orientation")==="vertical",ct=We?"ArrowDown":"ArrowRight",Z=We?"ArrowUp":"ArrowLeft";let L=null;Pe.key===ct&&(L=(Ge+1)%pe.length),Pe.key===Z&&(L=(Ge-1+pe.length)%pe.length),Pe.key==="Home"&&(L=0),Pe.key==="End"&&(L=pe.length-1),L!==null&&(Pe.preventDefault(),me(pe[L],!0))}})}const te=document.querySelector(".page-home .reader-list");te&&de(te,O=>{te.querySelectorAll('[role="tab"]').forEach(V=>{const pe=V.getAttribute("aria-controls"),me=pe?document.getElementById(pe):null;me&&(me.hidden=V!==O)})});const ve=document.querySelector("[data-copy-mcp]");ve&&(ve.onclick=()=>{h(`{
  "mcpServers": {
    "code-explorer": {
      "command": "ce",
      "args": ["mcp"]
    }
  }
}`,"MCP server JSON copied")});const ke={blast_radius:{tag:"blast_radius.cypher",raw:'MATCH path = (caller:Function)-[:CALLS*1..4]->(target:Function {name: "ChargeAsync"}) RETURN caller.symbol AS UpstreamCaller, length(path) AS Hops ORDER BY Hops ASC LIMIT 10',codeHtml:`<span class="syn-kw">MATCH</span> path = (caller:Function)-[:CALLS*<span class="syn-num">1..4</span>]-&gt;
      (target:Function {name: <span class="syn-str">"ChargeAsync"</span>})
<span class="syn-kw">RETURN</span> caller.symbol <span class="syn-kw">AS</span> UpstreamCaller,
       <span class="syn-fn">length</span>(path) <span class="syn-kw">AS</span> Hops
<span class="syn-kw">ORDER BY</span> Hops <span class="syn-kw">ASC</span>
<span class="syn-kw">LIMIT</span> <span class="syn-num">10</span>`,explanation:"Finds upstream callers within 4 hops that reach PaymentProcessor.ChargeAsync.",timing:"4 rows in 3.8 ms",headers:["UpstreamCaller","Hops"],rows:[["Billing.Application.Commands.CaptureOrderHandler.Handle","1"],["Orders.Api.Controllers.CheckoutController.SubmitOrder","2"],["Subscriptions.Worker.Consumers.RenewalConsumer.Consume","2"],["Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute","3"]]},cqrs_pipeline:{tag:"cqrs_pipeline.cypher",raw:"MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(:Function)-[:CALLS*1..3]->(h:Function)-[:HANDLES_COMMAND]->(cmd:Message), (h)-[:WRITES_TO]->(t:Table) RETURN ep.name AS Route, cmd.name AS Command, h.name AS Handler, t.name AS Table",codeHtml:`<span class="syn-kw">MATCH</span> (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]-&gt;(:Function)-[:CALLS*<span class="syn-num">1..3</span>]-&gt;(h:Function)-[:HANDLES_COMMAND]-&gt;(cmd:Message),
      (h)-[:WRITES_TO]-&gt;(t:Table)
<span class="syn-kw">RETURN</span> ep.name <span class="syn-kw">AS</span> Route,
       cmd.name <span class="syn-kw">AS</span> Command,
       h.name <span class="syn-kw">AS</span> Handler,
       t.name <span class="syn-kw">AS</span> Table`,explanation:"Traces HTTP endpoints through CQRS command contracts to handler functions and mutated SQL tables.",timing:"3 rows in 4.2 ms",headers:["Route","Command","Handler","Table"],rows:[["POST /api/v1/orders","CreateOrderCommand","CreateOrderCommandHandler.Handle","purchase_orders"],["POST /api/v1/refunds","IssueRefundCommand","IssueRefundCommandHandler.Handle","ledger_entries"],["PUT /api/v1/inventory/reserve","ReserveStockCommand","ReserveStockHandler.Handle","stock_reservations"]]},api_to_db:{tag:"api_to_db.cypher",raw:'MATCH (s:Service)-[:IMPLEMENTED_BY]->(p:Project), (fn:Function)-[op:READS_FROM|WRITES_TO]->(tbl:Table {name: "customer_ledgers"}) WHERE fn.project_name = p.name RETURN s.name AS Service, fn.name AS Function, type(op) AS Operation',codeHtml:`<span class="syn-kw">MATCH</span> (s:Service)-[:IMPLEMENTED_BY]-&gt;(p:Project),
      (fn:Function)-[op:READS_FROM|WRITES_TO]-&gt;(tbl:Table {name: <span class="syn-str">"customer_ledgers"</span>})
<span class="syn-kw">WHERE</span> fn.project_name = p.name
<span class="syn-kw">RETURN</span> s.name <span class="syn-kw">AS</span> Service,
       fn.name <span class="syn-kw">AS</span> Function,
       <span class="syn-fn">type</span>(op) <span class="syn-kw">AS</span> Operation`,explanation:"Finds every service and function reading from or writing to a database table.",timing:"4 rows in 2.9 ms",headers:["Service","Function","Operation"],rows:[["Billing.Service","LedgerRepository.AppendEntryAsync","WRITES_TO"],["Billing.Service","BalanceQueryService.GetBalanceAsync","READS_FROM"],["Reporting.Worker","DailyReconciliationJob.ExportLedgers","READS_FROM"],["Fraud.Engine","RiskScorer.EvaluateRecentVelocity","READS_FROM"]]},cyclic_deps:{tag:"cyclic_deps.cypher",raw:"MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON*1..3]->(a) WHERE a.name <> b.name RETURN DISTINCT a.name AS ProjectA, b.name AS ProjectB",codeHtml:`<span class="syn-kw">MATCH</span> (a:Project)-[:DEPENDS_ON]-&gt;(b:Project)-[:DEPENDS_ON*<span class="syn-num">1..3</span>]-&gt;(a)
<span class="syn-kw">WHERE</span> a.name &lt;&gt; b.name
<span class="syn-kw">RETURN DISTINCT</span> a.name <span class="syn-kw">AS</span> ProjectA,
                b.name <span class="syn-kw">AS</span> ProjectB`,explanation:"Finds direct or multi-hop circular project dependencies across the workspace.",timing:"2 rows in 1.9 ms",headers:["ProjectA","ProjectB"],rows:[["Orders.Application","Shared.Fulfillment.Contracts"],["Identity.Core","Tenancy.Resolution"]]}},he=O=>O.replace(/[&<>"]/g,V=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"})[V]||V);let Ce="blast_radius";const W=document.querySelector('#playground [role="tablist"]');if(W){de(W,V=>{const pe=V.getAttribute("data-preset")||"blast_radius",me=ke[pe];if(!me)return;Ce=pe;const Re=document.getElementById("queryPresetTag"),Ge=document.getElementById("queryCodeArea"),Pe=document.getElementById("queryExplanation"),We=document.getElementById("queryTimingBadge"),ct=document.getElementById("queryResultsArea");if(Re&&(Re.textContent=me.tag),Ge&&(Ge.innerHTML=me.codeHtml),Pe&&(Pe.textContent=me.explanation),We&&(We.textContent=me.timing),ct){const Z=me.headers.map(we=>`<th>${he(we)}</th>`).join(""),L=me.rows.map(we=>`<tr>${we.map(Be=>`<td>${he(Be)}</td>`).join("")}</tr>`).join("");ct.innerHTML=`<table class="table"><thead><tr>${Z}</tr></thead><tbody>${L}</tbody></table>`}});const O=document.querySelector("[data-copy-query]");O&&(O.onclick=()=>{const V=ke[Ce];V&&h(`ce query '${V.raw}'`,"Cypher CLI command copied")})}const k={boundaries:{file:".codeexplorer/gates/domain-purity.yaml",description:"Blocks any type inside a Domain module from referencing Infrastructure or EF Core types.",status:"Exit 1 · 2 violations",statusOk:!1,rawYaml:`id: SIG-CA-B-001
name: Domain Layer Must Not Reference Infrastructure
severity: Error
target: "{inf.name}"
message: "Domain type '{d.name}' references infrastructure type '{inf.name}'"
query: |-
  MATCH (d:Type)-[:DEPENDS_ON|INHERITS|IMPLEMENTS]->(inf:Type)
  WHERE d.file_path CONTAINS "/Domain/"
    AND (inf.file_path CONTAINS "/Infrastructure/" OR inf.symbol CONTAINS "DbContext")
  RETURN d, inf`,yamlHtml:`<span class="syn-kw">id:</span> <span class="syn-str">SIG-CA-B-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">Domain Layer Must Not Reference Infrastructure</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">target:</span> <span class="syn-str">"{inf.name}"</span>
<span class="syn-kw">message:</span> <span class="syn-str">"Domain type '{d.name}' references infrastructure type '{inf.name}'"</span>
<span class="syn-kw">query:</span> |-
  <span class="syn-kw">MATCH</span> (d:Type)-[:DEPENDS_ON|INHERITS|IMPLEMENTS]-&gt;(inf:Type)
  <span class="syn-kw">WHERE</span> d.file_path <span class="syn-kw">CONTAINS</span> <span class="syn-str">"/Domain/"</span>
    <span class="syn-kw">AND</span> (inf.file_path <span class="syn-kw">CONTAINS</span> <span class="syn-str">"/Infrastructure/"</span>
      <span class="syn-kw">OR</span> inf.symbol <span class="syn-kw">CONTAINS</span> <span class="syn-str">"DbContext"</span>)
  <span class="syn-kw">RETURN</span> d, inf`,violations:[{rule:"SIG-CA-B-001 · Error",target:"Orders.Domain.Aggregates.Order -> Orders.Infrastructure.Persistence.OrderDbContext",loc:"src/Orders.Domain/Aggregates/Order.cs:84"},{rule:"SIG-CA-B-001 · Error",target:"Billing.Domain.Services.InvoiceCalculator -> Billing.Infrastructure.Http.TaxRateClient",loc:"src/Billing.Domain/Services/InvoiceCalculator.cs:41"}]},topology:{file:".codeexplorer/gates/no-shared-database-writes.yaml",description:"Prevents two distinct services from writing directly to the same SQL table.",status:"Exit 1 · 1 violation",statusOk:!1,rawYaml:`id: SIG-DDD-001
name: No Shared Table Writes Across Services
severity: Error
target: "{t.name}"
message: "Table '{t.name}' written by both '{s1.name}' and '{s2.name}'"
query: |-
  MATCH (s1:Service)-[:IMPLEMENTED_BY]->(p1:Project),
        (s2:Service)-[:IMPLEMENTED_BY]->(p2:Project),
        (w1:Function)-[:WRITES_TO]->(t:Table)<-[:WRITES_TO]-(w2:Function)
  WHERE s1.name <> s2.name AND w1.project_name = p1.name AND w2.project_name = p2.name
  RETURN DISTINCT w1, t, s1, w2, s2`,yamlHtml:`<span class="syn-kw">id:</span> <span class="syn-str">SIG-DDD-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Shared Table Writes Across Services</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">target:</span> <span class="syn-str">"{t.name}"</span>
<span class="syn-kw">message:</span> <span class="syn-str">"Table '{t.name}' written by both '{s1.name}' and '{s2.name}'"</span>
<span class="syn-kw">query:</span> |-
  <span class="syn-kw">MATCH</span> (s1:Service)-[:IMPLEMENTED_BY]-&gt;(p1:Project),
        (s2:Service)-[:IMPLEMENTED_BY]-&gt;(p2:Project),
        (w1:Function)-[:WRITES_TO]-&gt;(t:Table)&lt;-[:WRITES_TO]-(w2:Function)
  <span class="syn-kw">WHERE</span> s1.name &lt;&gt; s2.name
    <span class="syn-kw">AND</span> w1.project_name = p1.name <span class="syn-kw">AND</span> w2.project_name = p2.name
  <span class="syn-kw">RETURN DISTINCT</span> w1, t, s1, w2, s2`,violations:[{rule:"SIG-DDD-001 · Error",target:'Table "stock_items" written by both Checkout.Service and Warehouse.Service',loc:"src/Checkout.Service/Repositories/StockWriter.cs:59"}]},visitor:{file:".codeexplorer/gates/no-sync-over-async.yaml",description:"Catches blocking .Result or .Wait() calls inside async functions that starve thread pools.",status:"Exit 1 · 2 violations",statusOk:!1,rawYaml:`id: SIG-AST-012
name: No Blocking .Result or .Wait() Inside Async Functions
severity: Error
target: "{b.name}"
message: "Async function '{fn.name}' makes blocking call '{b.name}'"
query: |-
  MATCH (fn:Function {is_async: true})-[:CALLS]->(b:Function)
  WHERE b.name IN ["get_Result", "Wait", "GetResult"] AND NOT fn.is_test
  RETURN fn, b`,yamlHtml:`<span class="syn-kw">id:</span> <span class="syn-str">SIG-AST-012</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Blocking .Result or .Wait() Inside Async Functions</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">target:</span> <span class="syn-str">"{b.name}"</span>
<span class="syn-kw">message:</span> <span class="syn-str">"Async function '{fn.name}' makes blocking call '{b.name}'"</span>
<span class="syn-kw">query:</span> |-
  <span class="syn-kw">MATCH</span> (fn:Function {is_async: <span class="syn-num">true</span>})-[:CALLS]-&gt;(b:Function)
  <span class="syn-kw">WHERE</span> b.name <span class="syn-kw">IN</span> [<span class="syn-str">"get_Result"</span>, <span class="syn-str">"Wait"</span>, <span class="syn-str">"GetResult"</span>] <span class="syn-kw">AND NOT</span> fn.is_test
  <span class="syn-kw">RETURN</span> fn, b`,violations:[{rule:"SIG-AST-012 · Error",target:"Payments.Api.WebhookController.HandleStripeAsync calls Task.get_Result",loc:"src/Payments.Api/Controllers/WebhookController.cs:112"},{rule:"SIG-AST-012 · Error",target:"Notifications.Worker.EmailDispatcher.SendBatchAsync calls Task.Wait",loc:"src/Notifications.Worker/EmailDispatcher.cs:67"}]},literal:{file:".codeexplorer/gates/no-hardcoded-urls.yaml",description:"Flags hardcoded internal service URLs or connection strings in non-test functions.",status:"Exit 1 · 1 violation",statusOk:!1,rawYaml:`id: SIG-SEC-003
name: No Hardcoded Internal Service URLs Outside Config
severity: Error
target: "{lit.value}"
message: "Function '{fn.name}' passes hardcoded URL '{lit.value}'"
query: |-
  MATCH (fn:Function)-[:PASSES_LITERAL]->(lit:CallLiteral)
  WHERE lit.value STARTS WITH "http://" AND NOT fn.is_test
  RETURN fn, lit`,yamlHtml:`<span class="syn-kw">id:</span> <span class="syn-str">SIG-SEC-003</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Hardcoded Internal Service URLs Outside Config</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">target:</span> <span class="syn-str">"{lit.value}"</span>
<span class="syn-kw">message:</span> <span class="syn-str">"Function '{fn.name}' passes hardcoded URL '{lit.value}'"</span>
<span class="syn-kw">query:</span> |-
  <span class="syn-kw">MATCH</span> (fn:Function)-[:PASSES_LITERAL]-&gt;(lit:CallLiteral)
  <span class="syn-kw">WHERE</span> lit.value <span class="syn-kw">STARTS WITH</span> <span class="syn-str">"http://"</span>
    <span class="syn-kw">AND NOT</span> fn.is_test
  <span class="syn-kw">RETURN</span> fn, lit`,violations:[{rule:"SIG-SEC-003 · Error",target:'Shipping.Infrastructure.CarrierClient passes literal "http://carrier-internal.prod:8080/v2"',loc:"src/Shipping.Infrastructure/Clients/CarrierClient.cs:29"}]},ratchet:{file:".codeexplorer/baselines/SIG-CA-B-001.json",description:"Tolerates fingerprinted legacy violations while failing CI on any net-new violation.",status:"Exit 0 · 0 new, 2 tolerated",statusOk:!0,rawYaml:`{
  "gateId": "SIG-CA-B-001",
  "allowedViolations": 2,
  "entries": [
    {
      "fingerprint": "a5d81e9467416901e0131d28a725dff8e4ce2c55ca78f70a27ecc0269eec5afb",
      "file": "src/Orders.Domain/Aggregates/Order.cs",
      "line": 84,
      "symbol": "Order",
      "target": "OrderDbContext"
    },
    {
      "fingerprint": "15102b1a405cdd08a31e41205a2beabae835006a0a5eee9fd8be82de74409192",
      "file": "src/Billing.Domain/Services/InvoiceCalculator.cs",
      "line": 41,
      "symbol": "InvoiceCalculator",
      "target": "TaxRateClient"
    }
  ]
}`,yamlHtml:`{
  <span class="syn-kw">"gateId"</span>: <span class="syn-str">"SIG-CA-B-001"</span>,
  <span class="syn-kw">"allowedViolations"</span>: <span class="syn-num">2</span>,
  <span class="syn-kw">"entries"</span>: [
    {
      <span class="syn-kw">"fingerprint"</span>: <span class="syn-str">"a5d81e9467416901..."</span>,
      <span class="syn-kw">"file"</span>: <span class="syn-str">"src/Orders.Domain/Aggregates/Order.cs"</span>,
      <span class="syn-kw">"line"</span>: <span class="syn-num">84</span>,
      <span class="syn-kw">"symbol"</span>: <span class="syn-str">"Order"</span>,
      <span class="syn-kw">"target"</span>: <span class="syn-str">"OrderDbContext"</span>
    }
  ]
}`,violations:[{rule:"RATCHET PASS · 0 net-new violations",target:"2 existing legacy violations matched SHA-256 baseline fingerprints; 0 new violations introduced in PR.",loc:".codeexplorer/baselines/SIG-CA-B-001.json (verified in 6.4 ms)",ok:!0}]}};let be="boundaries";const Ae=document.querySelector('#gates [role="tablist"]');if(Ae){de(Ae,V=>{const pe=V.getAttribute("data-gate-preset")||"boundaries",me=k[pe];if(!me)return;be=pe;const Re=document.getElementById("gateFileLabel"),Ge=document.getElementById("gateYamlArea"),Pe=document.getElementById("gateDescriptionText"),We=document.getElementById("gateStatusBadge"),ct=document.getElementById("gateViolationsArea");Re&&(Re.textContent=me.file),Ge&&(Ge.innerHTML=me.yamlHtml),Pe&&(Pe.textContent=me.description),We&&(We.textContent=me.status,We.className=me.statusOk?"measured ok":"measured bad"),ct&&(ct.innerHTML=me.violations.map(Z=>`
          <div class="violation">
            <span class="violation-rule ${Z.ok?"ok":"bad"}">${he(Z.rule)}</span>
            <span class="violation-target">${he(Z.target)}</span>
            <span class="violation-loc">${he(Z.loc)}</span>
          </div>`).join(""))});const O=document.querySelector("[data-copy-gate]");O&&(O.onclick=()=>{const V=k[be];V&&h(V.rawYaml,"Gate configuration copied")})}const Oe=document.querySelector('#install [role="tablist"]');if(Oe){const O=document.getElementById("install-cmd"),V=document.getElementById("install-note");de(Oe,me=>{const Re=me.getAttribute("data-cmd")||"",Ge=me.getAttribute("data-note")||"";O&&(O.textContent=Re),V&&(V.textContent=Ge)});const pe=document.querySelector("[data-copy-install]");pe&&O&&(pe.onclick=()=>{h(O.textContent||"","Install command copied")})}const kt=document.querySelectorAll(".page-home .race");let at=null;return"IntersectionObserver"in window?(at=new IntersectionObserver(O=>{O.forEach(V=>{V.isIntersecting&&(V.target.classList.add("in-view"),at==null||at.unobserve(V.target))})},{threshold:.25}),kt.forEach(O=>at==null?void 0:at.observe(O))):kt.forEach(O=>O.classList.add("in-view")),()=>{document.removeEventListener("keydown",X),at&&at.disconnect()}}const gu="/",yu=()=>{lt.useEffect(()=>hu(),[]);const d=aa[gu];return l.jsx("main",{id:"main-content",className:"page-home",dangerouslySetInnerHTML:{__html:d.mainHtml}})},fu=()=>l.jsxs(l.Fragment,{children:[l.jsx("div",{className:"lightbox-modal",id:"lightboxModal",role:"dialog","aria-modal":"true","aria-label":"Architecture Cockpit screenshot preview",children:l.jsx("img",{id:"lightboxImg",src:"/all_services.webp?v=3",alt:"Enlarged CodeExplorer Architecture Cockpit view"})}),l.jsx("div",{className:"toast",id:"toast",role:"status","aria-live":"polite",children:"Copied to clipboard!"})]}),vd={agnostic:{label:"Agnostic",ext:".cs / .ts / .go",domain:"/Domain/",app:"/Application/",infra:"/Infrastructure/",ui:"/Api/",modPrefix:"Modules/",slicePrefix:"Features/"},dotnet:{label:"C# / .NET",ext:".cs",domain:".Domain/",app:".Application/",infra:".Infrastructure/",ui:".Api/",modPrefix:"src/Modules/",slicePrefix:"Features/"},typescript:{label:"TypeScript",ext:".ts",domain:"src/domain/",app:"src/application/",infra:"src/infrastructure/",ui:"src/controllers/",modPrefix:"packages/",slicePrefix:"src/features/"},go:{label:"Go",ext:".go",domain:"internal/domain/",app:"internal/usecase/",infra:"internal/adapter/repository/",ui:"internal/adapter/http/",modPrefix:"internal/modules/",slicePrefix:"internal/features/"},python:{label:"Python",ext:".py",domain:"/domain/",app:"/use_cases/",infra:"/adapters/orm/",ui:"/routers/",modPrefix:"apps/",slicePrefix:"features/"},java:{label:"Java / Kotlin",ext:".java",domain:"/domain/",app:"/application/",infra:"/infrastructure/",ui:"/web/",modPrefix:"modules/",slicePrefix:"features/"}},hs=[{slug:"clean-architecture",category:"structural",shortName:"Clean Architecture",fileName:"clean-architecture.arch",title:"Clean Architecture (Onion Dependency Rule)",tagline:"All source code dependencies point strictly inward toward pure Domain entities and policies.",coreInvariant:"Inner layers (Domain Core and Application Use Cases) define business rules and abstract ports. They must never know which database engine, ORM, HTTP framework, or message broker sits in the outer rings.",whyItMatters:["When Domain entities and Application use cases stay free of ORM annotations, DbContext references, and HTTP client SDKs, business rules can be tested in milliseconds in memory and evolved independently of database migrations or transport upgrades.","As soon as a single Domain service imports an Infrastructure class (or an API controller bypasses Application use cases to run raw SQL directly), the separation collapses: domain unit tests now require a live database container, and transaction boundaries scatter across controllers."],howItErodes:[{title:"AI Agent Copying a Convenience Helper into Domain",scenario:"When asked to calculate regional tax or validate a subscription limit inside a Domain aggregate, an AI agent searches for existing examples in the repo and imports TaxRateHttpClient or OrderDbContext straight into the Domain layer.",codeExample:`// [VIOLATION: SIG-CA-B-001] Inside src/Orders.Domain/Aggregates/Order.cs
public async Task RecalculateTotalsAsync(OrderDbContext db, TaxRateClient taxClient)
{
    // Domain aggregate couples directly to EF Core DbContext and outbound HTTP client
    var rate = await taxClient.GetRateAsync(this.BillingCountry);
    await db.SaveChangesAsync();
}`},{title:"Controller Bypassing Application Use Cases into SQL / ORM",scenario:"Under deadline pressure, an endpoint handler injects a repository or raw SQL connection directly into a controller action, skipping Application command validation, authorization policies, and domain events.",codeExample:`// [VIOLATION: SIG-CA-B-003] Inside src/Orders.Api/Controllers/OrdersController.cs
[HttpPost("{id}/cancel")]
public async Task<IActionResult> Cancel(Guid id, [FromServices] SqlConnection conn)
{
    // Bypasses CancelOrderCommandHandler and domain invariant checks
    await conn.ExecuteAsync("UPDATE orders SET status = 'cancelled' WHERE id = @id", new { id });
    return Ok();
}`}],whyGraphBeatsLinters:"Project-reference linters only see .csproj or package.json boundaries. In many repositories, layers live in folders within the same project, or leaks occur through transitive method calls and raw SQL queries (Layer 4 :Query and :Table nodes). CodeExplorer traces both AST call edges (-[:CALLS]->) and SQL dataflow (-[:ACCESSED_BY]->) across all five layers.",bestFit:["Long-lived enterprise backends with rich domain invariants and multiple workflows","Systems where business rules must be verified independently of persistence and transport","Brownfield monoliths being untangled with a SHA-256 baseline ratchet"],avoidWhen:["Thin CRUD microservices that only forward JSON payloads to a single database table","Short-lived prototypes or internal admin scripts with no domain logic"],graphLayersUsed:["L2 :Project","L3 :Type & :Function","L4 :Query & :Table"],layers:d=>[{name:"1. Presentation & Delivery (API / gRPC / CLI)",role:"HTTP controllers, GraphQL resolvers, gRPC services, consumer entrypoints",paths:`${d.ui}**`},{name:"2. Infrastructure & Persistence (ORM / SQL / SDKs)",role:"Database repositories, DbContext, external HTTP/RPC clients, broker adapters",paths:`${d.infra}**`},{name:"3. Application Use Cases (Commands / Queries / Ports)",role:"Use-case orchestration, command/query handlers, inbound/outbound port interfaces",paths:`${d.app}**`},{name:"4. Domain Core (Aggregates / Entities / Value Objects)",role:"Pure business invariants, domain events, and specifications (0 outward dependencies)",paths:`${d.domain}**`,core:!0}],edges:[{from:"Presentation",to:"Application Use Cases",allow:!0,code:"ALLOWED"},{from:"Infrastructure",to:"Application & Domain",allow:!0,code:"ALLOWED"},{from:"Domain Core",to:"Infrastructure or Presentation",allow:!1,code:"SIG-CA-B-001",gateId:"SIG-CA-B-001"},{from:"Application",to:"Concrete Infrastructure Adapter",allow:!1,code:"SIG-CA-B-002",gateId:"SIG-CA-B-002"},{from:"Presentation Controller",to:"Direct SQL / ORM Query",allow:!1,code:"SIG-CA-B-003",gateId:"SIG-CA-B-003"}],gates:(d,w)=>[{id:"SIG-CA-B-001",name:"Domain Core Purity",coords:"Module x Boundaries",pattern:"(Domain)-[:DEPENDS_ON|CALLS]->(Application|Infrastructure|Presentation)",severity:"Error",description:"Domain types and functions must never call or depend on Application, Infrastructure, or Presentation layers.",remediation:"Extract an interface (Port) in Domain or Application and inject the implementation from Infrastructure.",target:"{dst.name}",message:"Domain symbol '{src.name}' calls outer-layer symbol '{dst.name}'",query:`MATCH (src:Function)-[:CALLS]->(dst:Function)
  WHERE src.file_path CONTAINS '${d.domain}'
    AND (dst.file_path CONTAINS '${d.infra}' OR dst.file_path CONTAINS '${d.app}' OR dst.file_path CONTAINS '${d.ui}')
  RETURN src, dst`,platform:w,sampleViolation:{file:`src/Orders${d.domain}Aggregates/Order${d.ext.split(" ")[0]}`,line:84,symbol:"Order.RecalculateTotalsAsync",target:"TaxRateHttpClient.GetRateAsync",detail:"Domain aggregate invokes outbound infrastructure HTTP client directly."}},{id:"SIG-CA-B-002",name:"Application Layer Isolation",coords:"Module x Boundaries",pattern:"(Application)-[:DEPENDS_ON|CALLS]->(Infrastructure|Presentation)",severity:"Error",description:"Application use cases orchestrate Domain logic through abstract ports without importing concrete Infrastructure or Delivery adapters.",remediation:"Depend on an Application port interface (e.g. IOrderRepository) rather than the concrete SqlOrderRepository.",target:"{dst.name}",message:"Application use case '{src.name}' depends directly on adapter '{dst.name}'",query:`MATCH (src:Function)-[:CALLS]->(dst:Function)
  WHERE src.file_path CONTAINS '${d.app}'
    AND (dst.file_path CONTAINS '${d.infra}' OR dst.file_path CONTAINS '${d.ui}')
  RETURN src, dst`,platform:w,sampleViolation:{file:`src/Billing${d.app}Commands/CaptureInvoiceHandler${d.ext.split(" ")[0]}`,line:47,symbol:"CaptureInvoiceHandler.Handle",target:"StripeRestAdapter.ChargeCardAsync",detail:"Use-case handler calls concrete Stripe adapter instead of IPaymentGateway port."}},{id:"SIG-CA-B-003",name:"No Delivery-to-Database Bypass",coords:"Method x Boundaries",pattern:"(Endpoint|Controller)-[:CALLS|READS_FROM|WRITES_TO]->(Query|Table)",severity:"Error",description:"Delivery handlers must dispatch through Application use cases rather than issuing SQL or ORM queries directly.",remediation:"Move the database query into an Application query/command handler or repository.",target:"{q.name}",message:"Delivery handler '{fn.name}' executes database query '{q.name}' directly, bypassing Application layer",query:`MATCH (q:Query)-[:ACCESSED_BY]->(fn:Function)
  WHERE fn.file_path CONTAINS '${d.ui}'
  RETURN fn, q`,platform:w,sampleViolation:{file:`src/Orders${d.ui}OrdersController${d.ext.split(" ")[0]}`,line:118,symbol:"OrdersController.CancelOrder",target:"UPDATE orders SET status = ...",detail:"HTTP controller executes SQL UPDATE directly without dispatching CancelOrderCommand."}},{id:"SIG-CA-C-004",name:"Persistence-Ignorant Domain Entities",coords:"Class x Contract",pattern:"(Type {layer:'Domain'}) with ORM or HTTP transport annotations",severity:"Warning",description:"Domain entities should stay free of database table or HTTP serialization decorators.",remediation:"Use separate persistence configurations (e.g. IEntityTypeConfiguration) or DTOs in outer layers.",target:"{t.attributes}",message:"Domain type '{t.name}' carries outer framework annotation '{t.attributes}'",query:`MATCH (t:Type)
  WHERE t.file_path CONTAINS '${d.domain}'
    AND (coalesce(t.attributes, '') CONTAINS 'Table' OR coalesce(t.attributes, '') CONTAINS 'Entity' OR coalesce(t.attributes, '') CONTAINS 'JsonProperty')
  RETURN t`,platform:w,sampleViolation:{file:`src/Catalog${d.domain}Entities/Product${d.ext.split(" ")[0]}`,line:12,symbol:"Product",target:'[Table("catalog_products"), JsonProperty]',detail:"Domain entity is polluted with relational schema and JSON transport attributes."}},{id:"SIG-CA-B-005",name:"Acyclic Project Dependency Graph",coords:"Module x Boundaries",pattern:"(p1:Project)-[:DEPENDS_ON]->(p2:Project)-[:DEPENDS_ON]->(p1)",severity:"Error",description:"Projects must form a strict directed acyclic graph without mutual dependencies.",remediation:"Extract shared contracts or domain events into an inward contracts project to break the cycle.",target:"{p2.name}",message:"Circular project dependency between '{p1.name}' and '{p2.name}'",query:`MATCH (p1:Project)-[:DEPENDS_ON]->(p2:Project)-[:DEPENDS_ON]->(p1)
  WHERE p1.id < p2.id
  RETURN p1, p2`,platform:w,sampleViolation:{file:"src/Orders.Application/Orders.Application.csproj",line:1,symbol:"Orders.Application",target:"Fulfillment.Shared",detail:"Mutual project reference cycle between Orders.Application and Fulfillment.Shared."}}]},{slug:"hexagonal",category:"structural",shortName:"Hexagonal (Ports & Adapters)",fileName:"hexagonal-ports-adapters.arch",title:"Hexagonal Architecture (Ports & Adapters)",tagline:"Driving inbound adapters and driven outbound adapters meet exclusively through explicit Core Port interfaces.",coreInvariant:"A symmetrical boundary separates the Inside (Application & Domain Hexagon + Inbound/Outbound Ports) from the Outside (Driving HTTP/gRPC/Queue adapters and Driven SQL/Redis/SDK adapters). No adapter may ever call another adapter directly.",whyItMatters:["A service often starts with one REST API and one PostgreSQL database, then grows to support gRPC, Kafka consumers, CLI jobs, and third-party webhook callbacks. Hexagonal architecture ensures every entry trigger drives the exact same Inbound Port contract.","By forcing all external I/O behind Outbound Ports implemented by Driven Adapters, you can swap payment gateways, mock external APIs at the port boundary, and prevent vendor SDK types (AWS, Stripe, Twilio) from leaking into core domain models."],howItErodes:[{title:"Driving Adapter Short-Circuiting Directly into a Driven Adapter",scenario:"An HTTP webhook controller or Kafka consumer imports a Postgres repository or S3 client adapter directly instead of invoking an Inbound Port use case, splitting business validation across transport adapters.",codeExample:`// [VIOLATION: SIG-HEX-B-001] Driving HTTP adapter calls Driven Postgres adapter directly
public class StripeWebhookController : ControllerBase
{
    public async Task<IActionResult> Handle(PostgresSubscriptionAdapter repo)
    {
        // Bypasses IHandleSubscriptionRenewalUseCase inbound port
        await repo.MarkRenewedDirectlyAsync(Request.Body);
        return Ok();
    }
}`},{title:"Outbound HTTP / SDK Call Leaking Inside the Hexagon Core",scenario:"Instead of defining an outbound port interface (e.g. FraudScoringPort), a developer instantiates an HttpClient or external SDK call straight inside the application core.",codeExample:`// [VIOLATION: SIG-HEX-B-004] Outbound HTTP call declared inside Hexagon Core
public class CheckoutCoreService
{
    public async Task ApproveAsync(HttpClient http, Order order)
    {
        // Creates Layer 4 :ExternalService node inside /domain/ instead of /adapter/
        var score = await http.PostAsJsonAsync("https://fraud.internal/v1/score", order);
    }
}`}],whyGraphBeatsLinters:"CodeExplorer extracts Layer 3 interface realization edges (-[:IMPLEMENTS]->) and Layer 4 outbound calls (:ExternalService-[:DECLARED_IN]->:File). That means it can verify not just folder imports, but whether every concrete driven repository actually implements a Port interface and whether any outbound network call originates outside the adapter ring.",bestFit:["Services driven by multiple protocols (REST + gRPC + message queue consumers + CLI)","Domains integrating with volatile external APIs (payments, KYC, shipping, LLM providers)"],avoidWhen:["Simple single-protocol services where extra port/adapter indirection adds boilerplate without value"],graphLayersUsed:["L3 :Type-[:IMPLEMENTS]->:Type","L3 :Function-[:CALLS]->:Function","L4 :ExternalService"],layers:d=>[{name:"1. Driving Inbound Adapters (HTTP / gRPC / Consumers)",role:"Translates external requests/messages into Inbound Port calls",paths:`${d.ui}**`},{name:"2. Inbound & Outbound Ports (Hexagon Boundary)",role:"Use-case interfaces (Inbound) and SPI contracts for persistence/messaging (Outbound)",paths:`${d.app}ports/**`,core:!0},{name:"3. Hexagon Core (Domain & Application Logic)",role:"Implements Inbound Ports and orchestrates domain models via Outbound Ports",paths:`${d.domain}**`,core:!0},{name:"4. Driven Outbound Adapters (SQL / Redis / External SDKs)",role:"Concrete implementations of Outbound Ports talking to databases, brokers, and APIs",paths:`${d.infra}**`}],edges:[{from:"Driving Adapter",to:"Inbound Port Interface",allow:!0,code:"ALLOWED"},{from:"Driven Adapter",to:"Implements Outbound Port",allow:!0,code:"ALLOWED"},{from:"Driving Adapter",to:"Driven Adapter (Short-Circuit)",allow:!1,code:"SIG-HEX-B-001",gateId:"SIG-HEX-B-001"},{from:"Hexagon Core",to:"Concrete Driven Adapter",allow:!1,code:"SIG-HEX-B-002",gateId:"SIG-HEX-B-002"}],gates:(d,w)=>[{id:"SIG-HEX-B-001",name:"No Adapter-to-Adapter Short-Circuit",coords:"Method x Boundaries",pattern:"(DrivingAdapter)-[:CALLS]->(DrivenAdapter) bypassing Core Port",severity:"Error",description:"Inbound HTTP/gRPC/CLI adapters must never invoke outbound persistence or external SDK adapters directly.",remediation:"Invoke an Inbound Port use case in the Hexagon Core, which then calls the Outbound Port interface.",target:"{outbound.name}",message:"Inbound adapter '{inbound.name}' bypasses Core Port and calls outbound adapter '{outbound.name}'",query:`MATCH (inbound:Function)-[:CALLS]->(outbound:Function)
  WHERE inbound.file_path CONTAINS '${d.ui}' AND outbound.file_path CONTAINS '${d.infra}'
  RETURN inbound, outbound`,platform:w,sampleViolation:{file:`src/Payments${d.ui}StripeWebhookController${d.ext.split(" ")[0]}`,line:53,symbol:"StripeWebhookController.HandleWebhook",target:"PostgresSubscriptionAdapter.UpdateStatusAsync",detail:"Driving webhook controller bypasses Hexagon Core and calls driven Postgres adapter directly."}},{id:"SIG-HEX-B-002",name:"Hexagon Core Infrastructure Isolation",coords:"Module x Boundaries",pattern:"(Core)-[:CALLS]->(DrivenAdapter|ExternalService)",severity:"Error",description:"Domain and Port definitions inside the hexagon must never call concrete adapter implementations.",remediation:"Define an Outbound Port interface inside the hexagon and invert the dependency.",target:"{adapter.name}",message:"Hexagon core function '{core.name}' calls concrete driven adapter '{adapter.name}'",query:`MATCH (core:Function)-[:CALLS]->(adapter:Function)
  WHERE (core.file_path CONTAINS '${d.domain}' OR core.file_path CONTAINS '${d.app}')
    AND adapter.file_path CONTAINS '${d.infra}'
  RETURN core, adapter`,platform:w,sampleViolation:{file:`src/Checkout${d.domain}CheckoutOrchestrator${d.ext.split(" ")[0]}`,line:79,symbol:"CheckoutOrchestrator.PlaceOrderAsync",target:"KafkaProducerAdapter.PublishRawAsync",detail:"Core orchestrator calls concrete KafkaProducerAdapter instead of OrderEventOutputPort."}},{id:"SIG-HEX-C-003",name:"Driven Repository Implements Port Interface",coords:"Class x Contract",pattern:"Repository or Adapter class without IMPLEMENTS -> Port interface",severity:"Warning",description:"Every concrete outbound repository or client adapter must implement an explicit Port interface.",remediation:"Extract an Outbound Port interface in the Application layer and have the adapter implement it.",target:"PortInterface",message:"Driven adapter '{repo.name}' does not implement an outbound Port interface",query:`MATCH (repo:Type)
  WHERE repo.file_path CONTAINS '${d.infra}' AND repo.name ENDS WITH 'Repository'
    AND coalesce(repo.implements, '') = ''
  RETURN repo`,platform:w,sampleViolation:{file:`src/Inventory${d.infra}WarehouseSqlRepository${d.ext.split(" ")[0]}`,line:14,symbol:"WarehouseSqlRepository",target:"PortInterface",detail:"Concrete repository class does not implement any outbound Port interface."}},{id:"SIG-HEX-B-004",name:"External HTTP Calls Confined to Driven Adapters",coords:"Service x Boundaries",pattern:"(ExternalService)-[:DECLARED_IN]->(File outside Driven Adapters)",severity:"Error",description:"Outbound HTTP/RPC calls must originate strictly from driven adapter modules.",remediation:"Move the HTTP client invocation into a Driven Adapter implementing an Outbound Port.",target:"{es.url}",message:"External service call '{es.name}' is declared outside driven adapters in '{f.path}'",query:`MATCH (es:ExternalService)-[:DECLARED_IN]->(f:File)
  WHERE NOT f.path CONTAINS '${d.infra}'
  RETURN es, f`,platform:w,sampleViolation:{file:`src/Checkout${d.domain}FraudCheckService${d.ext.split(" ")[0]}`,line:38,symbol:"POST https://fraud.internal/v1/score",target:"https://fraud.internal/v1/score",detail:"Outbound HTTP call detected inside Domain Core outside driven adapters."}}]},{slug:"modular-monolith",category:"structural",shortName:"Modular Monolith",fileName:"modular-monolith.arch",title:"Modular Monolith (Strict Module & SQL Table Encapsulation)",tagline:"One deployable unit split into autonomous business modules that share neither internal classes nor database tables.",coreInvariant:"Every business module (Orders, Billing, Inventory, Identity) exposes only a narrow Public Contract (Facades, Commands, Integration Events) and owns its SQL tables exclusively. Cross-module calls into /Internal/ types or direct SQL queries against another module’s tables are forbidden.",whyItMatters:["A Modular Monolith gives teams the operational simplicity of a single deployment (no distributed transactions, network serialization, or Kubernetes service mesh overhead) while preserving clear domain boundaries.",'However, 90% of Modular Monoliths degenerate into a "Big Ball of Mud" because nothing stops Module A from querying or updating Module B’s SQL tables directly. Once two modules share mutable tables, neither can change its schema or scale out independently.'],howItErodes:[{title:"Cross-Module Direct Database Table Write or Join",scenario:"In Case Study 07 and 08 on twentyhq/twenty, when asked to implement an admin or billing feature, the AI agent saw a legacy shortcut in an adjacent service and injected the foreign module’s TypeORM repository directly to query/mutate its table instead of calling the owning module’s public service.",codeExample:`// [VIOLATION: SIG-MM-B-002] Billing module mutates Orders module table directly
public class InvoiceSettlementHandler
{
    public async Task SettleAsync(DbConnection db, Guid orderId)
    {
        // Table 'purchase_orders' is owned by the Orders module!
        await db.ExecuteAsync("UPDATE purchase_orders SET billing_settled = true WHERE id = @orderId");
    }
}`},{title:"Calling Another Module’s Internal Domain Entities or Repositories",scenario:"When modules live in the same repository, IDE auto-import happily imports Orders.Internal.OrderPricingEngine inside Billing, bypassing Orders.Contracts.",codeExample:`// [VIOLATION: SIG-MM-B-001] Cross-module call into /Internal/ namespace
using Modules.Orders.Internal.Domain; // Forbidden: must use Modules.Orders.Contracts

public class BillingDiscountCalculator
{
    public Money Apply(OrderInternalAggregate order) => order.ComputeRawMargin();
}`}],whyGraphBeatsLinters:"Import linters (eslint, NetArchTest, ArchUnit) cannot parse SQL strings or trace which module owns which database table. CodeExplorer builds Layer 4 :Table and :Query nodes with -[:READS_FROM]->, -[:WRITES_TO]->, and -[:OWNS]-> edges, catching cross-module table access even when done via raw SQL or Dapper/TypeORM repositories.",bestFit:["Product engineering teams that want strict Bounded Context isolation without microservice ops","Monoliths preparing to extract 1 or 2 high-load modules into standalone services later"],avoidWhen:["Subsystems require completely different runtimes or independent scaling / blast-radius isolation"],graphLayersUsed:["L2 :Project","L3 :Function-[:CALLS]->:Function","L4 :Query-[:READS_FROM|WRITES_TO]->:Table"],layers:d=>[{name:"1. Module Public Contracts (Facades / Commands / Events)",role:"The ONLY legal entry surface for other modules in the monolith",paths:`${d.modPrefix}*/Contracts/**`,core:!0},{name:"2. Module A Internals (Orders: Handlers, Domain, Persistence)",role:"Encapsulated domain model and repositories private to Orders",paths:`${d.modPrefix}Orders/Internal/**`},{name:"3. Module B Internals (Billing: Handlers, Domain, Persistence)",role:"Encapsulated domain model and repositories private to Billing",paths:`${d.modPrefix}Billing/Internal/**`},{name:"4. Module-Owned SQL Tables (orders_*, billing_*)",role:"Each SQL table has a single owning module for writes and reads",paths:"Layer 4 :Table ownership boundary"}],edges:[{from:"Module A",to:"Module B Public Contracts",allow:!0,code:"ALLOWED"},{from:"Module A",to:"Module A Owned SQL Tables",allow:!0,code:"ALLOWED"},{from:"Module A",to:"Module B Internal Types",allow:!1,code:"SIG-MM-B-001",gateId:"SIG-MM-B-001"},{from:"Module A",to:"Module B Owned SQL Tables",allow:!1,code:"SIG-MM-B-002",gateId:"SIG-MM-B-002"}],gates:(d,w)=>[{id:"SIG-MM-B-001",name:"No Cross-Module Internal Symbol Calls",coords:"Module x Boundaries",pattern:"(ModuleA)-[:CALLS]->(ModuleB/Internal) bypassing Contracts",severity:"Error",description:"Modules may only invoke another module through its Contracts or Public API namespace.",remediation:"Expose the required operation on the target module’s Public Contract facade or publish an integration event.",target:"{callee.name}",message:"Cross-module encapsulation breach: '{caller.project_name}' calls internal symbol '{callee.name}'",query:`MATCH (caller:Function)-[:CALLS]->(callee:Function)
  WHERE caller.project_name <> callee.project_name
    AND callee.file_path CONTAINS '${d.modPrefix}'
    AND (callee.file_path CONTAINS '/Internal/' OR callee.file_path CONTAINS '/Domain/' OR callee.file_path CONTAINS '/Persistence/')
  RETURN caller, callee`,platform:w,sampleViolation:{file:`${d.modPrefix}Billing/Internal/InvoiceService${d.ext.split(" ")[0]}`,line:64,symbol:"InvoiceService.GenerateAsync",target:"Orders.Internal.OrderPricingRepository.GetCostBreakdown",detail:"Billing module bypasses Orders.Contracts and calls internal OrderPricingRepository."}},{id:"SIG-MM-B-002",name:"Cross-Module Database Table Write Shield",coords:"Service x Boundaries",pattern:"(Two distinct Modules)<-[:DECLARED_IN]-(Query)-[:WRITES_TO]->(Shared Table)",severity:"Error",description:"Each database table must have a single owning module responsible for mutations.",remediation:"Send a Command or publish an Integration Event to the module that owns the table.",target:"{t.name}",message:"Table '{t.name}' is mutated by multiple modules: '{f1.project_name}' and '{f2.project_name}'",query:`MATCH (q1:Query)-[:WRITES_TO]->(t:Table)<-[:WRITES_TO]-(q2:Query)
  MATCH (q1)-[:ACCESSED_BY]->(f1:Function), (q2)-[:ACCESSED_BY]->(f2:Function)
  WHERE f1.project_name < f2.project_name
  RETURN f1, t, f2`,platform:w,sampleViolation:{file:`${d.modPrefix}Billing/Internal/SettlementWriter${d.ext.split(" ")[0]}`,line:92,symbol:"Modules.Billing",target:"purchase_orders (also written by Modules.Orders)",detail:"Table purchase_orders is written directly by both Modules.Orders and Modules.Billing."}},{id:"SIG-MM-B-003",name:"No Cross-Module Direct SQL Table Reads",coords:"Service x Boundaries",pattern:"(ModuleA Query)-[:READS_FROM]->(Table owned/written by ModuleB)",severity:"Warning",description:"Modules should read foreign module data via query facades or local read projections instead of direct SQL joins.",remediation:"Query the owning module’s public contract facade or maintain an event-driven local projection.",target:"{t.name}",message:"Module '{fr.project_name}' reads table '{t.name}' owned by module '{fw.project_name}'",query:`MATCH (qw:Query)-[:WRITES_TO]->(t:Table)<-[:READS_FROM]-(qr:Query)
  MATCH (qw)-[:ACCESSED_BY]->(fw:Function), (qr)-[:ACCESSED_BY]->(fr:Function)
  WHERE fw.project_name <> fr.project_name
  RETURN fr, t, fw`,platform:w,sampleViolation:{file:`${d.modPrefix}Shipping/Internal/ShipmentPlanner${d.ext.split(" ")[0]}`,line:41,symbol:"ShipmentPlanner.LoadCustomerTier",target:"customer_subscriptions (owned by Modules.Billing)",detail:"Shipping module executes SELECT against customer_subscriptions table owned by Billing."}},{id:"SIG-MM-B-004",name:"Shared Kernel Purity",coords:"Module x Boundaries",pattern:"(SharedKernel)-[:DEPENDS_ON|CALLS]->(BusinessModule)",severity:"Error",description:"Shared building blocks must never depend back on any concrete business module.",remediation:"Keep SharedKernel strictly generic; move domain-specific types into the owning business module.",target:"{pMod.name}",message:"Shared kernel '{pShared.name}' depends on business module '{pMod.name}'",query:`MATCH (pShared:Project)-[:DEPENDS_ON]->(pMod:Project)
  WHERE (pShared.name CONTAINS 'Shared' OR pShared.name CONTAINS 'Common' OR pShared.name CONTAINS 'BuildingBlocks')
    AND pMod.path CONTAINS '${d.modPrefix}'
  RETURN pShared, pMod`,platform:w,sampleViolation:{file:"src/BuildingBlocks.Common/BuildingBlocks.Common.csproj",line:1,symbol:"BuildingBlocks.Common",target:"Modules.Identity",detail:"Shared kernel project references concrete business module Modules.Identity."}},{id:"SIG-MM-B-005",name:"Acyclic Inter-Module Contracts",coords:"Module x Boundaries",pattern:"(ModA)-[:DEPENDS_ON]->(ModB)-[:DEPENDS_ON]->(ModA)",severity:"Error",description:"Module contract dependencies must remain strictly acyclic.",remediation:"Decouple one direction of the interaction using an asynchronous Integration Event.",target:"{m2.name}",message:"Bidirectional coupling between modules '{m1.name}' and '{m2.name}'",query:`MATCH (m1:Project)-[:DEPENDS_ON]->(m2:Project)-[:DEPENDS_ON]->(m1)
  WHERE m1.id < m2.id
  RETURN m1, m2`,platform:w,sampleViolation:{file:`${d.modPrefix}Orders/Orders.Contracts.csproj`,line:1,symbol:"Modules.Orders.Contracts",target:"Modules.Inventory.Contracts",detail:"Bidirectional contract dependency cycle between Orders and Inventory."}}]},{slug:"vertical-slice",category:"structural",shortName:"Vertical Slice",fileName:"vertical-slice.arch",title:"Vertical Slice Architecture (Zero Lateral Feature Coupling)",tagline:"Organize code around self-contained feature slices (Request -> Handler -> Storage) that never couple laterally.",coreInvariant:"Minimize coupling between features and maximize cohesion within a single feature. Each slice (e.g. Features/Orders/CreateOrder, Features/Orders/CancelOrder) owns its request contract, validation, handler, and data query, and must never import DTOs or handlers from a sibling slice.",whyItMatters:["In traditional layered architectures, adding or changing one endpoint requires editing 6 horizontal folders (Controller, Service, Interface, Repository, DTO, Mapper) where shared abstractions create unintended blast radius across unrelated features.","In Vertical Slice Architecture, changing CreateOrder touches only Features/Orders/CreateOrder. New engineers and AI coding agents can understand and modify a feature by reading a single folder—provided sibling slices stay 100% decoupled."],howItErodes:[{title:"AI Agent Reusing a Sibling Slice’s Request Validator or Helper",scenario:"When implementing Features/Orders/CloneOrder, an AI agent finds a convenient helper or response DTO inside Features/Orders/CreateOrder and imports it directly, silently coupling the two slices.",codeExample:`// [VIOLATION: SIG-VSA-B-001] Lateral call from CloneOrder slice into CreateOrder slice
namespace Features.Orders.CloneOrder;
using Features.Orders.CreateOrder; // Forbidden lateral slice coupling!

public class CloneOrderHandler
{
    public Task Handle(CloneOrderCommand cmd) => CreateOrderTaxHelper.ApplyAsync(cmd);
}`}],whyGraphBeatsLinters:"Because all slices live inside the same compilation project (e.g. Features/Orders/CreateOrder and Features/Orders/CancelOrder), compiler project boundaries cannot isolate them. CodeExplorer compares path segments on every Layer 3 -[:CALLS]-> edge to block lateral slice-to-slice dependencies automatically.",bestFit:["Feature-velocity product applications and MediatR / CQRS request-response APIs","Teams where each ticket maps to an isolated user story or API endpoint"],avoidWhen:["Deeply algorithmic engines (compilers, physics, trading matchers) with minimal endpoint surface"],graphLayersUsed:["L1 :Folder Slice","L3 :Function-[:CALLS]->:Function","L3 :Type"],layers:d=>[{name:"Slice A: CreateOrder (Endpoint + Command + Validator + SQL)",role:"Self-contained vertical feature slice for order creation",paths:`${d.slicePrefix}Orders/CreateOrder/**`},{name:"Slice B: CancelOrder (Endpoint + Command + Validator + SQL)",role:"Self-contained vertical feature slice for order cancellation",paths:`${d.slicePrefix}Orders/CancelOrder/**`},{name:"Slice C: IssueInvoice (Endpoint + Command + Validator + SQL)",role:"Self-contained vertical feature slice for invoicing",paths:`${d.slicePrefix}Billing/IssueInvoice/**`},{name:"Shared Domain & Infrastructure Primitives (Inward Only)",role:"Pure domain entities, auth context, and low-level DB session factory",paths:"Shared/**, Domain/**",core:!0}],edges:[{from:"Feature Slice A",to:"Shared Domain Primitives",allow:!0,code:"ALLOWED"},{from:"Feature Slice A",to:"Feature Slice B (Lateral Call)",allow:!1,code:"SIG-VSA-B-001",gateId:"SIG-VSA-B-001"},{from:"Shared Primitives",to:"Any Feature Slice",allow:!1,code:"SIG-VSA-B-002",gateId:"SIG-VSA-B-002"}],gates:(d,w)=>[{id:"SIG-VSA-B-001",name:"Zero Lateral Feature-to-Feature Calls",coords:"Module x Boundaries",pattern:"(Features/SliceA)-[:CALLS]->(Features/SliceB)",severity:"Error",description:"Feature slices must remain independent and never directly call handlers, DTOs, or helpers in sibling slices.",remediation:"If logic is genuinely domain-wide, promote it to Domain/ or Shared/; otherwise keep slice-specific copies.",target:"{b.name}",message:"Feature slice function '{a.name}' couples laterally to sibling slice symbol '{b.name}'",query:`MATCH (a:Function)-[:CALLS]->(b:Function)
  WHERE a.file_path CONTAINS '${d.slicePrefix}' AND b.file_path CONTAINS '${d.slicePrefix}'
    AND split(a.file_path, '/')[2] <> split(b.file_path, '/')[2]
  RETURN a, b`,platform:w,sampleViolation:{file:`${d.slicePrefix}Orders/CloneOrder/CloneOrderHandler${d.ext.split(" ")[0]}`,line:34,symbol:"CloneOrderHandler.Handle",target:"CreateOrderValidator.ValidateLineItems",detail:"CloneOrder slice calls CreateOrderValidator declared in sibling CreateOrder slice."}},{id:"SIG-VSA-B-002",name:"Shared Primitives Never Call Feature Slices",coords:"Module x Boundaries",pattern:"(Shared|Common|Domain)-[:CALLS]->(Features/*)",severity:"Error",description:"Shared infrastructure and domain primitives must never depend upward on specific feature slices.",remediation:"Invert the dependency or move the feature-specific logic back into its owning slice.",target:"{feat.name}",message:"Shared symbol '{sh.name}' depends upward on feature slice '{feat.name}'",query:`MATCH (sh:Function)-[:CALLS]->(feat:Function)
  WHERE NOT sh.file_path CONTAINS '${d.slicePrefix}' AND feat.file_path CONTAINS '${d.slicePrefix}'
    AND (sh.file_path CONTAINS 'Shared' OR sh.file_path CONTAINS 'Common' OR sh.file_path CONTAINS 'Domain')
  RETURN sh, feat`,platform:w,sampleViolation:{file:`src/Shared/Pipeline/AuditBehavior${d.ext.split(" ")[0]}`,line:51,symbol:"AuditBehavior.FormatPayload",target:"CreateOrderResponse.ToAuditSummary",detail:"Shared pipeline behavior imports type from Features/Orders/CreateOrder."}},{id:"SIG-VSA-R-003",name:"Single-Responsibility Slice Handler Size",coords:"Method x Resources",pattern:"Slice Handler method exceeding 150 LOC",severity:"Warning",description:"Keep vertical slice handlers concise by pushing complex business invariants into pure domain methods.",remediation:"Move domain state transitions onto the Domain Entity and keep the slice handler focused on I/O orchestration.",target:"{fn.loc} LOC",message:"Slice handler '{fn.name}' exceeds 150 LOC ceiling ({fn.loc} LOC)",query:`MATCH (fn:Function)
  WHERE fn.file_path CONTAINS '${d.slicePrefix}' AND coalesce(fn.loc, 0) > 150
  RETURN fn`,platform:w,sampleViolation:{file:`${d.slicePrefix}Checkout/SubmitOrder/SubmitOrderHandler${d.ext.split(" ")[0]}`,line:22,symbol:"SubmitOrderHandler.Handle",target:"214 LOC",detail:"Slice handler grew to 214 lines by inlining pricing and inventory allocation rules."}},{id:"SIG-VSA-C-004",name:"No Bloated Generic Repository in Vertical Slices",coords:"Class x Contract",pattern:"Generic Repository class (>15 methods) shared across slices",severity:"Warning",description:"Vertical slices favor direct query/command access over giant shared repository classes that couple all slices.",remediation:"Execute focused queries directly inside each slice or split the god-repository by aggregate.",target:"{methods} methods",message:"Shared repository '{t.name}' accumulates {methods} methods across slices",query:`MATCH (t:Type)-[:HAS_METHOD]->(m:Function)
  WHERE t.name ENDS WITH 'Repository'
  WITH t, count(m) AS methods WHERE methods > 15
  RETURN t, methods`,platform:w,sampleViolation:{file:`src/Shared/Persistence/OrderRepository${d.ext.split(" ")[0]}`,line:10,symbol:"OrderRepository",target:"28 methods",detail:"OrderRepository has accumulated 28 slice-specific query methods, becoming a central coupling bottleneck."}}]},{slug:"feature-sliced-design",category:"structural",shortName:"Feature-Sliced Design",fileName:"feature-sliced-design.arch",title:"Feature-Sliced Design (FSD Frontend Hierarchy)",tagline:"Strict unidirectional frontend hierarchy: app -> pages -> widgets -> features -> entities -> shared.",coreInvariant:"A module on layer N in a frontend codebase may only import from layers strictly below it (N-1 .. shared), and two slices on the same layer (e.g. features/auth and features/checkout) may never cross-import each other.",whyItMatters:['Large React, Next.js, and Vue codebases notoriously rot into circular component graphs where a "reusable" card in entities/user imports a modal from features/edit-profile, which imports a layout from widgets/header.',"Enforcing the FSD hierarchy guarantees that entities/ and shared/ remain 100% portable and side-effect free, while composition happens strictly upward in widgets/ and pages/."],howItErodes:[{title:"Entity Component Importing a Feature Action Button",scenario:'To render an "Add to Cart" button inside a ProductCard in src/entities/product, an engineer or AI agent imports AddToCartButton from src/features/cart—violating the downward FSD rule.',codeExample:`// [VIOLATION: SIG-FSD-B-001] Upward import from entities/ into features/
// File: src/entities/product/ui/ProductCard.tsx
import { AddToCartButton } from '@/features/cart/ui/AddToCartButton'; // Forbidden!
// Fix: Accept an action slot (renderAction / children) and compose in widgets/ProductCatalog`}],whyGraphBeatsLinters:"CodeExplorer resolves TypeScript path aliases (@/features/*, ~/*), barrel re-exports (index.ts), and actual function/component invocation edges (-[:CALLS]->) alongside Layer 4 outbound API calls (:ExternalService) so domain API endpoints cannot leak into src/shared.",bestFit:["Medium-to-large React, Next.js, Vue, or Angular applications with multiple product squads"],avoidWhen:["Small marketing sites or single-purpose micro-frontends with <20 components"],graphLayersUsed:["L1 :File & :Folder","L3 :Function-[:CALLS]->:Function","L4 :ExternalService"],layers:()=>[{name:"1. app & pages (Routing, Providers, Full Route Views)",role:"Top-level application shell and route pages composing widgets and features",paths:"src/app/**, src/pages/**"},{name:"2. widgets (Composite Page Blocks)",role:"Self-contained UI blocks assembling entities and interactive features",paths:"src/widgets/**"},{name:"3. features (User Interactions & Business Actions)",role:"User-facing actions with business value (AddToCart, AuthLogin, FilterOrders)",paths:"src/features/**"},{name:"4. entities & shared (Domain Models & Reusable UI Kit)",role:"Business data entities (User, Order) and domain-agnostic design system / utilities",paths:"src/entities/**, src/shared/**",core:!0}],edges:[{from:"pages & widgets",to:"features, entities, shared",allow:!0,code:"ALLOWED"},{from:"entities or shared",to:"features / widgets / pages",allow:!1,code:"SIG-FSD-B-001",gateId:"SIG-FSD-B-001"},{from:"features/SliceA",to:"features/SliceB (Lateral)",allow:!1,code:"SIG-FSD-B-002",gateId:"SIG-FSD-B-002"}],gates:(d,w)=>{const h=w==="agnostic"?"typescript":w;return[{id:"SIG-FSD-B-001",name:"No Upward Layer Dependencies in FSD",coords:"Module x Boundaries",pattern:"(shared|entities)-[:CALLS]->(features|widgets|pages|app)",severity:"Error",description:"Lower FSD layers (shared, entities) must never reference higher layers (features, widgets, pages, app).",remediation:"Pass higher-layer components down via React slots/children or render props from a widget or page.",target:"{high.file_path}",message:"Upward FSD layer violation: '{low.file_path}' calls '{high.file_path}'",query:`MATCH (low:Function)-[:CALLS]->(high:Function)
  WHERE (low.file_path CONTAINS 'src/shared/' OR low.file_path CONTAINS 'src/entities/')
    AND (high.file_path CONTAINS 'src/features/' OR high.file_path CONTAINS 'src/widgets/' OR high.file_path CONTAINS 'src/pages/' OR high.file_path CONTAINS 'src/app/')
  RETURN low, high`,platform:h,sampleViolation:{file:"src/entities/product/ui/ProductCard.tsx",line:27,symbol:"ProductCard",target:"src/features/cart/ui/AddToCartButton.tsx",detail:"Entity component ProductCard imports and renders feature component AddToCartButton."}},{id:"SIG-FSD-B-002",name:"No Cross-Imports Within Same FSD Layer",coords:"Module x Boundaries",pattern:"(src/features/Auth)-[:CALLS]->(src/features/Checkout)",severity:"Error",description:"Slices on the same FSD layer (features or entities) must not couple horizontally; compose them in widgets or pages.",remediation:"Lift the shared state or composition up to a widget in src/widgets/ or down to src/entities/.",target:"{b.file_path}",message:"Horizontal FSD slice coupling between '{a.file_path}' and '{b.file_path}'",query:`MATCH (a:Function)-[:CALLS]->(b:Function)
  WHERE a.file_path CONTAINS 'src/features/' AND b.file_path CONTAINS 'src/features/'
    AND split(a.file_path, '/')[2] <> split(b.file_path, '/')[2]
  RETURN a, b`,platform:h,sampleViolation:{file:"src/features/checkout/model/useCheckoutSubmit.ts",line:19,symbol:"useCheckoutSubmit",target:"src/features/promo-code/model/validatePromo.ts",detail:"Feature slice features/checkout directly calls sibling feature slice features/promo-code."}},{id:"SIG-FSD-B-003",name:"Features Must Not Depend on Widgets or Pages",coords:"Module x Boundaries",pattern:"(src/features/*)-[:CALLS]->(src/widgets/* | src/pages/*)",severity:"Error",description:"Features sit below widgets and pages and must never call upward into composite page blocks.",remediation:"Extract the reusable primitive into src/entities/ or src/shared/ui/.",target:"{up.file_path}",message:"Feature '{f.name}' calls upward FSD layer '{up.file_path}'",query:`MATCH (f:Function)-[:CALLS]->(up:Function)
  WHERE f.file_path CONTAINS 'src/features/'
    AND (up.file_path CONTAINS 'src/widgets/' OR up.file_path CONTAINS 'src/pages/')
  RETURN f, up`,platform:h,sampleViolation:{file:"src/features/search/ui/QuickSearchModal.tsx",line:42,symbol:"QuickSearchModal",target:"src/widgets/header/model/useHeaderState.ts",detail:"Feature component reaches upward into widget state hook useHeaderState."}},{id:"SIG-FSD-C-004",name:"Shared Layer Business-Entity Agnosticism",coords:"Module x Contract",pattern:"src/shared/** referencing domain API endpoints directly",severity:"Warning",description:"src/shared should provide generic UI/lib primitives rather than domain-specific API calls.",remediation:"Move domain-specific endpoint requests to src/entities/<domain>/api/ or src/features/<feature>/api/.",target:"{es.url}",message:"Domain network call '{es.name}' leaked into generic src/shared layer ('{f.path}')",query:`MATCH (es:ExternalService)-[:DECLARED_IN]->(f:File)
  WHERE f.path CONTAINS 'src/shared/' AND NOT f.path CONTAINS 'src/shared/api/'
  RETURN es, f`,platform:h,sampleViolation:{file:"src/shared/ui/DataTable/DataTable.tsx",line:88,symbol:"GET /api/v1/billing/invoices",target:"/api/v1/billing/invoices",detail:"Generic UI component in src/shared/ui fetches a domain-specific billing endpoint directly."}}]}},{slug:"microservices-isolation",category:"structural",shortName:"Shared-Nothing Microservices",fileName:"microservices-isolation.arch",title:"Shared-Nothing Microservices (Database & Sync Cycle Guard)",tagline:"Every microservice owns its database tables exclusively and avoids synchronous distributed call cycles.",coreInvariant:"Two distinct deployable services (:Service / :App) must never read or write the same database :Table, must never import each other’s source projects directly, and must never form a synchronous HTTP/gRPC call cycle.",whyItMatters:['A "distributed monolith" is the worst of both worlds: you pay the full latency, operational complexity, and partial-failure cost of microservices, but you cannot deploy Service A without breaking Service B because both services query and mutate the same PostgreSQL tables.','Furthermore, synchronous HTTP/gRPC cycles (Service A -> Service B -> Service A) or sequential outbound RPC calls inside a loop (:Scope {kind: "loop"}) cause cascading thread-pool exhaustion during traffic spikes.'],howItErodes:[{title:"Two Polyglot Microservices Accessing the Same SQL Table",scenario:"A Go worker and a TypeScript NestJS API sit in the same monorepo (or multi-repo workspace). Instead of calling the API or consuming a Kafka topic, the Go worker runs a direct SELECT/UPDATE against the users_ledger table.",codeExample:`// [VIOLATION: SIG-MS-B-001] Go worker and TS service both access table 'users_ledger'
// Go worker (services/settlement-worker/store.go):
db.ExecContext(ctx, "UPDATE users_ledger SET balance = balance - $1 WHERE user_id = $2", amt, uid)
// TS service (services/ledger-api/src/ledger.repository.ts):
await this.repo.save(UsersLedgerEntity); // Shared database anti-pattern!`},{title:"N+1 Outbound HTTP / gRPC Call Inside a Loop Scope",scenario:"To enrich a list of 100 orders with customer profiles, an endpoint iterates over the orders in a loop and awaits an outbound HTTP call on each iteration instead of calling a batch endpoint.",codeExample:`// [VIOLATION: SIG-MS-R-004] Outbound RPC inside a Layer 3 Loop Scope
foreach (var order in orders) // Layer 3 :Scope { kind: 'loop', loop_depth: 1 }
{
    // Sequential O(N) network waterfall across services
    order.Customer = await _customerHttpClient.GetProfileAsync(order.CustomerId);
}`}],whyGraphBeatsLinters:"No single-language linter can see across a Go service, a .NET service, a Python FastAPI service, and a PostgreSQL schema at the same time. CodeExplorer indexes all 12 languages into one graph, linking polyglot services to shared :Table nodes, :Topic brokers, and Layer 3 :Scope loop blocks.",bestFit:["Polyglot microservice monorepos and multi-repo enterprise workspaces","Distributed systems enforcing strict database-per-service ownership"],avoidWhen:["Single-service monoliths intentionally sharing a unified relational schema"],graphLayersUsed:['L3 :Scope {kind:"loop"}',"L4 :Service, :Table & :ExternalService","L5 :SERVICE_CALL"],layers:()=>[{name:"1. Edge API Gateway / BFF",role:"Routes external traffic and aggregates read views via non-blocking fan-out",paths:"services/gateway/**, apps/bff/**"},{name:"2. Autonomous Microservices (Orders, Payments, Inventory)",role:"Independently deployable services with zero cross-service code imports",paths:"services/*/**",core:!0},{name:"3. Async Event Broker (Kafka / RabbitMQ / PubSub Topics)",role:"Decoupled event choreography and eventual consistency contracts",paths:"Layer 4 :Topic contracts"},{name:"4. Isolated Per-Service Databases",role:"Strict 1-to-1 ownership between a microservice and its SQL tables",paths:"Layer 4 :Table & :Database"}],edges:[{from:"Service A",to:"Broker Topic -> Service B",allow:!0,code:"ALLOWED"},{from:"Service A",to:"Service A Dedicated Tables",allow:!0,code:"ALLOWED"},{from:"Service A & Service B",to:"Same SQL Table (Shared DB)",allow:!1,code:"SIG-MS-B-001",gateId:"SIG-MS-B-001"},{from:"Service A -> Service B",to:"Service A (Sync RPC Cycle)",allow:!1,code:"SIG-MS-B-002",gateId:"SIG-MS-B-002"}],gates:(d,w)=>[{id:"SIG-MS-B-001",name:"Shared-Database Integration Anti-Pattern",coords:"System x Boundaries",pattern:"(ServiceA) and (ServiceB) both READS_FROM or WRITES_TO (Table)",severity:"Error",description:"No database table may be accessed directly by more than one microservice.",remediation:"Route cross-service data access through an API contract or replicate state via broker events.",target:"{t.name}",message:"Shared database coupling on table '{t.name}' between '{f1.project_name}' and '{f2.project_name}'",query:`MATCH (q1:Query)-[:READS_FROM|WRITES_TO]->(t:Table)<-[:READS_FROM|WRITES_TO]-(q2:Query)
  MATCH (q1)-[:ACCESSED_BY]->(f1:Function), (q2)-[:ACCESSED_BY]->(f2:Function)
  WHERE f1.project_name < f2.project_name
  RETURN f1, t, f2`,platform:w,sampleViolation:{file:"services/settlement-worker/internal/store/ledger.go",line:74,symbol:"settlement-worker & ledger-api",target:"users_ledger",detail:"Table users_ledger is directly queried/mutated by both settlement-worker (Go) and ledger-api (TS)."}},{id:"SIG-MS-B-002",name:"Distributed Synchronous Call Cycle Shield",coords:"System x Boundaries",pattern:"(SvcA)-[:SERVICE_CALL]->(SvcB)-[:SERVICE_CALL]->(SvcA)",severity:"Error",description:"Synchronous HTTP/gRPC service-to-service calls must never form distributed cycles.",remediation:"Break the synchronous cycle by converting one direction into an asynchronous event publication.",target:"{b.name}",message:"Distributed synchronous call cycle between '{a.name}' and '{b.name}'",query:`MATCH (a:App)-[:SERVICE_CALL]->(b:App)-[:SERVICE_CALL]->(a)
  WHERE a.id < b.id
  RETURN a, b`,platform:w,sampleViolation:{file:"services/orders-api/Clients/InventoryGrpcClient.cs",line:42,symbol:"orders-api",target:"inventory-api",detail:"Synchronous RPC cycle: orders-api -> inventory-api -> orders-api."}},{id:"SIG-MS-B-003",name:"No Direct Cross-Service Source Code Imports",coords:"Service x Boundaries",pattern:"(ServiceA:App)-[:DEPENDS_ON]->(ServiceB:App)",severity:"Error",description:"Deployable microservices may depend on shared libraries, never directly on another deployable service.",remediation:"Extract shared DTOs or proto contracts into a dedicated contracts library.",target:"{p2.name}",message:"Deployable service '{p1.name}' directly imports deployable service '{p2.name}'",query:`MATCH (s1:App)-[:IMPLEMENTED_BY]->(p1:Project)-[:DEPENDS_ON]->(p2:Project)<-[:IMPLEMENTED_BY]-(s2:App)
  WHERE p1.id <> p2.id
  RETURN p1, p2`,platform:w,sampleViolation:{file:"services/billing-worker/billing-worker.csproj",line:1,symbol:"billing-worker",target:"orders-api",detail:"Deployable worker billing-worker directly references executable service orders-api."}},{id:"SIG-MS-R-004",name:"N+1 Synchronous HTTP Call Inside Loop Scope",coords:"Method x Resources",pattern:'(Function)-[:DECLARES]->(Scope {kind:"loop"}) with outbound ExternalService call',severity:"Warning",description:"Avoid sequential cross-service HTTP/RPC calls inside loops; prefer batch endpoints or bounded concurrency.",remediation:"Replace the per-item RPC call inside the loop with a batch endpoint request outside the loop.",target:"{es.name}",message:"Outbound RPC call '{es.name}' in '{fn.name}' executes inside a loop scope",query:`MATCH (fn:Function)-[:DECLARES]->(s:Scope {kind: 'loop'}),
        (fn)-[:CALLS]->(es:ExternalService)
  RETURN fn, s, es`,platform:w,sampleViolation:{file:"services/orders-bff/Handlers/OrderFeedHandler.cs",line:68,symbol:"OrderFeedHandler.EnrichOrdersAsync",target:"GET http://customer-service/api/v1/profiles/{id}",detail:"Sequential outbound HTTP call executed inside foreach loop (loop_depth=1, await_count=1)."}}]},{slug:"cqrs-read-write",category:"runtime",shortName:"CQRS Read/Write Separation",fileName:"cqrs-read-write.arch",title:"CQRS Read/Write Separation (Zero Hidden Read-Path Mutations)",tagline:"Queries and HTTP GET endpoints return data without mutating SQL tables, publishing events, or invoking commands.",coreInvariant:'Every function handling a CQRS Query (-[:HANDLES_QUERY]->:Message) or serving an HTTP GET (:Endpoint {http_method: "GET"}) must be 100% side-effect free across its entire transitive call chain (*0..5 hops): zero -[:WRITES_TO]->(:Table), zero -[:PUBLISHES]->(:Topic), and zero calls to command handlers.',whyItMatters:["CQRS promises that read paths can be safely retried, cached, routed to read replicas, or called in parallel without altering system state.","In Case Study 10 across open-source CQRS repositories, CodeExplorer uncovered real production bugs where a QueryHandler (FindPublicInvoiceQuery) secretly updated a viewed timestamp in SQL, and an HTTP GET endpoint (GET /wallet/me) lazily inserted a new Wallet row—breaking read-replica routing and idempotency."],howItErodes:[{title:"Lazy Auto-Creation or Audit Counter Update Inside a QueryHandler",scenario:"A developer wants to record that an invoice was viewed or auto-initialize a missing user wallet during a GET request, adding a database write 3 hops deep inside a helper called by a QueryHandler.",codeExample:`// [VIOLATION: SIG-CQRS-001 & SIG-CQRS-003] Found in real OSS benchmarks (Case Study 10)
public class FindPublicInvoiceQueryHandler : IQueryHandler<FindPublicInvoiceQuery, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(FindPublicInvoiceQuery query)
    {
        var invoice = await _repo.GetByTokenAsync(query.Token);
        await _repo.MarkViewedAsync(invoice.Id); // Transitive WRITES_TO table 'invoices'!
        return invoice.ToDto();
    }
}`}],whyGraphBeatsLinters:"ArchUnit and NetArchTest can check whether a class named *QueryHandler* resides in the Queries namespace, but they cannot trace 4 hops through DI interfaces to see if a helper method executes an EF Core SaveChanges, TypeORM save(), or SQL INSERT/UPDATE/DELETE. CodeExplorer’s transitive graph query catches it in <5 ms.",bestFit:["MediatR (.NET), @nestjs/cqrs (TypeScript), Spring CQRS, and Axon systems","APIs routing GET queries to PostgreSQL/MySQL read replicas or HTTP caches"],avoidWhen:["Legacy RPC APIs where GET endpoints intentionally mutate state and cannot be refactored yet (use ratchet baseline first)"],graphLayersUsed:["L3 :Function-[:CALLS*0..5]->:Function","L4 :Endpoint, :Message & :Table","L5 :HANDLES_QUERY & :HANDLES_COMMAND"],layers:()=>[{name:"1. Query Ingress (HTTP GET Endpoints & CQRS Query Messages)",role:'Safe, idempotent read contracts (:Endpoint {http_method:"GET"}, :Message {sub_kind:"query"})',paths:"Layer 4 :Endpoint & :Message",core:!0},{name:"2. Query Handlers (-[:HANDLES_QUERY]->)",role:"Read-model projections and Dapper/SQL/ORM SELECT queries",paths:"**/Queries/**, **/*QueryHandler*",core:!0},{name:"3. Command Handlers (-[:HANDLES_COMMAND]->)",role:"Domain state mutations, transactional writes, and event publications",paths:"**/Commands/**, **/*CommandHandler*"},{name:"4. Storage & Broker Sinks (:Table & :Topic)",role:"READS_FROM allowed on query path; WRITES_TO and PUBLISHES strictly blocked",paths:"Layer 4 :Table & :Topic"}],edges:[{from:"QueryHandler / GET",to:"-[:READS_FROM]->(:Table)",allow:!0,code:"ALLOWED"},{from:"CommandHandler / POST",to:"-[:WRITES_TO]->(:Table)",allow:!0,code:"ALLOWED"},{from:"QueryHandler",to:"-[:CALLS*0..4]->()-[:WRITES_TO]->(:Table)",allow:!1,code:"SIG-CQRS-001",gateId:"SIG-CQRS-001"},{from:"QueryHandler",to:"-[:CALLS*1..3]->(CommandHandler)",allow:!1,code:"SIG-CQRS-002",gateId:"SIG-CQRS-002"},{from:"HTTP GET Endpoint",to:"-[:CALLS*0..5]->()-[:WRITES_TO]->(:Table)",allow:!1,code:"SIG-CQRS-003",gateId:"SIG-CQRS-003"}],gates:(d,w)=>[{id:"SIG-CQRS-001",name:"CQRS - Query Handlers Must Be Side-Effect Free",coords:"Method x Responsibility",pattern:"(q:Function)-[:HANDLES_QUERY]->(msg), (q)-[:CALLS*0..4]->(w)-[:WRITES_TO|PUBLISHES]->(target)",severity:"Error",description:"In CQRS, query handlers must only return data and never mutate database tables or publish events.",remediation:"Move database writes and event publishing out of the query path into an explicit Command Handler.",target:"{w.name} -> {target.name}",message:"Query handler '{q.name}' ({msg.name}) triggers state mutation on '{target.name}' via '{w.name}'",query:`MATCH (q:Function)-[:HANDLES_QUERY]->(msg:Message),
        (q)-[:CALLS*0..4]->(w:Function)-[r:WRITES_TO|PUBLISHES]->(target)
  WHERE NOT q.is_test AND NOT w.is_test
  RETURN DISTINCT q, msg, w, r, target`,platform:w,sampleViolation:{file:"packages/core/src/invoice/commands/handlers/find-public-invoice.handler.ts",line:46,symbol:"FindPublicInvoiceHandler.execute (FindPublicInvoiceQuery)",target:'InvoiceService.markViewed -> table "invoice"',detail:'QueryHandler mutates table "invoice" 2 hops deep via InvoiceService.markViewed.'}},{id:"SIG-CQRS-002",name:"CQRS - Queries Must Not Invoke Command Handlers",coords:"Method x Boundaries",pattern:"(q:Function)-[:HANDLES_QUERY]->(), (q)-[:CALLS*1..3]->(c:Function)-[:HANDLES_COMMAND]->()",severity:"Error",description:"Query handlers must remain strictly separated from command execution paths and never dispatch or invoke command handlers.",remediation:"Decouple the read model from command execution; dispatch commands explicitly from the workflow caller.",target:"{q.name} -> {c.name}",message:"Query handler '{q.name}' ({qmsg.name}) invokes command handler '{c.name}' ({cmd.name})",query:`MATCH (q:Function)-[:HANDLES_QUERY]->(qmsg:Message),
        (q)-[:CALLS*1..3]->(c:Function)-[:HANDLES_COMMAND]->(cmd:Message)
  WHERE NOT q.is_test AND NOT c.is_test
  RETURN DISTINCT q, qmsg, c, cmd`,platform:w,sampleViolation:{file:"src/Modules/Wallet/Queries/GetWalletQueryHandler.cs",line:39,symbol:"GetWalletQueryHandler.Handle",target:"EnsureDefaultWalletCommandHandler.Handle",detail:"Query handler dispatches EnsureDefaultWalletCommand when wallet record is missing."}},{id:"SIG-CQRS-003",name:"CQRS - HTTP GET Endpoints Must Be Read-Only",coords:"Method x Boundaries",pattern:'(ep:Endpoint {http_method:"GET"})-[:TRIGGERS]->(fn)-[:CALLS*0..5]->(w)-[:WRITES_TO]->(target)',severity:"Error",description:"HTTP GET endpoints represent queries and must be safe and idempotent without mutating database tables or publishing events.",remediation:"Route state-changing operations through POST/PUT/PATCH/DELETE command endpoints.",target:"{ep.name} -> {w.name} -> {target.name}",message:"HTTP GET endpoint '{ep.name}' handled by '{fn.name}' mutates '{target.name}' via '{w.name}'",query:`MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)
        -[:CALLS*0..5]->(w:Function)-[r:WRITES_TO|PUBLISHES]->(target)
  WHERE ep.http_method = 'GET' AND NOT fn.is_test AND NOT w.is_test
  RETURN DISTINCT fn, ep, w, r, target`,platform:w,sampleViolation:{file:"src/Api/Endpoints/WalletEndpoints.cs",line:28,symbol:"GET:/api/v1/wallet/me",target:"WalletService.GetOrCreateAsync -> Wallets.Add",detail:'HTTP GET endpoint triggers SQL INSERT on table "Wallets" 3 hops downstream.'}}]},{slug:"scope-and-async-cost",category:"runtime",shortName:"Control Scopes & Async Cost",fileName:"scope-and-async-cost.arch",title:"Control Scopes, Async-in-Loop Waterfalls & Resource Safety",tagline:"Use Layer 3 Scope trees to block N+1 sequential awaits in loops, defer/using leaks in loops, and await inside locks.",coreInvariant:"Every function’s nested control-flow and resource blocks are indexed as Layer 3 :Scope nodes (kind: try, catch, finally, loop, branch, resource, lock, unsafe, task_spawn) with depth, loop_depth, await_count, and cognitive_complexity.",whyItMatters:["A function can pass every typechecker and unit test on a 3-row fixture, then bring down production on 5,000 items because an await expression or Go defer resource cleanup sits inside a nested loop scope (loop_depth >= 1).",'Similarly, awaiting an asynchronous operation inside a synchronization lock (:Scope {kind: "lock", await_count: > 0}) or accumulating a 6-level nested pyramid of loops and branches (cognitive_complexity > 25) creates latent deadlocks and unmaintainable control flow.'],howItErodes:[{title:"Sequential Await Waterfall Inside a Loop (O(N) Latency)",scenario:"An AI agent writes a clean-looking for/foreach loop that awaits a database query or HTTP call on every iteration instead of batching ids or using Task.WhenAll / Promise.all.",codeExample:`// [VIOLATION: SIG-SCOPE-001] Layer 3 :Scope { kind: 'loop', loop_depth: 1, await_count: 2 }
public async Task SyncInvoicesAsync(IEnumerable<Guid> ids)
{
    foreach (var id in ids) // :Scope (loop)
    {
        var inv = await _db.LoadInvoiceAsync(id);   // await #1 inside loop
        await _erpClient.PushInvoiceAsync(inv);     // await #2 inside loop
    }
}`},{title:"Deferred Resource Cleanup Inside a Loop or Await Inside a Lock",scenario:"In Go, placing defer file.Close() or defer rows.Close() inside a for loop delays cleanup until the outer function exits, exhausting file descriptors or DB connections.",codeExample:`// [VIOLATION: SIG-SCOPE-003] :Scope { kind: 'resource', target_type: 'defer', loop_depth: 1 }
for _, path := range files {
    f, err := os.Open(path)
    if err != nil { continue }
    defer f.Close() // Descriptor leak until function exit!
}`}],whyGraphBeatsLinters:"CodeExplorer’s Layer 3 Scope Tree Enricher builds the full hierarchy (Function)-[:DECLARES]->(Scope)-[:CONTAINS*]->(Scope) across all 12 languages and computes exact loop_depth, await_count, cyclomatic_complexity, and cognitive_complexity on every function and scope block.",bestFit:["High-throughput C#, TypeScript, Go, Python, Rust, and Java backend services","Preventing AI-generated N+1 async waterfalls and resource exhaustion in PR checks"],avoidWhen:["CLI migration scripts where sequential 1-by-1 throttling inside a loop is intentional"],graphLayersUsed:["L3 :Function (cognitive_complexity, loop_await_count)","L3 :Scope (kind, loop_depth, await_count)"],layers:()=>[{name:"1. Function Entry (:Function)",role:"Aggregates cyclomatic_complexity, cognitive_complexity, max_nesting_depth, loop_await_count",paths:"Layer 3 :Function",core:!0},{name:'2. Loop & Branch Scopes (:Scope {kind: "loop" | "branch"})',role:"Tracks nesting depth, loop_depth, and sequential await_count inside iteration bodies",paths:"for, foreach, while, .map(async ...)"},{name:'3. Resource & Lock Scopes (:Scope {kind: "resource" | "lock"})',role:"Tracks using, await using, Go defer, Python with, lock, and synchronized blocks",paths:"using, defer, with, lock"},{name:'4. Try / Catch / Finally Scopes (:Scope {kind: "try" | "catch" | "finally"})',role:"Tracks exception target_type, is_empty, has_comment, and statement_count",paths:"try, catch, except, finally"}],edges:[{from:"Function",to:"-[:DECLARES]->(:Scope {loop_depth: 0})",allow:!0,code:"ALLOWED"},{from:"Loop Scope (loop_depth > 0)",to:"Sequential await (await_count > 0)",allow:!1,code:"SIG-SCOPE-001",gateId:"SIG-SCOPE-001"},{from:"Function",to:"cognitive_complexity > 25",allow:!1,code:"SIG-SCOPE-002",gateId:"SIG-SCOPE-002"},{from:"Loop / Lock Scope",to:"defer/using in loop OR await in lock",allow:!1,code:"SIG-SCOPE-003",gateId:"SIG-SCOPE-003"}],gates:(d,w)=>[{id:"SIG-SCOPE-001",name:"Performance - No Sequential Await Inside Loops",coords:"Method x Resources",pattern:"(fn:Function)-[:DECLARES|CONTAINS*1..6]->(s:Scope) WHERE s.loop_depth > 0 AND s.await_count > 0",severity:"Warning",description:"Sequential await expressions inside a loop execute I/O round-trips one-by-one (O(N) latency waterfall) instead of batching.",remediation:"Batch the query/request outside the loop or use bounded concurrency (Task.WhenAll / Promise.all).",target:"loop@{s.line} (depth={s.loop_depth}, awaits={s.await_count})",message:"Function '{fn.name}' executes {s.await_count} sequential await(s) inside a loop at line {s.line}",query:`MATCH (fn:Function)-[:DECLARES|CONTAINS*1..6]->(s:Scope)
  WHERE NOT coalesce(fn.is_test, false)
    AND s.loop_depth > 0
    AND s.await_count > 0
  RETURN DISTINCT fn, s`,platform:w,sampleViolation:{file:"src/Billing/Services/InvoiceSyncService.cs",line:88,symbol:"InvoiceSyncService.SyncBatchAsync",target:"loop@88 (depth=1, awaits=2)",detail:"Function executes 2 sequential awaits per iteration inside foreach loop (loop_await_count=2)."}},{id:"SIG-SCOPE-002",name:"Maintainability - Cognitive & Cyclomatic Complexity Ceiling",coords:"Method x Resources",pattern:"(fn:Function) WHERE fn.cognitive_complexity > 25 OR fn.cyclomatic_complexity > 15",severity:"Warning",description:"Functions with high depth-weighted cognitive complexity (> 25) or cyclomatic complexity (> 15) are error-prone and hard to test.",remediation:"Extract nested loop/branch/try-catch scopes into focused helper methods or use early guard clauses.",target:"cognitive={fn.cognitive_complexity}, cyclomatic={fn.cyclomatic_complexity}, depth={fn.max_nesting_depth}",message:"Function '{fn.name}' exceeds complexity ceiling (cognitive={fn.cognitive_complexity}, cyclomatic={fn.cyclomatic_complexity})",query:`MATCH (fn:Function)
  WHERE NOT coalesce(fn.is_test, false)
    AND fn.name <> '<module_init>'
    AND (coalesce(fn.cognitive_complexity, 0) > 25 OR coalesce(fn.cyclomatic_complexity, 0) > 15)
  RETURN fn`,platform:w,sampleViolation:{file:"src/Orders/Domain/DiscountRuleEvaluator.cs",line:114,symbol:"DiscountRuleEvaluator.EvaluateCampaignTree",target:"cognitive=34, cyclomatic=18, depth=5",detail:"Deeply nested 5-level loop/branch pyramid exceeds cognitive complexity ceiling (34 > 25)."}},{id:"SIG-SCOPE-003",name:"Reliability - No Resource Leak in Loop or Await in Lock",coords:"Method x Resources",pattern:'(s:Scope) WHERE (s.kind = "resource" AND s.loop_depth > 0) OR (s.kind = "lock" AND s.await_count > 0)',severity:"Error",description:"Allocating deferred/disposable resources inside a loop or awaiting inside a lock scope causes descriptor exhaustion or deadlocks.",remediation:"Move resource acquisition outside the loop (or extract a helper function for Go defer) and never await inside a lock.",target:"{s.kind}@{s.line} ({s.target_type})",message:"Function '{fn.name}' declares '{s.kind}' scope ({s.target_type}) at line {s.line} with loop_depth={s.loop_depth}, await_count={s.await_count}",query:`MATCH (fn:Function)-[:DECLARES|CONTAINS*1..6]->(s:Scope)
  WHERE NOT coalesce(fn.is_test, false)
    AND ((s.kind = 'resource' AND s.loop_depth > 0)
      OR (s.kind = 'lock' AND s.await_count > 0))
  RETURN DISTINCT fn, s`,platform:w,sampleViolation:{file:"internal/importer/archive_reader.go",line:63,symbol:"ArchiveReader.ImportAll",target:"resource@63 (defer)",detail:"Go defer statement declared inside for loop (loop_depth=1) delays file descriptor close until function exit."}}]},{slug:"transactions-outbox",category:"runtime",shortName:"Transactions & Outbox",fileName:"transactions-outbox.arch",title:"Database Transactions & Transactional Outbox Safety",tagline:"Prevent external HTTP calls inside open DB transactions, multi-table partial writes, and dual-write broker races.",coreInvariant:"Database transactions (:Transaction) must never span outbound network I/O (:ExternalService), commands mutating 2+ distinct tables must execute inside a transaction boundary, and functions mutating a SQL table must not publish directly to a message broker (:Topic) without an Outbox table.",whyItMatters:["Calling an external HTTP API (Stripe, SendGrid, internal microservice) while holding an open SQL transaction keeps row/table locks open across 5–30 second network timeouts, exhausting the database connection pool in seconds.","Conversely, writing to a database table and publishing directly to Kafka/RabbitMQ in the same method (Dual Write) guarantees data inconsistency whenever the broker publish fails after commit or the DB commit fails after publish."],howItErodes:[{title:"Outbound HTTP Call Inside an Open Database Transaction",scenario:"Inside a BeginTransactionAsync scope (or [Transactional] method), a handler updates an order row and then calls an external notification or payment webhook before committing.",codeExample:`// [VIOLATION: SIG-TX-001] Function executes in :Transaction AND calls :ExternalService
await using var tx = await _db.Database.BeginTransactionAsync();
await _db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(...);
// Holds PostgreSQL row locks while waiting on external HTTP response!
await _httpClient.PostAsJsonAsync("https://hooks.partner.io/notify", payload);
await tx.CommitAsync();`}],whyGraphBeatsLinters:"CodeExplorer models Layer 4 :Transaction boundaries (-[:EXECUTES_IN_TX]->), :Table mutations (-[:WRITES_TO]->), :Topic publications (-[:PUBLISHES_TO]->), and :ExternalService calls in the same graph, making cross-cutting transaction hazards trivial to query.",bestFit:["Financial, e-commerce, billing, and event-driven systems requiring atomic state transitions"],avoidWhen:["Read-only analytics services or stateless proxy gateways with no transactional storage"],graphLayersUsed:["L4 :Transaction","L4 :Table & :Topic","L4 :ExternalService"],layers:()=>[{name:"1. Command Handler / Use Case",role:"Opens transaction boundary and coordinates domain aggregate updates",paths:"Layer 3 :Function-[:HANDLES_COMMAND]->:Message",core:!0},{name:"2. Active Database Transaction (:Transaction)",role:"Wraps multi-table SQL mutations + Outbox table insert atomically",paths:"Layer 4 :Transaction (-[:EXECUTES_IN_TX]->)",core:!0},{name:"3. Transactional Outbox Relay Worker",role:"Polls or streams outbox_messages table after commit and publishes to :Topic",paths:"Layer 4 :Job / :Worker"},{name:"4. External HTTP Services & Message Brokers (:ExternalService, :Topic)",role:"Must be invoked outside the DB lock window or via Outbox relay",paths:"Layer 4 :ExternalService & :Topic"}],edges:[{from:"Transaction Scope",to:"Multi-Table SQL Writes + Outbox Insert",allow:!0,code:"ALLOWED"},{from:"Transaction Scope",to:"Outbound HTTP / gRPC Call",allow:!1,code:"SIG-TX-001",gateId:"SIG-TX-001"},{from:"Command Handler (2+ Tables)",to:"No Transaction Boundary",allow:!1,code:"SIG-TX-002",gateId:"SIG-TX-002"},{from:"DB Table Write",to:"Direct Broker Publish (No Outbox)",allow:!1,code:"SIG-TX-003",gateId:"SIG-TX-003"}],gates:(d,w)=>[{id:"SIG-TX-001",name:"Transactions - No External Network IO Inside DB Transaction",coords:"Method x Boundaries",pattern:"(fn:Function)-[:EXECUTES_IN_TX]->(tx:Transaction), (fn)-[:CALLS]->(es:ExternalService)",severity:"Error",description:"Invoking external HTTP/gRPC services inside an open database transaction holds row locks across network timeouts.",remediation:"Move outbound network calls outside the transaction scope or decouple them via a transactional Outbox event.",target:"{tx.name} -> {es.name}",message:"Function '{fn.name}' inside transaction '{tx.name}' calls external service '{es.name}'",query:`MATCH (fn:Function)-[:EXECUTES_IN_TX]->(tx:Transaction),
        (fn)-[:CALLS|CALLED_BY|ACCESSED_BY]-(es:ExternalService)
  WHERE NOT fn.is_test
  RETURN DISTINCT fn, tx, es`,platform:w,sampleViolation:{file:"src/Payments/Handlers/CapturePaymentCommandHandler.cs",line:72,symbol:"CapturePaymentCommandHandler.Handle",target:"tx:BeginTransactionAsync -> POST https://api.stripe.com/v1/charges",detail:"Outbound Stripe HTTP call executes while database transaction is open."}},{id:"SIG-TX-002",name:"Transactions - Multi-Table Write Without Transaction Boundary",coords:"Method x Responsibility",pattern:"(cmd:Function)-[:HANDLES_COMMAND]->(), count(DISTINCT tbl written) >= 2 without :Transaction",severity:"Warning",description:"Command handlers writing to multiple distinct tables without a transaction boundary leave partial state on mid-flight failures.",remediation:"Wrap multi-table command execution in an explicit database transaction or transactional pipeline behavior.",target:"{cmd.name} ({tableCount} tables)",message:"Command handler '{cmd.name}' ({m.name}) writes to {tableCount} distinct tables outside an active transaction",query:`MATCH (cmd:Function)-[:HANDLES_COMMAND]->(m:Message),
        (cmd)-[:CALLS*0..4]->(w:Function)-[:WRITES_TO]->(tbl:Table)
  WHERE NOT cmd.is_test AND NOT w.is_test
    AND NOT EXISTS((cmd)-[:EXECUTES_IN_TX]->(:Transaction))
  WITH cmd, m, count(DISTINCT tbl) AS tableCount
  WHERE tableCount >= 2
  RETURN cmd, m, tableCount`,platform:w,sampleViolation:{file:"src/Orders/Commands/TransferSubscriptionHandler.cs",line:44,symbol:"TransferSubscriptionHandler.Handle",target:"TransferSubscriptionHandler (3 tables)",detail:"Command mutates subscriptions,member_seats,audit_entries without a transaction boundary."}},{id:"SIG-TX-003",name:"Transactions - Dual Write to DB and Broker Without Outbox",coords:"Method x Responsibility",pattern:"(fn:Function)-[:WRITES_TO]->(tbl:Table) AND (fn)-[:PUBLISHES_TO]->(top:Topic) without outbox",severity:"Warning",description:"Writing to a database table and publishing directly to a message broker risks lost or phantom messages.",remediation:"Persist outgoing integration events to a transactional Outbox table in the same DB transaction.",target:"{tbl.name} + {top.name}",message:"Function '{fn.name}' performs dual write to table '{tbl.name}' and broker topic '{top.name}' without Outbox",query:`MATCH (fn:Function)-[:WRITES_TO]->(tbl:Table),
        (fn)-[:PUBLISHES|PUBLISHES_TO|PUBLISHED_BY]-(top:Topic)
  WHERE NOT fn.is_test
    AND NOT toLower(tbl.name) CONTAINS 'outbox'
  RETURN DISTINCT fn, tbl, top`,platform:w,sampleViolation:{file:"src/Orders/Services/OrderFulfillmentService.cs",line:105,symbol:"OrderFulfillmentService.CompleteOrderAsync",target:"orders + topic:order.completed",detail:'Function writes to SQL table "orders" and publishes directly to Kafka topic "order.completed".'}}]},{slug:"endpoint-security",category:"runtime",shortName:"Zero-Trust Endpoint Security",fileName:"endpoint-security.arch",title:"Zero-Trust Endpoint & Data Lineage Security Gates",tagline:"Trace every HTTP/GraphQL/gRPC route to the SQL tables it touches and verify authorization policies and fail-closed guards.",coreInvariant:'Every state-mutating endpoint (POST/PUT/PATCH/DELETE) and every endpoint reaching sensitive tables (:Table {sensitivity: "pii"|"pci"|"secret"}) must be guarded by an explicit Layer 4 :Policy, privileged routes (/admin, /internal, /billing) must require role/permission policies, and authorization guards must never contain empty catch blocks.',whyItMatters:["Most authorization bugs happen when a new endpoint or GraphQL mutation is added and the developer (or AI agent) forgets [Authorize] / @UseGuards, or uses a generic logged-in guard on an /admin or /billing endpoint that requires an explicit role.",'By combining Layer 4 :Endpoint, :Policy, transitive call chains (-[:CALLS*0..4]->), :Table sensitivity, and Layer 3 :Scope {kind: "catch"}, CodeExplorer audits authorization across the full request-to-database path.'],howItErodes:[{title:"Mutating Endpoint Reaching a Database Table Without a Policy Guard",scenario:"A new controller or FastAPI/NestJS route is wired up to a command handler that writes to a SQL table, but no authentication or role guard decorator is attached.",codeExample:`// [VIOLATION: SIG-SEC-001] POST endpoint mutates 'user_api_keys' table with no :Policy node
[HttpPost("api/v1/keys/rotate")] // Missing [Authorize] policy!
public async Task<IActionResult> RotateKey(RotateKeyCommand cmd)
    => Ok(await _mediator.Send(cmd)); // Transitively WRITES_TO table 'user_api_keys'`},{title:'Silent Exception Swallowing Inside an Auth Guard (:Scope {kind: "catch", is_empty: true})',scenario:"An authorization handler catches a token-parsing or permission-lookup exception with an empty catch block, inadvertently allowing execution to continue.",codeExample:`// [VIOLATION: SIG-SEC-005] Empty catch Scope inside Policy enforcement function
try {
    await _permissionVerifier.EnsureTenantAdminAsync(ctx.User);
} catch (Exception) {
    // Empty catch inside auth guard risks fail-open bypass!
}`}],whyGraphBeatsLinters:"A syntactic linter looking at an endpoint cannot tell whether that endpoint is a harmless healthcheck or whether 3 hops down the call chain it mutates a PII/billing database table. CodeExplorer joins :Endpoint, :Policy, transitive :Function calls, :Table sensitivity, and :Scope catch blocks in one query.",bestFit:["Multi-tenant SaaS backends, fintech/healthcare APIs, and public REST/GraphQL gateways"],avoidWhen:["Internal air-gapped CLI utilities with no HTTP/gRPC endpoints"],graphLayersUsed:['L3 :Scope {kind:"catch"}',"L4 :Endpoint & :Policy","L4 :Table (sensitivity)"],layers:()=>[{name:"1. Exposed API Endpoints (:Endpoint)",role:"REST, GraphQL, gRPC, and WebSocket routes with http_method and route_template",paths:"Layer 4 :Endpoint"},{name:"2. Authorization Policies & Guards (:Policy)",role:"Role, permission, authenticated, and custom_guard policies (-[:GUARDED_BY]->, -[:ENFORCED_BY]->)",paths:"Layer 4 :Policy",core:!0},{name:"3. Guard Enforcement Functions & Catch Scopes (:Function -> :Scope)",role:"Must fail closed on exceptions (zero empty catch scopes)",paths:'Layer 3 :Function & :Scope {kind:"catch"}',core:!0},{name:"4. Sensitive & Mutable SQL Tables (:Table)",role:"Classified tables (pii, pci, secret) and write sinks (-[:WRITES_TO]->)",paths:"Layer 4 :Table"}],edges:[{from:"Mutating Endpoint",to:"-[:GUARDED_BY]->(:Policy)",allow:!0,code:"ALLOWED"},{from:"Mutating Endpoint (No Policy)",to:"-[:CALLS*0..4]->()-[:WRITES_TO]->(:Table)",allow:!1,code:"SIG-SEC-001",gateId:"SIG-SEC-001"},{from:"Anonymous Endpoint",to:"Sensitive PII/PCI/Secret Table",allow:!1,code:"SIG-SEC-002",gateId:"SIG-SEC-002"},{from:"Privileged /admin or /billing Route",to:"Missing Role/Permission Policy",allow:!1,code:"SIG-SEC-003",gateId:"SIG-SEC-003"},{from:"Policy Guard Function",to:"Empty Catch Scope (is_empty: true)",allow:!1,code:"SIG-SEC-005",gateId:"SIG-SEC-005"}],gates:(d,w)=>[{id:"SIG-SEC-001",name:"Security - Unguarded Mutating Endpoint",coords:"Method x Boundaries",pattern:"(ep:Endpoint {POST/PUT/PATCH/DELETE})-[:CALLS*0..4]->(w)-[:WRITES_TO]->(tbl) without :Policy",severity:"Error",description:"State-mutating HTTP endpoints (POST, PUT, PATCH, DELETE) must declare an authorization Policy or explicit public marker.",remediation:"Attach [Authorize], @UseGuards, @PreAuthorize, or an explicit [AllowAnonymous] / @Public policy.",target:"{ep.name} -> {tbl.name}",message:"Mutating endpoint '{ep.name}' writes to '{tbl.name}' via '{w.name}' without a GUARDED_BY authorization policy",query:`MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)
        -[:CALLS*0..4]->(w:Function)-[:WRITES_TO]->(tbl:Table)
  WHERE ep.http_method IN ['POST', 'PUT', 'PATCH', 'DELETE']
    AND NOT fn.is_test AND NOT w.is_test
    AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy))
  RETURN DISTINCT fn, ep, w, tbl`,platform:w,sampleViolation:{file:"src/Api/Controllers/ApiKeysController.cs",line:49,symbol:"POST:/api/v1/keys/rotate",target:"POST:/api/v1/keys/rotate -> user_api_keys",detail:'Mutating POST endpoint writes to table "user_api_keys" without a GUARDED_BY Policy.'}},{id:"SIG-SEC-002",name:"Security - Unprotected Access to Sensitive (PII/PCI/Secret) Tables",coords:"Method x Boundaries",pattern:"(ep:Endpoint)-[:CALLS*0..4]->(acc)-[:READS_FROM|WRITES_TO]->(tbl {sensitivity: pii|pci|secret})",severity:"Warning",description:"Endpoints accessing tables classified as pii, pci, or secret must be protected by a non-anonymous authorization Policy.",remediation:"Protect the endpoint with an authenticated, role, permission, or custom_guard Policy.",target:"{ep.name} -> {tbl.name} ({tbl.sensitivity})",message:"Unprotected or anonymous endpoint '{ep.name}' accesses sensitive table '{tbl.name}' ({tbl.sensitivity})",query:`MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)
        -[:CALLS*0..4]->(acc:Function)-[:READS_FROM|WRITES_TO]->(tbl:Table)
  WHERE tbl.sensitivity IN ['pii', 'pci', 'secret']
    AND NOT fn.is_test AND NOT acc.is_test
    AND (NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy))
         OR EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'anonymous'})))
  RETURN DISTINCT fn, ep, acc, tbl`,platform:w,sampleViolation:{file:"src/Users/Controllers/ProfileExportController.cs",line:33,symbol:"GET:/api/v1/users/export",target:"GET:/api/v1/users/export -> customer_kyc_documents (pii)",detail:'Unguarded endpoint reads PII-classified table "customer_kyc_documents".'}},{id:"SIG-SEC-003",name:"Security - Privileged Route Without Role or Permission Policy",coords:"Method x Boundaries",pattern:"(ep:Endpoint {/admin|/internal|/billing}) without :Policy {sub_kind: role|permission|custom_guard}",severity:"Error",description:"Privileged routes (/admin, /internal, /billing, /roles, /permissions) must enforce role, permission, or custom_guard authorization.",remediation:'Add a role or permission policy (e.g. [Authorize(Roles = "Admin")], @Roles, @PreAuthorize).',target:"{ep.name}",message:"Privileged endpoint '{ep.name}' in '{ep.file_path}' lacks a role, permission, or custom_guard Policy",query:`MATCH (ep:Endpoint)
  WHERE (toLower(ep.route_template) CONTAINS '/admin'
      OR toLower(ep.route_template) CONTAINS '/internal'
      OR toLower(ep.route_template) CONTAINS '/billing'
      OR toLower(ep.route_template) CONTAINS '/roles'
      OR toLower(ep.route_template) CONTAINS '/permissions')
    AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'role'}))
    AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'permission'}))
    AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'custom_guard'}))
  RETURN DISTINCT ep`,platform:w,sampleViolation:{file:"src/Billing/Controllers/AdminBillingController.cs",line:61,symbol:"POST:/api/v1/admin/billing/credit",target:"POST:/api/v1/admin/billing/credit",detail:"Privileged /admin/billing route only requires basic login instead of a role or permission policy."}},{id:"SIG-SEC-005",name:"Security - Silent Exception Swallowing in Auth Guard",coords:"Method x Implementation",pattern:'(pol:Policy)-[:ENFORCED_BY]->(fn:Function)-[:DECLARES|CONTAINS*1..4]->(c:Scope {kind:"catch", is_empty:true})',severity:"Error",description:"Empty catch blocks inside authorization guard or requirement handler functions can silently fail open and bypass access control.",remediation:"Explicitly deny access (return false / throw Unauthorized) and log the failure inside the catch block.",target:"{pol.name} -> {fn.name}",message:"Authorization guard '{fn.name}' enforcing policy '{pol.name}' contains an empty catch block at line {c.line}",query:`MATCH (pol:Policy)-[:ENFORCED_BY]->(fn:Function)-[:DECLARES|CONTAINS*1..4]->(c:Scope)
  WHERE c.kind = 'catch' AND c.is_empty = true AND NOT fn.is_test
  RETURN DISTINCT fn, pol, c`,platform:w,sampleViolation:{file:"src/Auth/Guards/TenantAdminGuard.cs",line:44,symbol:"TenantAdminGuard.CanActivateAsync",target:"Policy:TenantAdmin -> TenantAdminGuard.CanActivateAsync",detail:"Empty catch Scope at line 44 inside authorization guard can silently fail open."}}]}],vu=["agnostic","dotnet","typescript","go","python","java"];function bu(d){const w=d.coords.split(" x ");return[`id: ${d.id}`,"category: Architectural",`platform: ${d.platform}`,`scope: ${w[0]||"Module"}`,`dimension: ${w[1]||"Boundaries"}`,`severity: ${d.severity}`,`name: ${d.name}`,`description: ${d.description}`,`remediation: ${d.remediation}`,`target: "${d.target}"`,`message: "${d.message}"`,"query: |-",`  ${d.query}`,"ratchet:","  enabled: true",`  baseline: baselines/${d.id}.json`].join(`
`)}function qi(d,w,h){return`${["# CodeExplorer Semantic Invariant Gate Pack (v2 Node-First & Edge-Aware)",`# Pack: ${d.slug} | Stack: ${w}`,`# Save to .codeexplorer/gates/${d.slug}.yaml and run: ce gate check`,"---"].join(`
`)}
${h.map(bu).join(`
---
`)}
`}const wu=({pack:d,gatesCount:w,stackLabel:h,onOpenTopology:Q,onOpenAllYaml:j})=>l.jsxs("article",{className:"ide-doc",children:[l.jsxs("header",{className:"ide-doc-header",children:[l.jsxs("div",{className:"ide-doc-badges",children:[l.jsx("code",{className:"ide-badge ide-badge--accent",children:d.category==="structural"?"Structural Style":"Runtime & Dataflow Invariant"}),l.jsxs("code",{className:"ide-badge",children:[w," Semantic Invariant Gates"]}),l.jsxs("code",{className:"ide-badge",children:["Preset: ",h]})]}),l.jsx("h2",{className:"ide-doc-title",children:d.title}),l.jsx("p",{className:"ide-doc-tagline",children:d.tagline})]}),l.jsxs("div",{className:"ide-callout ide-callout--invariant",children:[l.jsx("div",{className:"ide-callout-label",children:"THE CORE INVARIANT"}),l.jsx("p",{className:"ide-callout-text",children:d.coreInvariant})]}),l.jsxs("section",{className:"ide-doc-section",children:[l.jsx("h3",{className:"ide-doc-h3",children:"1. Why This Architecture Matters in Production"}),l.jsx("div",{className:"ide-prose-stack",children:d.whyItMatters.map((x,X)=>l.jsx("p",{children:x},X))})]}),l.jsxs("section",{className:"ide-doc-section",children:[l.jsx("h3",{className:"ide-doc-h3",children:"2. How It Erodes in Practice (and Under AI Coding Agents)"}),l.jsx("div",{className:"ide-erosion-grid",children:d.howItErodes.map((x,X)=>l.jsxs("div",{className:"ide-erosion-card",children:[l.jsxs("div",{className:"ide-erosion-head",children:[l.jsxs("span",{className:"ide-erosion-num",children:["FAILURE MODE 0",X+1]}),l.jsx("h4",{className:"ide-erosion-title",children:x.title})]}),l.jsx("p",{className:"ide-erosion-desc",children:x.scenario}),l.jsx("pre",{className:"ide-code-snippet",children:l.jsx("code",{children:x.codeExample})})]},X))})]}),l.jsxs("section",{className:"ide-doc-section",children:[l.jsx("h3",{className:"ide-doc-h3",children:"3. Why File-Level Linters & ArchUnit Miss It (5-Layer Graph Advantage)"}),l.jsx("div",{className:"ide-callout ide-callout--graph",children:l.jsx("p",{className:"ide-callout-text",children:d.whyGraphBeatsLinters})})]}),l.jsxs("section",{className:"ide-doc-section",children:[l.jsx("h3",{className:"ide-doc-h3",children:"4. Engineering Trade-Offs: When to Use vs When to Skip"}),l.jsxs("div",{className:"ide-tradeoff-grid",children:[l.jsxs("div",{className:"ide-tradeoff-box ide-tradeoff-box--fit",children:[l.jsx("div",{className:"ide-tradeoff-head",children:"✓ Best Fit When"}),l.jsx("ul",{className:"ide-bullet-list",children:d.bestFit.map((x,X)=>l.jsx("li",{children:x},X))})]}),l.jsxs("div",{className:"ide-tradeoff-box ide-tradeoff-box--avoid",children:[l.jsx("div",{className:"ide-tradeoff-head",children:"✕ Avoid or Keep Simple When"}),l.jsx("ul",{className:"ide-bullet-list",children:d.avoidWhen.map((x,X)=>l.jsx("li",{children:x},X))})]})]})]}),l.jsxs("footer",{className:"ide-doc-footer",children:[l.jsx("button",{type:"button",className:"btn",onClick:Q,children:"Inspect Layer Topology & Forbidden Edges →"}),l.jsxs("button",{type:"button",className:"btn btn--primary",onClick:j,children:["View Executable ",d.slug,".gates.yaml (",w," gates) →"]})]})]}),Su=({pack:d,layers:w,gates:h,stackLabel:Q,onInspectGate:j})=>l.jsxs("div",{className:"ide-topology-view",children:[l.jsxs("div",{className:"ide-topo-split",children:[l.jsxs("div",{className:"ide-topo-diagram-box",children:[l.jsxs("div",{className:"ide-box-head",children:[l.jsxs("div",{children:[l.jsx("h3",{className:"ide-box-title",children:"Layer & Boundary Blueprint"}),l.jsxs("p",{className:"ide-box-sub",children:["Stack paths adapted for ",l.jsx("code",{children:Q})]})]}),l.jsxs("div",{className:"blueprint-legend",children:[l.jsx("code",{className:"legend-tag legend-tag--allow",children:"ALLOWED"}),l.jsx("code",{className:"legend-tag legend-tag--block",children:"BLOCKED BY GATE"})]})]}),l.jsx("div",{className:"ide-layers-stack",children:w.map((x,X)=>l.jsxs("div",{className:`ide-layer-card${x.core?" is-core":""}`,children:[l.jsxs("div",{className:"ide-layer-card-top",children:[l.jsx("span",{className:"ide-layer-name",children:x.name}),l.jsx("code",{className:"ide-layer-path",children:x.paths})]}),l.jsx("p",{className:"ide-layer-role",children:x.role})]},X))})]}),l.jsxs("div",{className:"ide-topo-edges-box",children:[l.jsx("div",{className:"ide-box-head",children:l.jsxs("div",{children:[l.jsx("h3",{className:"ide-box-title",children:"Allowed vs Blocked Graph Transitions"}),l.jsx("p",{className:"ide-box-sub",children:"Click any blocked transition to open its v2 Cypher gate YAML"})]})}),l.jsx("div",{className:"ide-edge-list",children:d.edges.map((x,X)=>x.allow?l.jsxs("div",{className:"ide-edge-item is-allowed",children:[l.jsxs("div",{className:"ide-edge-flow",children:[l.jsx("strong",{children:x.from}),l.jsx("span",{className:"ide-edge-arrow",children:"→"}),l.jsx("span",{children:x.to})]}),l.jsx("code",{className:"ide-edge-badge is-ok",children:x.code})]},X):l.jsxs("button",{type:"button",className:"ide-edge-item is-blocked",onClick:()=>x.gateId&&j(x.gateId),children:[l.jsxs("div",{className:"ide-edge-flow",children:[l.jsx("strong",{children:x.from}),l.jsx("span",{className:"ide-edge-arrow",children:"↛"}),l.jsx("span",{children:x.to})]}),l.jsxs("code",{className:"ide-edge-badge is-bad",children:["BLOCKED · ",x.code," →"]})]},X))})]})]}),l.jsxs("div",{className:"ide-gates-table-box",children:[l.jsxs("div",{className:"ide-box-head",children:[l.jsxs("h3",{className:"ide-box-title",children:["Included Semantic Invariant Gates (",h.length,")"]}),l.jsx("span",{className:"ide-box-sub",children:"Click any row to inspect its v2 Node-First YAML & Cypher query"})]}),l.jsx("div",{className:"table-surface",children:l.jsxs("table",{className:"bench-table",children:[l.jsx("thead",{children:l.jsxs("tr",{children:[l.jsx("th",{children:"Gate ID & Name"}),l.jsx("th",{children:"FOIM Coordinates"}),l.jsx("th",{children:"Forbidden Graph Pattern"}),l.jsx("th",{children:"Severity"}),l.jsx("th",{children:"Action"})]})}),l.jsx("tbody",{children:h.map(x=>l.jsxs("tr",{children:[l.jsxs("td",{children:[l.jsx("code",{children:x.id}),l.jsx("br",{}),l.jsx("strong",{children:x.name})]}),l.jsx("td",{children:x.coords}),l.jsx("td",{children:l.jsx("code",{children:x.pattern})}),l.jsx("td",{className:"cell-num",children:l.jsx("code",{className:`badge ${x.severity==="Error"?"badge--bad":"badge--accent"}`,children:x.severity})}),l.jsx("td",{className:"cell-num",children:l.jsx("button",{type:"button",className:"ide-mini-btn",onClick:()=>j(x.id),children:"Open YAML →"})})]},x.id))})]})})]})]}),ku=()=>{const[d,w]=lt.useState("clean-architecture"),[h,Q]=lt.useState("overview"),[j,x]=lt.useState("agnostic"),[X,de]=lt.useState(null),[te,ve]=lt.useState("problems"),[ke,he]=lt.useState(!0),[Ce,W]=lt.useState(null),k=lt.useMemo(()=>hs.find(L=>L.slug===d)||hs[0],[d]),be=vd[j],Ae=lt.useMemo(()=>k.layers(be),[k,be]),Oe=lt.useMemo(()=>k.gates(be,j),[k,be,j]),kt=lt.useMemo(()=>hs.filter(L=>L.category==="structural"),[]),at=lt.useMemo(()=>hs.filter(L=>L.category==="runtime"),[]),O=lt.useMemo(()=>{if(!X)return Oe;const L=Oe.filter(we=>we.id===X);return L.length>0?L:Oe},[Oe,X]),V=lt.useMemo(()=>qi(k,j,O),[k,j,O]),pe=lt.useMemo(()=>V.trimEnd().split(`
`),[V]);function me(L){W(L),setTimeout(()=>{W(we=>we===L?null:we)},1800)}function Re(L){w(L),de(null)}function Ge(L){de(L),Q("yaml")}function Pe(){typeof navigator<"u"&&navigator.clipboard&&navigator.clipboard.writeText(V).then(()=>me("Copied YAML"))}function We(){const L=qi(k,j,Oe),we=`Inspect my workspace using CodeExplorer get_architecture_map, adapt the following '${k.title}' (${j}) Semantic Invariant Gate pack to match our actual folder and project paths, save the rules to .codeexplorer/gates/${k.slug}.yaml, run check_semantic_gates, and freeze existing baseline debt via ce gate ratchet --update:

`+L;typeof navigator<"u"&&navigator.clipboard&&navigator.clipboard.writeText(we).then(()=>me("Copied AI Prompt"))}function ct(){const L=qi(k,j,Oe),we=new Blob([L],{type:"text/yaml;charset=utf-8"}),Be=URL.createObjectURL(we),Fe=document.createElement("a");Fe.href=Be,Fe.download=`${k.slug}-${j}.gates.yaml`,document.body.appendChild(Fe),Fe.click(),document.body.removeChild(Fe),URL.revokeObjectURL(Be),me("Downloaded .yaml")}const Z=j==="agnostic"?"ce gate check --scan":`ce gate check -p ${j} --scan`;return l.jsxs("main",{id:"main-content",className:"arch-page",children:[l.jsx("section",{className:"arch-top-header",children:l.jsxs("div",{className:"wrap arch-top-bar",children:[l.jsxs("div",{className:"arch-top-copy",children:[l.jsxs("div",{className:"arch-eyebrow-row",children:[l.jsx("code",{className:"arch-eyebrow-badge",children:"ARCHITECTURE EXPLORER & GATE PACKS"}),l.jsxs("span",{className:"arch-eyebrow-meta",children:["10 packs · ",hs.reduce((L,we)=>L+we.gates(be,j).length,0)," executable v2 Cypher invariants"]})]}),l.jsx("h1",{className:"arch-main-title",children:"Read the architecture. Understand how it breaks. Execute the rules."})]}),l.jsxs("div",{className:"arch-top-actions",children:[l.jsx("button",{type:"button",className:"btn",onClick:We,children:Ce==="Copied AI Prompt"?"Prompt Copied!":"Copy AI Adapt Prompt"}),l.jsx("button",{type:"button",className:"btn btn--primary",onClick:ct,children:Ce==="Downloaded .yaml"?"Downloaded!":`Download ${k.slug}.yaml`})]})]})}),l.jsx("section",{className:"arch-workbench-section","aria-label":"Architecture IDE Workbench",children:l.jsx("div",{className:"wrap",children:l.jsxs("div",{className:"ide-shell",children:[l.jsxs("div",{className:"ide-titlebar",children:[l.jsxs("div",{className:"ide-titlebar-left",children:[l.jsxs("span",{className:"ide-window-dots","aria-hidden":"true",children:[l.jsx("span",{className:"ide-dot ide-dot--1"}),l.jsx("span",{className:"ide-dot ide-dot--2"}),l.jsx("span",{className:"ide-dot ide-dot--3"})]}),l.jsxs("span",{className:"ide-workspace-label",children:["code-explorer-architectures — ",l.jsx("code",{children:k.fileName})]})]}),l.jsxs("div",{className:"ide-stack-bar",role:"group","aria-label":"Language stack preset",children:[l.jsx("span",{className:"ide-stack-label",children:"Stack paths:"}),vu.map(L=>l.jsx("button",{type:"button",className:`ide-stack-pill${j===L?" is-active":""}`,"aria-pressed":j===L,onClick:()=>x(L),children:vd[L].label},L))]})]}),l.jsxs("div",{className:"ide-body",children:[l.jsxs("aside",{className:"ide-sidebar","aria-label":"Architecture packs explorer",children:[l.jsxs("div",{className:"ide-sidebar-header",children:[l.jsx("span",{children:"EXPLORER"}),l.jsx("code",{className:"ide-sidebar-count",children:"10 PACKS"})]}),l.jsxs("div",{className:"ide-tree",children:[l.jsxs("div",{className:"ide-folder-group",children:[l.jsxs("div",{className:"ide-folder-title",children:[l.jsx("span",{className:"ide-chevron","aria-hidden":"true",children:"▾"}),l.jsx("span",{children:"STRUCTURAL-STYLES"})]}),l.jsx("ul",{className:"ide-file-list",role:"listbox","aria-label":"Structural architecture styles",children:kt.map(L=>{const we=L.slug===k.slug,Be=L.gates(be,j).length;return l.jsx("li",{children:l.jsxs("button",{type:"button",role:"option","aria-selected":we,className:`ide-file-btn${we?" is-selected":""}`,onClick:()=>Re(L.slug),children:[l.jsx("span",{className:"ide-file-icon ide-file-icon--arch","aria-hidden":"true",children:"◆"}),l.jsx("span",{className:"ide-file-name",children:L.shortName}),l.jsx("code",{className:"ide-file-badge",children:Be})]})},L.slug)})})]}),l.jsxs("div",{className:"ide-folder-group",children:[l.jsxs("div",{className:"ide-folder-title",children:[l.jsx("span",{className:"ide-chevron","aria-hidden":"true",children:"▾"}),l.jsx("span",{children:"RUNTIME-INVARIANTS"})]}),l.jsx("ul",{className:"ide-file-list",role:"listbox","aria-label":"Runtime architectural invariants",children:at.map(L=>{const we=L.slug===k.slug,Be=L.gates(be,j).length;return l.jsx("li",{children:l.jsxs("button",{type:"button",role:"option","aria-selected":we,className:`ide-file-btn${we?" is-selected":""}`,onClick:()=>Re(L.slug),children:[l.jsx("span",{className:"ide-file-icon ide-file-icon--runtime","aria-hidden":"true",children:"⚡"}),l.jsx("span",{className:"ide-file-name",children:L.shortName}),l.jsx("code",{className:"ide-file-badge",children:Be})]})},L.slug)})})]})]}),l.jsxs("div",{className:"ide-sidebar-foot",children:[l.jsx("div",{className:"ide-sidebar-foot-label",children:"Graph Layers Active"}),l.jsx("div",{className:"ide-layer-pills",children:k.graphLayersUsed.map(L=>l.jsx("code",{className:"ide-layer-tag",children:L},L))})]})]}),l.jsxs("div",{className:"ide-main",children:[l.jsxs("div",{className:"ide-tabs-bar",children:[l.jsxs("div",{className:"ide-tabs",role:"tablist","aria-label":"Architecture inspector views",children:[l.jsxs("button",{type:"button",role:"tab","aria-selected":h==="overview",className:`ide-tab${h==="overview"?" is-active":""}`,onClick:()=>Q("overview"),children:[l.jsx("span",{className:"ide-tab-icon ide-tab-icon--md","aria-hidden":"true",children:"M↓"}),l.jsx("span",{children:"OVERVIEW.md"})]}),l.jsxs("button",{type:"button",role:"tab","aria-selected":h==="topology",className:`ide-tab${h==="topology"?" is-active":""}`,onClick:()=>Q("topology"),children:[l.jsx("span",{className:"ide-tab-icon ide-tab-icon--graph","aria-hidden":"true",children:"◈"}),l.jsx("span",{children:"TOPOLOGY.graph"})]}),l.jsxs("button",{type:"button",role:"tab","aria-selected":h==="yaml",className:`ide-tab${h==="yaml"?" is-active":""}`,onClick:()=>Q("yaml"),children:[l.jsx("span",{className:"ide-tab-icon ide-tab-icon--yaml","aria-hidden":"true"}),l.jsxs("span",{children:[k.slug,".gates.yaml"]}),l.jsx("code",{className:"ide-tab-count",children:Oe.length})]})]}),l.jsx("div",{className:"ide-tab-actions",children:l.jsx("button",{type:"button",className:"ide-mini-btn",onClick:Pe,title:"Copy YAML gate pack to clipboard",children:Ce==="Copied YAML"?"Copied!":"Copy YAML"})})]}),l.jsxs("div",{className:"ide-breadcrumbs","aria-label":"Breadcrumb",children:[l.jsx("span",{children:"architectures"}),l.jsx("span",{className:"ide-crumb-sep",children:"/"}),l.jsx("span",{children:k.category==="structural"?"structural-styles":"runtime-invariants"}),l.jsx("span",{className:"ide-crumb-sep",children:"/"}),l.jsx("strong",{className:"ide-crumb-current",children:k.slug}),l.jsx("span",{className:"ide-crumb-sep",children:"/"}),l.jsx("code",{children:h==="overview"?"OVERVIEW.md":h==="topology"?"TOPOLOGY.graph":`${k.slug}.gates.yaml`})]}),l.jsxs("div",{className:"ide-editor-stage",children:[h==="overview"&&l.jsx(wu,{pack:k,gatesCount:Oe.length,stackLabel:be.label,onOpenTopology:()=>Q("topology"),onOpenAllYaml:()=>{de(null),Q("yaml")}}),h==="topology"&&l.jsx(Su,{pack:k,layers:Ae,gates:Oe,stackLabel:be.label,onInspectGate:Ge}),h==="yaml"&&l.jsxs("div",{className:"ide-yaml-view",children:[l.jsxs("div",{className:"ide-yaml-toolbar",children:[l.jsxs("div",{className:"ide-gate-filter-pills",children:[l.jsxs("button",{type:"button",className:`ide-stack-pill${X===null?" is-active":""}`,onClick:()=>de(null),children:["All Gates (",Oe.length,")"]}),Oe.map(L=>l.jsx("button",{type:"button",className:`ide-stack-pill${X===L.id?" is-active":""}`,onClick:()=>de(L.id),children:L.id},L.id))]}),l.jsxs("div",{className:"ide-yaml-actions",children:[l.jsx("button",{type:"button",className:"ide-mini-btn",onClick:Pe,children:Ce==="Copied YAML"?"Copied!":"Copy YAML"}),l.jsx("button",{type:"button",className:"ide-mini-btn",onClick:ct,children:"Download .yaml"})]})]}),l.jsxs("div",{className:"ide-code-editor",role:"region","aria-label":"Gate YAML source",children:[l.jsx("div",{className:"ide-line-gutter","aria-hidden":"true",children:pe.map((L,we)=>l.jsx("span",{children:we+1},we))}),l.jsx("pre",{className:"ide-yaml-pre",children:l.jsx("code",{children:V})})]})]})]}),l.jsxs("div",{className:`ide-bottom-panel${ke?" is-open":" is-collapsed"}`,children:[l.jsxs("div",{className:"ide-bottom-bar",children:[l.jsxs("div",{className:"ide-bottom-tabs",role:"tablist","aria-label":"Bottom panel views",children:[l.jsxs("button",{type:"button",role:"tab","aria-selected":te==="problems",className:`ide-bottom-tab${te==="problems"?" is-active":""}`,onClick:()=>{ve("problems"),he(!0)},children:[l.jsx("span",{children:"PROBLEMS"}),l.jsx("code",{className:"ide-problem-count",children:Oe.length})]}),l.jsx("button",{type:"button",role:"tab","aria-selected":te==="terminal",className:`ide-bottom-tab${te==="terminal"?" is-active":""}`,onClick:()=>{ve("terminal"),he(!0)},children:l.jsx("span",{children:"TERMINAL — ce gate check"})})]}),l.jsx("button",{type:"button",className:"ide-bottom-toggle",onClick:()=>he(L=>!L),"aria-label":ke?"Collapse bottom panel":"Expand bottom panel",children:ke?"▾ Hide Panel":"▴ Show Panel"})]}),ke&&l.jsx("div",{className:"ide-bottom-body",children:te==="problems"?l.jsx("ul",{className:"ide-problems-list","aria-label":"Simulated gate violations",children:Oe.map(L=>l.jsx("li",{className:"ide-problem-row",children:l.jsxs("button",{type:"button",className:"ide-problem-btn",onClick:()=>Ge(L.id),children:[l.jsx("span",{className:`ide-problem-sev ${L.severity==="Error"?"is-err":"is-warn"}`,children:L.severity==="Error"?"✕":"⚠"}),l.jsx("code",{className:"ide-problem-id",children:L.id}),l.jsxs("span",{className:"ide-problem-msg",children:[l.jsx("strong",{children:L.sampleViolation.symbol})," →"," ",l.jsx("code",{children:L.sampleViolation.target})," — ",L.sampleViolation.detail]}),l.jsxs("code",{className:"ide-problem-loc",children:[L.sampleViolation.file,":",L.sampleViolation.line]})]})},L.id))}):l.jsx("pre",{className:"ide-terminal-pre",children:l.jsxs("code",{children:[`$ ${Z}
`,`--- Semantic Invariant Gates Evaluation (${k.title}) ---
`,Oe.map(L=>`[FAIL] ${L.id.padEnd(14)} ${L.severity.padEnd(8)} ${L.sampleViolation.file}:${L.sampleViolation.line}  ${L.sampleViolation.symbol} -> ${L.sampleViolation.target}`).join(`
`),`

# Freeze legacy brownfield violations as a SHA-256 baseline ratchet so CI blocks only net-new leaks:
`,`$ ce gate ratchet --update
`,`✓ Recorded ${Oe.length} baseline fingerprints in .codeexplorer/baselines/. Subsequent 'ce gate check' exits 0.`]})})})]})]})]}),l.jsxs("div",{className:"ide-statusbar",children:[l.jsxs("div",{className:"ide-statusbar-left",children:[l.jsx("span",{className:"ide-status-remote",children:"⇄ CodeExplorer Graph Engine"}),l.jsx("span",{children:"main*"}),l.jsxs("span",{children:["✗ ",Oe.filter(L=>L.severity==="Error").length," Errors · ⚠"," ",Oe.filter(L=>L.severity==="Warning").length," Warnings"]})]}),l.jsxs("div",{className:"ide-statusbar-right",children:[l.jsxs("span",{children:["Stack: ",be.label]}),l.jsx("span",{children:"OpenCypher v2 Node-First"}),l.jsx("span",{children:"SQLite .codeexplorer/graph.db"})]})]})]})})})]})};function Tu(){if(typeof window>"u"||typeof document>"u")return()=>{};for(var d={commonProperties:[{name:"id",sourceName:"Id",type:"string",description:"Globally unique deterministic URN identifier of the node."},{name:"name",sourceName:"Name",type:"string",description:"Human-readable symbol, contract, or resource name."},{name:"path",sourceName:"Path",type:"string",description:"Workspace-relative path of the container or declaration."},{name:"project_id",sourceName:"ProjectId",type:"string",description:"URN of the enclosing compilation project, when applicable."}],nodes:[{kind:"Domain",pluralLabel:"Domains",layerId:5,layerKey:"L5_logical",icon:"globe",order:1,idScheme:"{workspaceId}:dom:{domainKey}",purpose:"Represents a high-level business domain (e.g. Core, Supporting, Generic, Shared) containing one or more bounded contexts in Level 5 (Logical Structure).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The canonical domain name."},{name:"display_name",sourceName:"DisplayName",type:"string",description:"The human-readable display name of the domain."},{name:"domain_type",sourceName:"DomainType",type:"enum (DomainType)",description:"The DDD classification of the domain (Core, Supporting, Generic, Shared)."},{name:"summary",sourceName:"Summary",type:"string",description:"A concise summary of the domain's business responsibility."},{name:"path",sourceName:"Path",type:"string",description:"Primary path associated with the domain."}],outEdges:[{rel:"DEPENDS_ON",toKind:"Domain",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",toKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"Domain",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."}],sampleCypher:`MATCH (n:Domain)-[:DEPENDS_ON]->(t:Domain)
RETURN n.name, n.display_name, n.domain_type, t.name
LIMIT 25`},{kind:"BoundedContext",pluralLabel:"Bounded Contexts",layerId:5,layerKey:"L5_logical",icon:"layers",order:2,idScheme:"{workspaceId}:bc:{contextKey}",purpose:"Represents a DDD Bounded Context in Level 5 (Logical Structure) that encapsulates semantic workloads, data stores, topics, and domain types.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The canonical bounded context name."},{name:"display_name",sourceName:"DisplayName",type:"string",description:"The human-readable display name of the bounded context."},{name:"domain_id",sourceName:"DomainId",type:"string",description:"The parent domain ID that owns this bounded context."},{name:"summary",sourceName:"Summary",type:"string",description:"A concise summary of the bounded context's business responsibility."},{name:"purity_percentage",sourceName:"PurityPercentage",type:"number",description:"Domain purity percentage (0-100)."},{name:"path",sourceName:"Path",type:"string",description:"Primary path associated with the bounded context."}],outEdges:[{rel:"DEPENDS_ON",toKind:"BoundedContext",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",toKind:"Database",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Module",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Topic",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DEFINES",toKind:"Type",category:"structural",description:"Indicates that an entity defines a particular property or configuration."},{rel:"OWNS",toKind:"Table",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"BoundedContext",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",fromKind:"Domain",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:BoundedContext)-[:DEPENDS_ON]->(t:BoundedContext)
RETURN n.name, n.display_name, n.domain_id, t.name
LIMIT 25`},{kind:"Module",pluralLabel:"Software Modules",layerId:4,layerKey:"L4_semantic",icon:"package",order:0,idScheme:"{workspaceId}:{kindPrefix}:{moduleName}",purpose:"Abstract base software module encompassing executable workloads, shared libraries, and automated test suites.",isAbstract:!0,subtypes:["Workload","Library","TestSuite"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name."},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace."},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language."},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module."},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework."}],outEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"CONTAINS",toKind:"Cache",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Job",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Message",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Policy",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Transaction",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"OWNS",toKind:"Table",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns."}],inEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies."},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Module)-[:DEPENDS_ON]->(t:Library)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Workload",pluralLabel:"Runtime Workloads",layerId:4,layerKey:"L4_semantic",icon:"server",order:1,idScheme:"{workspaceId}:{workloadPrefix}:{workloadName}",purpose:"Abstract executable runtime workload encompassing backend services, client applications, background workers, and CLI tools.",isAbstract:!0,extends:"Module",ancestors:["Module"],subtypes:["App","Service","Worker","CliTool"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"EXPOSES",toKind:"Job",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."},{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"SERVICE_CALL",toKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to."},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."}],inEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."}],inheritedOutEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Cache",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Job",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Message",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Policy",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Transaction",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"OWNS",toKind:"Table",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Workload)-[:EXPOSES]->(t:Job)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"App",pluralLabel:"Applications",layerId:4,layerKey:"L4_semantic",icon:"browser",order:2,idScheme:"{workspaceId}:app:{appName}",purpose:"Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular, Electron).",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[],inEdges:[],inheritedOutEdges:[{rel:"EXPOSES",toKind:"Job",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Cache",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Job",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Message",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Policy",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Transaction",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"OWNS",toKind:"Table",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",inheritedFrom:"Workload"},{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:App)-[:EXPOSES]->(t:Job)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Service",pluralLabel:"Services",layerId:4,layerKey:"L4_semantic",icon:"server-process",order:3,idScheme:"{workspaceId}:s:{serviceName}",purpose:"Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server).",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"CALLED_BY",toKind:"Function",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"EXPOSES",toKind:"Endpoint",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."}],inEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."}],inheritedOutEdges:[{rel:"EXPOSES",toKind:"Job",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Cache",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Job",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Message",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Policy",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Transaction",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"OWNS",toKind:"Table",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",inheritedFrom:"Workload"},{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Service)-[:CALLED_BY]->(t:Function)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Worker",pluralLabel:"Workers",layerId:4,layerKey:"L4_semantic",icon:"gear",order:4,idScheme:"{workspaceId}:w:{workerName}",purpose:"Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service).",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"SUBSCRIBES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."}],inEdges:[],inheritedOutEdges:[{rel:"EXPOSES",toKind:"Job",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Cache",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Job",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Message",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Policy",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Transaction",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"OWNS",toKind:"Table",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",inheritedFrom:"Workload"},{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Worker)-[:SUBSCRIBES_TO]->(t:Topic)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"CliTool",pluralLabel:"CLI Tools",layerId:4,layerKey:"L4_semantic",icon:"terminal",order:5,idScheme:"{workspaceId}:cli:{toolName}",purpose:"Represents a command-line tool, developer script, or administrative CLI utility.",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[],inEdges:[],inheritedOutEdges:[{rel:"EXPOSES",toKind:"Job",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Cache",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Job",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Message",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Policy",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Transaction",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"OWNS",toKind:"Table",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",inheritedFrom:"Workload"},{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:CliTool)-[:EXPOSES]->(t:Job)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Library",pluralLabel:"Libraries & SDKs",layerId:4,layerKey:"L4_semantic",icon:"library",order:6,idScheme:"{workspaceId}:lib:{libraryName}",purpose:"Represents a shared library, utility module, DTO package, or domain contract reused across services.",extends:"Module",ancestors:["Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[],inEdges:[{rel:"DEPENDS_ON",fromKind:"Module",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."}],inheritedOutEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Cache",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Job",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Message",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Policy",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Transaction",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"OWNS",toKind:"Table",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Library)-[:DEPENDS_ON]->(t:Library)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"TestSuite",pluralLabel:"Test Suites",layerId:4,layerKey:"L4_semantic",icon:"beaker",order:7,idScheme:"{workspaceId}:suite:{suiteName}",purpose:"Represents an automated test project, runner suite, or test module (e.g. NUnit/xUnit test project, Jest test suite, pytest suite).",extends:"Module",ancestors:["Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"TESTS",toKind:"Module",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies."}],inEdges:[],inheritedOutEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Cache",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Job",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Message",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Policy",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"CONTAINS",toKind:"Transaction",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"},{rel:"OWNS",toKind:"Table",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:TestSuite)-[:TESTS]->(t:Module)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Endpoint",pluralLabel:"API & Message Endpoints (REST, gRPC, WS, MSG)",layerId:4,layerKey:"L4_semantic",icon:"radio-tower",order:10,idScheme:"{workspaceId}:ep:{httpMethod}:{routeTemplate}",purpose:"Represents an exposed external API or message contract endpoint (REST, gRPC, GraphQL, WebSocket, WCF, or Broker Consumer).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The endpoint name (e.g. GET /api/orders, MSG:SubmitOrder)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"http_method",sourceName:"HttpMethod",type:"string",description:"The HTTP Verb or contract verb (GET, POST, PUT, DELETE, ALL, MSG, WS, RPC)."},{name:"route_template",sourceName:"RouteTemplate",type:"string",description:"The declared route template or message contract name."},{name:"protocol",sourceName:"Protocol",type:"string",description:"The contract protocol (REST, gRPC, GraphQL, WS, WCF, MSG, RPC)."},{name:"is_anonymous",sourceName:"IsAnonymous",type:"boolean",description:"Whether endpoint allows unauthenticated anonymous access."},{name:"required_roles",sourceName:"RequiredRoles",type:"string",description:"Comma-separated required roles or permissions."},{name:"policies",sourceName:"Policies",type:"string",description:"Authorization policies guarding the endpoint."},{name:"request_type",sourceName:"RequestType",type:"string",description:"The request payload type (e.g. StartRideRequest, CreateOrderDto)."},{name:"response_type",sourceName:"ResponseType",type:"string",description:"The response payload type (e.g. RideDto, OrderResponse)."},{name:"operation_type",sourceName:"OperationType",type:"enum (OperationType)",description:"The operation type for GraphQL (Query, Mutation, Subscription) or gRPC (Unary, Streaming)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring the endpoint."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the endpoint declaration."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed source line number of the route or contract mapping."},{name:"operation",sourceName:"Operation",type:"string",description:"Operation or handler action name associated with this endpoint."},{name:"evidence_kind",sourceName:"EvidenceKind",type:"string",description:"Extraction provenance kind (endpoint, grpc, graphql, ws, consumer)."},{name:"graphql_field",sourceName:"GraphqlField",type:"string",description:"Exposed GraphQL root field name when protocol is GraphQL."},{name:"graphql_sdl",sourceName:"IsGraphqlSdl",type:"boolean",description:"Indicates whether the endpoint was extracted from a GraphQL SDL schema."},{name:"status_code",sourceName:"StatusCode",type:"string",description:"Declared HTTP response status codes (e.g. 200, 201, 404)."},{name:"summary",sourceName:"Summary",type:"string",description:"OpenAPI / Swagger or docstring endpoint summary."},{name:"canonical_route",sourceName:"CanonicalRoute",type:"string",description:"Normalized canonical route template with typed parameter slots (e.g. /users/{id:uuid}/orders)."},{name:"canonical_shape",sourceName:"CanonicalShape",type:"string",description:"Structural canonical route shape for O(1) endpoint matching (e.g. /users/{}/orders)."}],outEdges:[{rel:"CALLS",toKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"EXPOSED_BY",toKind:"Function",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"EXPOSED_BY",toKind:"Type",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"GUARDED_BY",toKind:"Policy",category:"control_flow",description:"Links an Endpoint or CQRS Message contract to a Layer 4 authorization Policy or Guard."},{rel:"TRIGGERS",toKind:"Function",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"ATTRIBUTED_TO",toKind:"Database",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"ATTRIBUTED_TO",toKind:"ExternalService",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"ATTRIBUTED_TO",toKind:"Query",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"PASSES_LITERAL",toKind:"CallLiteral",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"SUBSCRIBES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",toKind:"Project",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARES",toKind:"Scope",category:"structural",description:"Indicates that a container entity declares a sub-entity."}],inEdges:[{rel:"CALLS_ENDPOINT",fromKind:"ExternalService",category:"control_flow",description:"Links an external API call to the target HTTP endpoint it invokes."},{rel:"EXPOSES",fromKind:"Service",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."}],sampleCypher:`MATCH (n:Endpoint)-[:CALLS]->(t:Function)
RETURN n.name, n.http_method, n.route_template, t.name
LIMIT 25`},{kind:"Topic",pluralLabel:"Message Topics & Queues",layerId:4,layerKey:"L4_semantic",icon:"mail",order:11,idScheme:"{workspaceId}:top:{brokerType}:{topicName}",purpose:"Represents a message queue, event exchange, or topic boundary.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the topic or exchange."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"broker_type",sourceName:"BrokerType",type:"enum (BrokerType)",description:"The broker system type (rabbitmq, kafka, sqs, in-memory)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring or referencing the topic."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed source line of the publish or subscribe site."},{name:"messaging_direction",sourceName:"MessagingDirection",type:"string",description:"Direction of broker interaction (publish, subscribe, both)."},{name:"scope",sourceName:"Scope",type:"string",description:"Broker scope or virtual host / namespace."},{name:"message_type",sourceName:"MessageType",type:"string",description:"Payload or event contract type published/consumed on this topic."},{name:"resource_type",sourceName:"ResourceType",type:"string",description:"IaC resource type when provisioned by Terraform or cloud templates."},{name:"provisioned_by",sourceName:"ProvisionedBy",type:"string",description:"IaC tool or engine provisioning this topic (e.g. terraform)."},{name:"canonical_topic",sourceName:"CanonicalTopic",type:"string",description:"Normalized canonical topic URI preserving provider namespace and topic name."},{name:"canonical_shape",sourceName:"CanonicalShape",type:"string",description:"Structural canonical topic shape for cross-service pub/sub matching."}],outEdges:[{rel:"PUBLISHED_BY",toKind:"Function",category:"data_flow",description:"Links a topic to the function that publishes to it."},{rel:"SUBSCRIBED_BY",toKind:"Function",category:"data_flow",description:"Links a topic to the function that subscribes to it."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"PUBLISHES_TO",fromKind:"Workload",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to."},{rel:"SUBSCRIBES_TO",fromKind:"Endpoint",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{rel:"SUBSCRIBES_TO",fromKind:"Worker",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Topic)-[:PUBLISHED_BY]->(t:Function)
RETURN n.name, n.broker_type, n.file_path, t.name
LIMIT 25`},{kind:"ExternalService",pluralLabel:"External Services & Resources",layerId:4,layerKey:"L4_semantic",icon:"globe",order:12,idScheme:"{workspaceId}:es:{protocol}:{host}",purpose:"Represents an external API, managed cloud resource, or remote service dependency outside the workspace (REST, gRPC, GraphQL, S3, Stripe, Auth0).",properties:[{name:"name",sourceName:"Name",type:"string",description:"Logical host or external client dependency name."},{name:"protocol",sourceName:"Protocol",type:"string",description:"Outbound transport protocol (HTTP, HTTPS, gRPC, GraphQL, WS, WCF)."},{name:"domain_or_service",sourceName:"DomainOrService",type:"string",description:"Target domain hostname or logical service identifier."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file issuing the outbound call."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed line number of the outbound client call."},{name:"base_url",sourceName:"BaseUrl",type:"string",description:"Configured base URL or service address."},{name:"url",sourceName:"Url",type:"string",description:"Resolved outbound request URL or path template."},{name:"config_key",sourceName:"ConfigKey",type:"string",description:"Configuration setting or environment variable supplying the host URL."},{name:"graphql_field",sourceName:"GraphqlField",type:"string",description:"Queried GraphQL field name on the remote service."},{name:"operation_type",sourceName:"OperationType",type:"string",description:"Remote operation kind (Query, Mutation, Unary, Streaming)."},{name:"method",sourceName:"Method",type:"string",description:"Outbound HTTP verb or RPC method name."},{name:"provider",sourceName:"Provider",type:"string",description:"Cloud platform or SaaS vendor hosting this external service (e.g. AWS, GCP, Azure, Stripe, Auth0)."},{name:"resource_role",sourceName:"ResourceRole",type:"string",description:"Operational role when representing a managed cloud resource (storage, secrets, auth, compute, cdn, ai)."},{name:"is_external",sourceName:"IsExternal",type:"boolean",description:"True when the target resolves outside workspace services."},{name:"canonical_route",sourceName:"CanonicalRoute",type:"string",description:"Normalized canonical outbound route template with typed parameter or literal slots (e.g. /users/{=42:int}/orders)."},{name:"canonical_shape",sourceName:"CanonicalShape",type:"string",description:"Structural canonical outbound route shape for O(1) endpoint matching (e.g. /users/{}/orders)."}],outEdges:[{rel:"CALLED_BY",toKind:"Function",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"CALLS_ENDPOINT",toKind:"Endpoint",category:"control_flow",description:"Links an external API call to the target HTTP endpoint it invokes."},{rel:"TRANSITIVELY_CALLS",toKind:"Function",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",toKind:"Type",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",toKind:"Project",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"ATTRIBUTED_TO",fromKind:"Endpoint",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."}],sampleCypher:`MATCH (n:ExternalService)-[:CALLED_BY]->(t:Function)
RETURN n.name, n.protocol, n.domain_or_service, t.name
LIMIT 25`},{kind:"Message",pluralLabel:"CQRS & Domain Messages",layerId:4,layerKey:"L4_semantic",icon:"mail",order:12,idScheme:"{workspaceId}:msg:{subKind}:{messageName}",purpose:"Represents a CQRS or application message contract (Query, Command, or Domain/Integration Event) handled via a mediator, bus, or domain dispatcher.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The message contract name (e.g. GetMyWalletQuery, CreateOrderCommand)."},{name:"path",sourceName:"Path",type:"string",description:"The workspace-relative path of the declaring file or handler file."},{name:"sub_kind",sourceName:"SubKind",type:"enum (OperationType)",description:"The semantic message role (query, command, event, notification)."},{name:"framework",sourceName:"Framework",type:"string",description:"Detected CQRS or messaging framework (e.g. mediatr, nestjs-cqrs, axon, symfony-messenger, spring, cqrs)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring the message or handler."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the message declaration."}],outEdges:[{rel:"GUARDED_BY",toKind:"Policy",category:"control_flow",description:"Links an Endpoint or CQRS Message contract to a Layer 4 authorization Policy or Guard."},{rel:"TRIGGERS",toKind:"Function",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"IMPLEMENTED_BY",toKind:"Type",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"HANDLES_COMMAND",fromKind:"Function",category:"control_flow",description:"Links a command handler function to the Layer 4 CQRS Command message contract it executes."},{rel:"HANDLES_EVENT",fromKind:"Function",category:"control_flow",description:"Links an event or notification handler function to the Layer 4 Domain or Integration Event message contract it processes."},{rel:"HANDLES_QUERY",fromKind:"Function",category:"control_flow",description:"Links a query handler function to the Layer 4 CQRS Query message contract it executes."},{rel:"CONTAINS",fromKind:"Module",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Message)-[:GUARDED_BY]->(t:Policy)
RETURN n.name, n.sub_kind, n.framework, t.name
LIMIT 25`},{kind:"Cloud",pluralLabel:"Cloud Platforms & Vendors",layerId:4,layerKey:"L4_semantic",icon:"cloud",order:13,idScheme:"{workspaceId}:cloud:{provider}",purpose:"Represents a cloud platform, managed infrastructure provider, or SaaS vendor hosting external services, databases, brokers, or workloads (e.g. AWS, GCP, Azure, Cloudflare, Stripe, Supabase).",properties:[{name:"name",sourceName:"Name",type:"string",description:"Cloud platform or vendor display name (e.g. AWS, GCP, Azure, Cloudflare, Stripe)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"provider",sourceName:"Provider",type:"string",description:"Normalized cloud or vendor identifier (aws, gcp, azure, cloudflare, stripe, auth0, supabase, firebase)."},{name:"region",sourceName:"Region",type:"string",description:"Configured cloud region when declared (e.g. us-east-1, europe-west1)."},{name:"category",sourceName:"Category",type:"string",description:"Vendor category (hyperscaler, saas, edge, managed_data)."}],outEdges:[{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"HOSTED_IN",fromKind:"CloudResource",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"Database",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"ExternalService",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"Topic",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"Workload",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."}],sampleCypher:`MATCH (n:Cloud)-[:DECLARED_IN]->(t:File)
RETURN n.name, n.provider, n.region, t.name
LIMIT 25`},{kind:"Policy",pluralLabel:"Authorization Policies & Guards",layerId:4,layerKey:"L4_semantic",icon:"lock",order:13,idScheme:"{workspaceId}:pol:{subKind}:{policyName}",purpose:"Represents an authentication, role, permission, guard, or explicit public-access security policy protecting endpoints and CQRS messages.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The policy, role, guard, or permission name (e.g. Authenticated, Admin, JwtAuthGuard, Anonymous)."},{name:"path",sourceName:"Path",type:"string",description:"The workspace-relative path of the declaring guard or endpoint file."},{name:"sub_kind",sourceName:"SubKind",type:"enum (PolicyKind)",description:"The authorization policy classification (authenticated, role, permission, custom_guard, anonymous)."},{name:"requirement",sourceName:"Requirement",type:"string",description:"Raw role, claim, SpEL expression, or permission requirement."},{name:"framework",sourceName:"Framework",type:"string",description:"Detected security framework (e.g. aspnetcore-auth, nestjs-guards, spring-security, fastapi-security)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring or referencing the policy."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the policy declaration or guard."}],outEdges:[{rel:"ENFORCED_BY",toKind:"Function",category:"control_flow",description:"Links a Layer 4 authorization Policy to the guard or middleware function enforcing it."},{rel:"IMPLEMENTED_BY",toKind:"Type",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"GUARDED_BY",fromKind:"Endpoint",category:"control_flow",description:"Links an Endpoint or CQRS Message contract to a Layer 4 authorization Policy or Guard."},{rel:"GUARDED_BY",fromKind:"Message",category:"control_flow",description:"Links an Endpoint or CQRS Message contract to a Layer 4 authorization Policy or Guard."},{rel:"CONTAINS",fromKind:"Module",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Policy)-[:ENFORCED_BY]->(t:Function)
RETURN n.name, n.sub_kind, n.requirement, t.name
LIMIT 25`},{kind:"CloudResource",pluralLabel:"Cloud Resources",layerId:4,layerKey:"L4_semantic",icon:"cloud-upload",order:14,idScheme:"{workspaceId}:p:{relativeProjectDir}:cr:{resourceAddress}",purpose:"Represents a provisioned or referenced cloud infrastructure resource (e.g. RDS instance, SQS queue, S3 bucket, VPC, IAM role, KMS key, Kubernetes workload) hosted in a Cloud platform.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The canonical resource address or name (e.g. aws_s3_bucket.assets)."},{name:"resource_type",sourceName:"ResourceType",type:"string",description:"The provider resource type identifier (e.g. aws_s3_bucket, aws_db_instance, google_sql_database_instance)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"provider",sourceName:"Provider",type:"string",description:"Cloud or infrastructure provider (e.g. aws, gcp, azure, kubernetes, digitalocean, cloudflare)."},{name:"resource_role",sourceName:"ResourceRole",type:"string",description:"Operational role of the cloud resource (database, messaging, storage, compute, network, security, secrets, cdn, dns, container, monitoring)."},{name:"mode",sourceName:"Mode",type:"string",description:"Declaration mode of the resource (managed or data)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring the cloud resource."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed starting line number of the resource declaration."},{name:"line_end",sourceName:"LineEnd",type:"integer",description:"1-indexed ending line number of the resource declaration."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the resource declaration."},{name:"end_line",sourceName:"EndLine",type:"integer",description:"1-indexed ending line number of the resource declaration."}],outEdges:[{rel:"DEPENDS_ON",toKind:"CloudResource",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPENDS_ON",toKind:"Member",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPLOYS",toKind:"Database",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"DEPLOYS",toKind:"ExternalService",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"DEPLOYS",toKind:"Topic",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"DEPLOYS",toKind:"Workload",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"CloudResource",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"IMPLEMENTED_BY",fromKind:"Database",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"ExternalService",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"Topic",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"Workload",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."}],sampleCypher:`MATCH (n:CloudResource)-[:DEPENDS_ON]->(t:CloudResource)
RETURN n.name, n.resource_type, n.provider, t.name
LIMIT 25`},{kind:"Job",pluralLabel:"Scheduled Jobs & Workflows",layerId:4,layerKey:"L4_semantic",icon:"clock",order:14,idScheme:"{workspaceId}:job:{subKind}:{jobName}",purpose:"Represents a scheduled cron task, recurring background worker, queued job, or durable workflow/activity.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The scheduled job, task, or workflow name."},{name:"path",sourceName:"Path",type:"string",description:"The workspace-relative path of the declaring file."},{name:"sub_kind",sourceName:"SubKind",type:"enum (JobKind)",description:"The execution model of the job (cron, recurring, fire_and_forget, workflow, activity, background_worker)."},{name:"schedule_expr",sourceName:"ScheduleExpr",type:"string",description:"Cron expression or fixed interval specification when applicable."},{name:"queue",sourceName:"Queue",type:"string",description:"Target job queue or task routing key when applicable."},{name:"framework",sourceName:"Framework",type:"string",description:"Detected scheduler or workflow framework (e.g. spring-scheduled, hangfire, quartz, nestjs-schedule, celery, temporal)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring the job."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the job handler."}],outEdges:[{rel:"TRIGGERS",toKind:"Function",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"EXPOSES",fromKind:"Workload",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."},{rel:"CONTAINS",fromKind:"Module",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Job)-[:TRIGGERS]->(t:Function)
RETURN n.name, n.sub_kind, n.schedule_expr, t.name
LIMIT 25`},{kind:"Database",pluralLabel:"Databases & Storage",layerId:4,layerKey:"L4_semantic",icon:"database",order:20,idScheme:"{workspaceId}:db:{dbType}:{dbName}",purpose:"Represents a database server instance or catalog configured by a workload and containing one or more DbSchema namespaces.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The database engine or catalog name."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"db_type",sourceName:"DbType",type:"enum (DatabaseType)",description:"The database system type (sqlserver, postgres, sqlite, mongodb, neo4j)."},{name:"display_name",sourceName:"DisplayName",type:"string",description:"Human-readable database or catalog display name."},{name:"engine",sourceName:"Engine",type:"string",description:"Detected database engine (e.g. postgresql, sqlserver, sqlite, mysql, mongodb, redis)."},{name:"technology",sourceName:"Technology",type:"string",description:"ORM, driver, or client technology accessing the database (e.g. EF Core, Dapper, Prisma, TypeORM)."},{name:"schema",sourceName:"Schema",type:"string",description:"Physical database schema name when declared (e.g. public, dbo, billing)."},{name:"provider",sourceName:"Provider",type:"string",description:"Database driver or provider package identifier."},{name:"role",sourceName:"Role",type:"string",description:"Storage role classification (primary, cache, analytics, read-replica)."},{name:"config_key",sourceName:"ConfigKey",type:"string",description:"Configuration key or connection string name bound to this database."},{name:"from_config",sourceName:"FromConfig",type:"boolean",description:"Indicates whether the database connection was discovered from configuration files."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether this database instance is used exclusively in tests."},{name:"is_orm",sourceName:"IsOrm",type:"boolean",description:"Indicates whether access was inferred via an ORM entity/DbContext mapping."},{name:"resource_type",sourceName:"ResourceType",type:"string",description:"IaC resource type when provisioned by Terraform or cloud templates."},{name:"provisioned_by",sourceName:"ProvisionedBy",type:"string",description:"IaC tool or engine provisioning this database (e.g. terraform)."},{name:"canonical_dsn",sourceName:"CanonicalDsn",type:"string",description:"Normalized canonical database DSN address."},{name:"canonical_shape",sourceName:"CanonicalShape",type:"string",description:"Structural canonical database shape for cross-service database consolidation."}],outEdges:[{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"CONTAINS",toKind:"DbSchema",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"ATTRIBUTED_TO",fromKind:"Endpoint",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"USES_DB",fromKind:"Workload",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes."},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Database)-[:TARGETED_BY]->(t:CallArgument)
RETURN n.name, n.db_type, n.display_name, t.name
LIMIT 25`},{kind:"DbSchema",pluralLabel:"Database Schemas",layerId:4,layerKey:"L4_semantic",icon:"symbol-namespace",order:21,idScheme:"{workspaceId}:db:{dbType}:{dbName}:schema:{schemaName}",purpose:"Represents a logical or physical schema namespace within a database catalog owning tables, views, and stored procedures (e.g. public, dbo, billing, main).",properties:[{name:"name",sourceName:"Name",type:"string",description:"Database schema name (e.g. public, dbo, billing, main)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"engine",sourceName:"Engine",type:"string",description:"Database engine owning this schema (e.g. PostgreSQL, SqlServer, SQLite)."},{name:"database_id",sourceName:"DatabaseId",type:"string",description:"URN of the parent Database catalog containing this schema."},{name:"is_default",sourceName:"IsDefault",type:"boolean",description:"True when synthesized for the engine's default schema (public, dbo, main)."}],outEdges:[{rel:"CONTAINS",toKind:"Procedure",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Table",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"CONTAINS",fromKind:"Database",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:DbSchema)-[:CONTAINS]->(t:Procedure)
RETURN n.name, n.engine, n.database_id, t.name
LIMIT 25`},{kind:"Table",pluralLabel:"Tables & Views",layerId:4,layerKey:"L4_semantic",icon:"table",order:22,idScheme:"{workspaceId}:tbl:{tableName}",purpose:"Represents a physical database table.",properties:[{name:"name",sourceName:"Name",type:"string",description:"Physical database table or view name."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"engine",sourceName:"Engine",type:"string",description:"Database engine hosting this table (e.g. postgresql, sqlserver, sqlite)."},{name:"schema",sourceName:"Schema",type:"string",description:"Database schema qualifying this table (e.g. dbo, public, core)."},{name:"entity",sourceName:"EntityName",type:"string",description:"Mapped ORM entity or domain model type name, if applicable."},{name:"sub_kind",sourceName:"SubKind",type:"string",description:"Specific relational object kind (e.g. table, view, materialized_view)."},{name:"columns",sourceName:"ColumnsJson",type:"string",description:"Compact JSON array describing table columns (name, type, pk, nullable, unique, fk)."},{name:"sensitivity",sourceName:"Sensitivity",type:"enum (DataSensitivity)",description:"Data sensitivity classification (public, internal, pii, pci, secret)."},{name:"canonical_table",sourceName:"CanonicalTable",type:"string",description:"Normalized canonical table address preserving schema and table name."},{name:"canonical_shape",sourceName:"CanonicalShape",type:"string",description:"Structural canonical table shape for SQL and ORM matching."}],outEdges:[{rel:"TRIGGERS",toKind:"Procedure",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",toKind:"Query",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",toKind:"Type",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"READS_FROM",toKind:"Table",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"IMPLEMENTED_BY",toKind:"Type",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"REFERENCES",toKind:"Table",category:"type_dependency",description:"Links a child database table to a parent table via a foreign key constraint."}],inEdges:[{rel:"READS_FROM",fromKind:"Procedure",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from."},{rel:"READS_FROM",fromKind:"Query",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from."},{rel:"READS_FROM",fromKind:"Table",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from."},{rel:"WRITES_TO",fromKind:"Procedure",category:"data_flow",description:"Links a query or workload to the table or cloud resource it writes to."},{rel:"WRITES_TO",fromKind:"Query",category:"data_flow",description:"Links a query or workload to the table or cloud resource it writes to."},{rel:"DEPENDS_ON",fromKind:"Query",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"REFERENCES",fromKind:"Table",category:"type_dependency",description:"Links a child database table to a parent table via a foreign key constraint."},{rel:"CONTAINS",fromKind:"DbSchema",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"OWNS",fromKind:"BoundedContext",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns."},{rel:"OWNS",fromKind:"Module",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns."}],sampleCypher:`MATCH (n:Table)-[:TRIGGERS]->(t:Procedure)
RETURN n.name, n.engine, n.schema, t.name
LIMIT 25`},{kind:"Procedure",pluralLabel:"Stored Procedures",layerId:4,layerKey:"L4_semantic",icon:"database",order:23,idScheme:"{workspaceId}:proc:{procedureName}",purpose:"Represents a stored procedure in a database.",properties:[{name:"engine",sourceName:"Engine",type:"string",description:"Database engine hosting this routine (e.g. postgresql, sqlserver, mysql)."},{name:"schema",sourceName:"Schema",type:"string",description:"Database schema qualifying this routine (e.g. dbo, public)."},{name:"sub_kind",sourceName:"SubKind",type:"string",description:"Specific database routine kind (procedure, function, trigger)."},{name:"trigger_table",sourceName:"TriggerTable",type:"string",description:"Target table name that fires this trigger, if sub_kind is trigger."},{name:"trigger_event",sourceName:"TriggerEvent",type:"string",description:"Trigger timing and event (e.g. AFTER INSERT, BEFORE UPDATE)."}],outEdges:[{rel:"CALLS",toKind:"Procedure",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"READS_FROM",toKind:"Table",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"WRITES_TO",toKind:"Table",category:"data_flow",description:"Links a query or workload to the table or cloud resource it writes to."},{rel:"CONTAINS",toKind:"Query",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],inEdges:[{rel:"CALLS",fromKind:"Procedure",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"TRIGGERS",fromKind:"Table",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"CONTAINS",fromKind:"DbSchema",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Procedure)-[:CALLS]->(t:Procedure)
RETURN n.name, n.engine, n.schema, t.name
LIMIT 25`},{kind:"Cache",pluralLabel:"Caches & Regions",layerId:4,layerKey:"L4_semantic",icon:"zap",order:24,idScheme:"{workspaceId}:cache:{engine}:{regionOrKey}",purpose:"Represents an in-memory, distributed, or HTTP cache region/key with distinct read, write, and invalidation semantics.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The cache key prefix, region, or named cache store."},{name:"path",sourceName:"Path",type:"string",description:"The workspace-relative path of the declaring or primary accessing file."},{name:"sub_kind",sourceName:"SubKind",type:"enum (CacheEngine)",description:"The backing cache engine kind (memory, distributed, redis, memcached, hybrid, http_output)."},{name:"key_pattern",sourceName:"KeyPattern",type:"string",description:"Extracted cache key template or region name."},{name:"framework",sourceName:"Framework",type:"string",description:"Detected caching library or framework (e.g. stackexchange-redis, memory-cache, spring-cache, ioredis)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file referencing or configuring the cache."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the primary cache declaration or call."}],outEdges:[{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"INVALIDATES_CACHE",fromKind:"Function",category:"data_flow",description:"Links a function to a Layer 4 Cache region/key it evicts or deletes."},{rel:"READS_CACHE",fromKind:"Function",category:"data_flow",description:"Links a function to a Layer 4 Cache region/key it reads from."},{rel:"WRITES_CACHE",fromKind:"Function",category:"data_flow",description:"Links a function to a Layer 4 Cache region/key it populates or updates."},{rel:"CONTAINS",fromKind:"Module",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Cache)-[:ACCESSED_BY]->(t:Function)
RETURN n.name, n.sub_kind, n.key_pattern, t.name
LIMIT 25`},{kind:"Transaction",pluralLabel:"Transaction Boundaries",layerId:4,layerKey:"L4_semantic",icon:"shield-check",order:25,idScheme:"{workspaceId}:tx:{subKind}:{scopeName}",purpose:"Represents an explicit, declarative, ambient, or pipeline-behavior database transaction boundary.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The transaction boundary name (e.g. enclosing function or pipeline behavior)."},{name:"path",sourceName:"Path",type:"string",description:"The workspace-relative path of the declaring file."},{name:"sub_kind",sourceName:"SubKind",type:"enum (TransactionKind)",description:"How the transaction boundary is established (explicit, declarative, pipeline_behavior, ambient)."},{name:"is_read_only",sourceName:"IsReadOnly",type:"boolean",description:"Indicates whether the transaction is explicitly configured as read-only."},{name:"isolation_level",sourceName:"IsolationLevel",type:"string",description:"Configured transaction isolation level when specified."},{name:"framework",sourceName:"Framework",type:"string",description:"Detected transaction API or framework (e.g. efcore, spring-tx, typeorm, prisma, database-sql)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring the transaction boundary."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the transaction boundary."}],outEdges:[{rel:"WRAPS",toKind:"Function",category:"control_flow",description:"Links a Layer 4 Transaction boundary to the root function or handler it wraps."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"EXECUTES_IN_TX",fromKind:"Function",category:"control_flow",description:"Links a function to an active Layer 4 database Transaction boundary under which it executes."},{rel:"CONTAINS",fromKind:"Module",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Transaction)-[:WRAPS]->(t:Function)
RETURN n.name, n.sub_kind, n.is_read_only, t.name
LIMIT 25`},{kind:"Query",pluralLabel:"SQL Queries",layerId:4,layerKey:"L4_semantic",icon:"search",order:30,idScheme:"{workspaceId}:q:{queryHash}",purpose:"Represents a SQL query.",properties:[{name:"name",sourceName:"Name",type:"string",description:"Short query identifier or operation summary."},{name:"query_text",sourceName:"QueryText",type:"string",description:"Extracted SQL or query statement text."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the query in source."},{name:"verb",sourceName:"Verb",type:"string",description:"SQL DML/DDL verb (SELECT, INSERT, UPDATE, DELETE, MERGE, EXEC)."},{name:"operation",sourceName:"Operation",type:"string",description:"Normalized data access operation (read, write, schema)."},{name:"evidence_kind",sourceName:"EvidenceKind",type:"string",description:"Extraction provenance kind (query, orm, migration, procedure)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file path declaring the query."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed source line of the query call site."}],outEdges:[{rel:"TRANSITIVELY_CALLS",toKind:"Function",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",toKind:"Type",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"READS_FROM",toKind:"Table",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from."},{rel:"WRITES_TO",toKind:"Table",category:"data_flow",description:"Links a query or workload to the table or cloud resource it writes to."},{rel:"DEPENDS_ON",toKind:"Table",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"ACCESSED_BY",fromKind:"Table",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ATTRIBUTED_TO",fromKind:"Endpoint",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"CONTAINS",fromKind:"Procedure",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Query)-[:TRANSITIVELY_CALLS]->(t:Function)
RETURN n.name, n.query_text, n.start_line, t.name
LIMIT 25`},{kind:"Type",pluralLabel:"Types (Classes, Interfaces)",layerId:3,layerKey:"L3_syntactic",icon:"symbol-class",order:1,idScheme:"{workspaceId}:sym:{filePath}:Type:{name}:{line}",purpose:"Represents a type declaration (Class, Interface, Struct, Record, Enum, or Union type).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"symbol",sourceName:"Symbol",type:"string",description:"A globally unique ID for this symbol scope."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative path of the declaring file."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"The starting line number (1-indexed) of the declaration."},{name:"kind",sourceName:"TypeKind",type:"enum (TypeKind)",description:"The specific type kind (class, interface, struct, record, enum)."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the type is declared within test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."},{name:"loc",sourceName:"Loc",type:"integer",description:"Lines of code span for this type declaration."},{name:"is_exported",sourceName:"IsExported",type:"boolean",description:"Indicates whether the type is publicly exported from its module."},{name:"extends",sourceName:"Extends",type:"string",description:"Declared base type or component inherited by this type."},{name:"implements",sourceName:"Implements",type:"string",description:"Declared interfaces implemented by this type."},{name:"attributes",sourceName:"Attributes",type:"string",description:"Comma-separated annotations, attributes, or decorators applied to this type."}],outEdges:[{rel:"CALLS",toKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"IMPLEMENTS",toKind:"Type",category:"control_flow",description:"Links a class/struct declaration to the interface it implements."},{rel:"PASSES_LITERAL",toKind:"CallLiteral",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"INHERITS_FROM",toKind:"Type",category:"type_dependency",description:"Links a class/interface to its base class or inherited interface."},{rel:"POTENTIAL_TYPE",toKind:"Type",category:"type_dependency",description:"Links a variable or parameter to concrete classes that implement its declared interface type."},{rel:"USES_TYPE",toKind:"Type",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARES",toKind:"Type",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"HAS_MEMBER",toKind:"Member",category:"structural",description:"Links a type declaration to its declared member variables or fields."},{rel:"HAS_METHOD",toKind:"Function",category:"structural",description:"Links a type declaration to its declared methods or functions."}],inEdges:[{rel:"CALLS",fromKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"EXPOSED_BY",fromKind:"Endpoint",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"IMPLEMENTS",fromKind:"Type",category:"control_flow",description:"Links a class/struct declaration to the interface it implements."},{rel:"INSTANTIATES",fromKind:"Function",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"ACCESSED_BY",fromKind:"ExternalService",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Query",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Table",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"IMPLEMENTED_BY",fromKind:"Message",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"Policy",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"Table",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"INHERITS_FROM",fromKind:"Type",category:"type_dependency",description:"Links a class/interface to its base class or inherited interface."},{rel:"OF_TYPE",fromKind:"Member",category:"type_dependency",description:"Links a member variable or field to its declared type."},{rel:"POTENTIAL_TYPE",fromKind:"Type",category:"type_dependency",description:"Links a variable or parameter to concrete classes that implement its declared interface type."},{rel:"USES_TYPE",fromKind:"Function",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references."},{rel:"USES_TYPE",fromKind:"Type",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references."},{rel:"DECLARES",fromKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",fromKind:"Type",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DEFINES",fromKind:"BoundedContext",category:"structural",description:"Indicates that an entity defines a particular property or configuration."}],sampleCypher:`MATCH (n:Type)-[:CALLS]->(t:Function)
RETURN n.name, n.symbol, n.file_path, t.name
LIMIT 25`},{kind:"Function",pluralLabel:"Functions & Methods",layerId:3,layerKey:"L3_syntactic",icon:"symbol-method",order:2,idScheme:"{workspaceId}:sym:{filePath}:Function:{name}:{line}",purpose:"Represents a parsed method, function, subroutine, or procedure.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"symbol",sourceName:"Symbol",type:"string",description:"A globally unique ID for this symbol scope."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative path of the declaring file."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"The starting line number (1-indexed) of the declaration."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the function is declared within test code."},{name:"test_framework",sourceName:"TestFramework",type:"string",description:"Detected test runner or framework (e.g. xunit, nunit, mstest, jest, pytest, go-test, junit)."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."},{name:"loc",sourceName:"Loc",type:"integer",description:"Lines of code span for this function."},{name:"is_exported",sourceName:"IsExported",type:"boolean",description:"Indicates whether the function is publicly exported from its module."},{name:"is_module_init",sourceName:"IsModuleInit",type:"boolean",description:"Indicates whether this node represents top-level module initialization code."},{name:"is_async",sourceName:"IsAsync",type:"boolean",description:"Indicates whether the function is asynchronous."},{name:"is_unsafe",sourceName:"IsUnsafe",type:"boolean",description:"Indicates whether the function uses unsafe code blocks or modifiers."},{name:"visibility",sourceName:"Visibility",type:"string",description:"Declared visibility or access modifier of the function (e.g. public, private, remote)."},{name:"graphql_operation",sourceName:"GraphqlOperation",type:"string",description:"GraphQL client operation name or flag."},{name:"operation_type",sourceName:"OperationType",type:"string",description:"Operation type for GraphQL or RPC operations."},{name:"graphql_fields",sourceName:"GraphqlFields",type:"string",description:"Comma-separated GraphQL root selection fields."},{name:"attributes",sourceName:"Attributes",type:"string",description:"Comma-separated annotations, attributes, or decorators applied to this function."},{name:"cyclomatic_complexity",sourceName:"CyclomaticComplexity",type:"integer",description:"McCabe cyclomatic complexity of the function (1 + total decision points across nested scopes)."},{name:"cognitive_complexity",sourceName:"CognitiveComplexity",type:"integer",description:"Depth-weighted cognitive complexity score across nested control-flow scopes."},{name:"max_nesting_depth",sourceName:"MaxNestingDepth",type:"integer",description:"Maximum structural control-flow nesting depth inside the function."},{name:"max_loop_depth",sourceName:"MaxLoopDepth",type:"integer",description:"Maximum loop nesting depth inside the function (0 = no loops, 1 = O(N), 2 = O(N^2))."},{name:"loop_await_count",sourceName:"LoopAwaitCount",type:"integer",description:"Total number of await suspension points executed inside loop scopes of this function."}],outEdges:[{rel:"CALLS",toKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"CALLS",toKind:"Type",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"EXECUTES_IN_TX",toKind:"Transaction",category:"control_flow",description:"Links a function to an active Layer 4 database Transaction boundary under which it executes."},{rel:"HANDLES_COMMAND",toKind:"Message",category:"control_flow",description:"Links a command handler function to the Layer 4 CQRS Command message contract it executes."},{rel:"HANDLES_EVENT",toKind:"Message",category:"control_flow",description:"Links an event or notification handler function to the Layer 4 Domain or Integration Event message contract it processes."},{rel:"HANDLES_QUERY",toKind:"Message",category:"control_flow",description:"Links a query handler function to the Layer 4 CQRS Query message contract it executes."},{rel:"IMPLEMENTS",toKind:"EntryPoint",category:"control_flow",description:"Links a class/struct declaration to the interface it implements."},{rel:"INSTANTIATES",toKind:"EntryPoint",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"INSTANTIATES",toKind:"Function",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"INSTANTIATES",toKind:"Type",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"HAS_PARAMETER",toKind:"Parameter",category:"data_flow",description:"Links a function or method to one of its semantically colored parameters."},{rel:"INVALIDATES_CACHE",toKind:"Cache",category:"data_flow",description:"Links a function to a Layer 4 Cache region/key it evicts or deletes."},{rel:"PASSES_ARGUMENT",toKind:"CallArgument",category:"data_flow",description:"Links a calling function or scope to a semantically colored call argument."},{rel:"PASSES_LITERAL",toKind:"CallLiteral",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"READS_CACHE",toKind:"Cache",category:"data_flow",description:"Links a function to a Layer 4 Cache region/key it reads from."},{rel:"WRITES_CACHE",toKind:"Cache",category:"data_flow",description:"Links a function to a Layer 4 Cache region/key it populates or updates."},{rel:"USES_TYPE",toKind:"Type",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARES",toKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",toKind:"Member",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",toKind:"Scope",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",toKind:"Type",category:"structural",description:"Indicates that a container entity declares a sub-entity."}],inEdges:[{rel:"CALLED_BY",fromKind:"ExternalService",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"CALLED_BY",fromKind:"Service",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"CALLS",fromKind:"Endpoint",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"CALLS",fromKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"CALLS",fromKind:"Scope",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"CALLS",fromKind:"Type",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"ENFORCED_BY",fromKind:"Policy",category:"control_flow",description:"Links a Layer 4 authorization Policy to the guard or middleware function enforcing it."},{rel:"EXPOSED_BY",fromKind:"Endpoint",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"INSTANTIATES",fromKind:"Function",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"TRANSITIVELY_CALLS",fromKind:"ExternalService",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{rel:"TRANSITIVELY_CALLS",fromKind:"Query",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{rel:"TRIGGERS",fromKind:"Endpoint",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"TRIGGERS",fromKind:"Job",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"TRIGGERS",fromKind:"Message",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"WRAPS",fromKind:"Transaction",category:"control_flow",description:"Links a Layer 4 Transaction boundary to the root function or handler it wraps."},{rel:"ACCESSED_BY",fromKind:"Cache",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"ExternalService",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Member",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Procedure",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Query",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Table",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"PUBLISHED_BY",fromKind:"Topic",category:"data_flow",description:"Links a topic to the function that publishes to it."},{rel:"SUBSCRIBED_BY",fromKind:"Topic",category:"data_flow",description:"Links a topic to the function that subscribes to it."},{rel:"DECLARES",fromKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"HAS_METHOD",fromKind:"Type",category:"structural",description:"Links a type declaration to its declared methods or functions."}],sampleCypher:`MATCH (n:Function)-[:CALLS]->(t:Function)
RETURN n.name, n.symbol, n.file_path, t.name
LIMIT 25`},{kind:"Member",pluralLabel:"Members & Fields",layerId:3,layerKey:"L3_syntactic",icon:"symbol-field",order:3,idScheme:"{workspaceId}:sym:{filePath}:Member:{name}:{line}",purpose:"Represents a declared field, property, parameter, or local variable.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"symbol",sourceName:"Symbol",type:"string",description:"A globally unique ID for this symbol scope."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative path of the declaring file."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"The starting line number (1-indexed) of the declaration."},{name:"kind",sourceName:"MemberKind",type:"enum (MemberKind)",description:"The specific member kind (field, property, parameter, variable)."},{name:"data_type",sourceName:"DataType",type:"string",description:"The declared data type of the member."},{name:"parameter_index",sourceName:"ParameterIndex",type:"integer",description:"0-based formal parameter index when the member is a function parameter."},{name:"variable_type",sourceName:"VariableClassification",type:"string",description:"Variable classification tags (e.g. parameter, config, etl, constant, global, property, field)."},{name:"initializer_expression",sourceName:"InitializerExpression",type:"string",description:"Bounded initializer expression text."},{name:"is_constant",sourceName:"IsConstant",type:"boolean",description:"Indicates whether the member is a compile-time or immutable constant."},{name:"is_static",sourceName:"IsStatic",type:"boolean",description:"Indicates whether the member is declared static or module-global (shared across instances)."},{name:"attributes",sourceName:"Attributes",type:"string",description:"Comma-separated annotations, attributes, or decorators applied to this member."}],outEdges:[{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"OF_TYPE",toKind:"Type",category:"type_dependency",description:"Links a member variable or field to its declared type."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"CloudResource",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DECLARES",fromKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"HAS_MEMBER",fromKind:"Type",category:"structural",description:"Links a type declaration to its declared member variables or fields."}],sampleCypher:`MATCH (n:Member)-[:ACCESSED_BY]->(t:Function)
RETURN n.name, n.symbol, n.file_path, t.name
LIMIT 25`},{kind:"Parameter",pluralLabel:"Semantic Parameters",layerId:3,layerKey:"L3_syntactic",icon:"symbol-parameter",order:38,idScheme:"{workspaceId}:param:{functionSymbol}:{index}:{name}",purpose:"Represents a semantically colored function or method parameter that flows into an architectural sink.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The parameter identifier name."},{name:"function_symbol",sourceName:"FunctionSymbol",type:"string",description:"The symbol ID of the declaring function or method."},{name:"index",sourceName:"ParamIndex",type:"integer",description:"The 0-indexed position of the parameter in the function signature."},{name:"data_type",sourceName:"DataType",type:"string",description:"The declared parameter type annotation, if present."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative file path of the declaring function."},{name:"line",sourceName:"Line",type:"integer",description:"The 1-indexed declaration line number."},{name:"nature",sourceName:"Nature",type:"enum (ParameterNature)",description:"The semantic nature inferred from intra- or inter-procedural sink usage."},{name:"sink_callee",sourceName:"SinkCallee",type:"string",description:"The primary sink callee that determined the parameter's semantic nature."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the parameter belongs to test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."}],outEdges:[{rel:"FLOWS_TO",toKind:"CallArgument",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"FLOWS_TO",fromKind:"CallArgument",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{rel:"HAS_PARAMETER",fromKind:"Function",category:"data_flow",description:"Links a function or method to one of its semantically colored parameters."}],sampleCypher:`MATCH (n:Parameter)-[:FLOWS_TO]->(t:CallArgument)
RETURN n.name, n.function_symbol, n.index, t.name
LIMIT 25`},{kind:"CallArgument",pluralLabel:"Semantic Arguments",layerId:3,layerKey:"L3_syntactic",icon:"symbol-variable",order:39,idScheme:"{workspaceId}:arg:{filePath}:{line}:{col}:{slot}",purpose:"Represents a semantically colored invocation argument sliced backward to its provenance origin.",properties:[{name:"name",sourceName:"Name",type:"string",description:"Display label or slot representation of the argument."},{name:"callee",sourceName:"Callee",type:"string",description:"The method, function, or constructor being invoked."},{name:"arg_index",sourceName:"ArgIndex",type:"integer",description:"The 0-indexed positional slot of the argument."},{name:"property_key",sourceName:"PropertyKey",type:"string",description:"The object property key when passed via an options object (e.g. sql, text, url, topic)."},{name:"raw_expression",sourceName:"RawExpression",type:"string",description:"The syntactic expression passed at the call site."},{name:"resolved_value",sourceName:"ResolvedValue",type:"string",description:"The concrete value resolved via backward constant/template slicing, if determinable."},{name:"nature",sourceName:"Nature",type:"enum (ParameterNature)",description:"The semantic nature of the argument slot."},{name:"provenance",sourceName:"Provenance",type:"enum (ArgumentProvenance)",description:"Where the argument's value originates (InternalConstant, CallerParameter, ConfigOrEnv, MemberOrField, etc.)."},{name:"expression_role",sourceName:"Role",type:"enum (ExpressionRole)",description:"How the expression participates in constructing the argument value."},{name:"value_kind",sourceName:"ValueKind",type:"enum (ConstantValueKind)",description:"Fine-grained classification of the resolved constant or template value."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative file path where the call occurs."},{name:"line",sourceName:"Line",type:"integer",description:"The 1-indexed line number of the call argument."},{name:"origin_file",sourceName:"OriginFile",type:"string",description:"The relative file path where the value was originally defined, if distinct."},{name:"origin_line",sourceName:"OriginLine",type:"integer",description:"The 1-indexed line number where the value was originally defined."},{name:"origin_symbol",sourceName:"OriginSymbol",type:"string",description:"The variable, constant, field, parameter, or config key from which the value originated."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the call occurs within test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."}],outEdges:[{rel:"FLOWS_TO",toKind:"Parameter",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"FLOWS_TO",fromKind:"Parameter",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{rel:"PASSES_ARGUMENT",fromKind:"Function",category:"data_flow",description:"Links a calling function or scope to a semantically colored call argument."},{rel:"TARGETED_BY",fromKind:"Database",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"Endpoint",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"ExternalService",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"Procedure",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"Table",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"Topic",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."}],sampleCypher:`MATCH (n:CallArgument)-[:FLOWS_TO]->(t:Parameter)
RETURN n.name, n.callee, n.arg_index, t.name
LIMIT 25`},{kind:"CallLiteral",pluralLabel:"Call Literals",layerId:3,layerKey:"L3_syntactic",icon:"quote",order:40,idScheme:"{workspaceId}:lit:{filePath}:{line}:{col}",purpose:"Represents a raw literal argument passed directly into a method or constructor invocation.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name or representation of the literal argument."},{name:"value",sourceName:"Value",type:"string",description:"The raw string value of the literal."},{name:"callee",sourceName:"Callee",type:"string",description:"The method or callee being invoked."},{name:"arg_index",sourceName:"ArgIndex",type:"integer",description:"The index of the argument (0-indexed)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative file path where the call occurs."},{name:"line",sourceName:"Line",type:"integer",description:"The 1-indexed line number of the argument."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the call occurs within test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."},{name:"role",sourceName:"Role",type:"string",description:"The semantic role of the literal argument (e.g. Business, Routing, ParamBinding, Config, Logging, QueryBuilder, DI)."},{name:"is_decorator",sourceName:"IsDecorator",type:"boolean",description:"Indicates whether the literal argument occurs within a decorator or attribute."},{name:"nature",sourceName:"Nature",type:"enum (ParameterNature)",description:"The semantic nature of the parameter/argument (e.g. SqlQuery, UrlOrRoute, ConnectionString, FilePath)."}],outEdges:[{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"PASSES_LITERAL",fromKind:"Endpoint",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"PASSES_LITERAL",fromKind:"Function",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"PASSES_LITERAL",fromKind:"Type",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."}],sampleCypher:`MATCH (n:CallLiteral)-[:DECLARED_IN]->(t:File)
RETURN n.name, n.value, n.callee, t.name
LIMIT 25`},{kind:"Scope",pluralLabel:"Control & Resource Scopes",layerId:3,layerKey:"L3_syntactic",icon:"shield-alert",order:41,idScheme:"{workspaceId}:scope:{filePath}:{kind}:{line}:{col}",purpose:"Represents a lexical, control-flow, or resource-management scope block (try, catch, finally, loop, branch, resource/using/defer, lock, unsafe, task_spawn).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The identifier or descriptor of the scope."},{name:"kind",sourceName:"ScopeKind",type:"enum (ScopeKind)",description:"The strongly-typed kind of the scope (try, catch, finally, loop, branch, resource, lock, unsafe, task_spawn)."},{name:"target_type",sourceName:"TargetType",type:"string",description:"The declared exception type, resource type, or lock target associated with this scope, if specified."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative file path where the scope occurs."},{name:"line",sourceName:"Line",type:"integer",description:"The 1-indexed line number where the scope starts."},{name:"exception_type",sourceName:"ExceptionType",type:"string",description:"The declared exception type caught by a catch scope (alias of target_type)."},{name:"depth",sourceName:"Depth",type:"integer",description:"The 1-indexed structural nesting depth of the scope inside its enclosing function."},{name:"loop_depth",sourceName:"LoopDepth",type:"integer",description:"The nesting depth of enclosing loops at this scope (0 = outside loops, 1 = O(N), 2 = O(N^2))."},{name:"await_count",sourceName:"AwaitCount",type:"integer",description:"The number of await or asynchronous suspension points inside this scope."},{name:"branch_count",sourceName:"BranchCount",type:"integer",description:"The number of branching predicates or decision points contributed by this scope."},{name:"is_async",sourceName:"IsAsync",type:"boolean",description:"Indicates whether the scope uses asynchronous iteration or disposal (e.g. await using, for await)."},{name:"is_empty",sourceName:"IsEmpty",type:"boolean",description:"Indicates whether the scope body contains no executable statements."},{name:"has_comment",sourceName:"HasComment",type:"boolean",description:"Indicates whether an empty scope block contains an explanatory comment."},{name:"statement_count",sourceName:"StatementCount",type:"integer",description:"The number of executable statements inside the scope body."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the scope occurs within test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."}],outEdges:[{rel:"CALLS",toKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"CONTAINS",toKind:"Scope",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"CONTAINS",fromKind:"Scope",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARES",fromKind:"Endpoint",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",fromKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."}],sampleCypher:`MATCH (n:Scope)-[:CALLS]->(t:Function)
RETURN n.name, n.kind, n.target_type, t.name
LIMIT 25`},{kind:"Project",pluralLabel:"Projects",layerId:2,layerKey:"L2_boundaries",icon:"project",order:1,idScheme:"{workspaceId}:p:{relativeProjectDir}:",purpose:"Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The language/signature identifier (e.g. 'csharp', 'go', 'python', 'typescript')."},{name:"language",sourceName:"Language",type:"string",description:"The programming language (e.g. 'csharp', 'go', 'python', 'typescript')."},{name:"kind",sourceName:"KindValue",type:"string",description:"The physical entity classification kind (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test)."},{name:"role",sourceName:"Role",type:"string",description:"Architectural role of the project (e.g. Service, SharedLibrary, FrontendApp, Worker, CliTool, Test)."},{name:"archetype",sourceName:"Archetype",type:"enum (ProjectArchetype)",description:"Architectural archetype of the project (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon)."},{name:"primary_role",sourceName:"PrimaryRole",type:"string",description:"Primary architectural role of the project (e.g. WebService, Scheduler, WorkerService, SharedLibrary, FrontendFramework, CliFramework, TestFramework)."},{name:"framework",sourceName:"Framework",type:"string",description:"Detected primary framework of the project."},{name:"entity_type",sourceName:"EntityType",type:"string",description:"Normalized semantic entity type (app, service, worker, cli, test, library)."},{name:"external_apis",sourceName:"ExternalApis",type:"string",description:"Comma-separated external APIs or SDKs used by this project."},{name:"package_name",sourceName:"PackageName",type:"string",description:"Package name declared in the project manifest."},{name:"package_version",sourceName:"PackageVersion",type:"string",description:"Package version declared in the project manifest."},{name:"main_file",sourceName:"MainFile",type:"string",description:"Main entry file declared in the project manifest."},{name:"module_file",sourceName:"ModuleFile",type:"string",description:"ESM module entry file declared in the project manifest."},{name:"description",sourceName:"Description",type:"string",description:"Project description from manifest."},{name:"is_library",sourceName:"IsLibrary",type:"boolean",description:"Indicates whether the project is a shared library rather than an executable application."},{name:"entity_kind",sourceName:"EntityKind",type:"enum (ProjectEntityKind)",description:"The physical entity classification (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test)."},{name:"sub_kind",sourceName:"SubKind",type:"enum (ProjectEntitySubKind)",description:"Sub-classification for apps (e.g. None, Web, Mobile, Desktop, Cli)."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether this project is a test project."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The physical entity classification kind."}],outEdges:[{rel:"HAS_ENTRYPOINT",toKind:"EntryPoint",category:"control_flow",description:"Links an executable ProjectNode to its binary or process EntryPointNode."},{rel:"SERVICE_CALL",toKind:"Project",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPENDS_ON",toKind:"Project",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"PRODUCES",toKind:"Package",category:"type_dependency",description:"Indicates that a library project builds and produces a package."},{rel:"USES_GIT",toKind:"GitSettings",category:"type_dependency",description:"Links the workspace to its repository configuration."},{rel:"CONTAINS",toKind:"EntryPoint",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"File",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Project",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"LOCATED_IN",toKind:"Folder",category:"structural",description:"Links a project to the physical folder or files structure where it is located."}],inEdges:[{rel:"SERVICE_CALL",fromKind:"Project",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"DEPENDS_ON",fromKind:"Project",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"IMPLEMENTED_BY",fromKind:"Module",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"Package",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"PRODUCES",fromKind:"Package",category:"type_dependency",description:"Indicates that a library project builds and produces a package."},{rel:"BELONGS_TO",fromKind:"EntryPoint",category:"structural",description:"Links an entity to its parent project or container."},{rel:"CONTAINS",fromKind:"Project",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",fromKind:"Endpoint",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"ExternalService",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],sampleCypher:`MATCH (n:Project)-[:HAS_ENTRYPOINT]->(t:EntryPoint)
RETURN n.name, n.project_type, n.language, t.name
LIMIT 25`},{kind:"EntryPoint",pluralLabel:"Entry Points",layerId:2,layerKey:"L2_boundaries",icon:"zap",order:2,idScheme:"{workspaceId}:entry:{entryType}:{name}",purpose:"Represents the binary, process, or bundle bootstrap entry point of an executable project (e.g. Program.Main, func main, __main__, index.tsx root mount).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the binary or process entry point."},{name:"path",sourceName:"Path",type:"string",description:"The path of the entry point file relative to its parent container."},{name:"entry_type",sourceName:"EntryType",type:"enum (EntryPointType)",description:"The kind of binary/process entry point (e.g. main-method, top-level, script-main, bundle-entry)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Relative path of the source file declaring the entry point."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-based starting line number of the entry point."},{name:"command",sourceName:"Command",type:"string",description:"Shell command executed by a lifecycle or task entry point."},{name:"hook_kind",sourceName:"HookKind",type:"string",description:"Lifecycle hook classification (e.g. install, build)."},{name:"task_label",sourceName:"TaskLabel",type:"string",description:"Task label for VS Code folderOpen tasks."}],outEdges:[{rel:"TRIGGERS",toKind:"File",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"BELONGS_TO",toKind:"Project",category:"structural",description:"Links an entity to its parent project or container."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"HAS_ENTRYPOINT",fromKind:"Project",category:"control_flow",description:"Links an executable ProjectNode to its binary or process EntryPointNode."},{rel:"IMPLEMENTS",fromKind:"Function",category:"control_flow",description:"Links a class/struct declaration to the interface it implements."},{rel:"INSTANTIATES",fromKind:"Function",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"CONTAINS",fromKind:"Project",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:EntryPoint)-[:TRIGGERS]->(t:File)
RETURN n.name, n.entry_type, n.file_path, t.name
LIMIT 25`},{kind:"Package",pluralLabel:"Packages & Dependencies",layerId:2,layerKey:"L2_boundaries",icon:"package",order:2,idScheme:"{workspaceId}:pkg:{ecosystem}:{packageName}",purpose:"Represents an external dependency package or workspace package referenced or produced by projects.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"version",sourceName:"Version",type:"string",description:"The package version."},{name:"is_internal",sourceName:"IsInternal",type:"boolean",description:"Whether this package is produced internally by a workspace project."},{name:"ecosystem",sourceName:"Ecosystem",type:"string",description:"The package ecosystem (e.g. nuget, npm, maven, pip, go)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"archetype",sourceName:"Archetype",type:"enum (ProjectArchetype)",description:"The architectural archetype of the package (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon)."},{name:"dependency_scope",sourceName:"DependencyScope",type:"string",description:"Dependency scope (e.g. runtime, dev, test, build)."},{name:"is_external",sourceName:"IsExternal",type:"boolean",description:"Whether this package is an external third-party dependency."}],outEdges:[{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"PRODUCES",toKind:"Project",category:"type_dependency",description:"Indicates that a library project builds and produces a package."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"Module",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPENDS_ON",fromKind:"Project",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"PRODUCES",fromKind:"Project",category:"type_dependency",description:"Indicates that a library project builds and produces a package."}],sampleCypher:`MATCH (n:Package)-[:IMPLEMENTED_BY]->(t:Project)
RETURN n.name, n.version, n.is_internal, t.name
LIMIT 25`},{kind:"File",pluralLabel:"Files",layerId:1,layerKey:"L1_physical",icon:"file-code",order:1,idScheme:"{workspaceId}:f:{relativeFilePath}",purpose:"Represents a source code file containing parsable content.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the file is part of a test suite or test project."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."},{name:"line_count",sourceName:"LineCount",type:"integer",description:"Total lines of code in this file."},{name:"size_bytes",sourceName:"SizeBytes",type:"integer",description:"Size of the file in bytes."},{name:"artifact_role",sourceName:"ArtifactRole",type:"string",description:"The physical artifact role of the file (source, dist, minified, declaration, script)."},{name:"directive",sourceName:"Directive",type:"string",description:"Leading module directive pragma when present (e.g., 'use client', 'use server')."}],outEdges:[],inEdges:[{rel:"TRIGGERS",fromKind:"EntryPoint",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"CONTAINS",fromKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",fromKind:"Project",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",fromKind:"Cache",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"CallArgument",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"CallLiteral",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Cloud",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"CloudResource",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Database",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"DbSchema",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Endpoint",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"EntryPoint",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"ExternalService",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Function",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Job",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Member",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Message",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Parameter",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Policy",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Query",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Scope",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Topic",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Transaction",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Type",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],sampleCypher:`MATCH (s:EntryPoint)-[:TRIGGERS]->(n:File)
RETURN s.name, n.name, n.is_test, n.project_kind
LIMIT 25`},{kind:"Folder",pluralLabel:"Folders",layerId:1,layerKey:"L1_physical",icon:"folder",order:2,idScheme:"{workspaceId}:dir:{relativeDirectoryPath}",purpose:"Represents a directory within the indexed workspace.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the folder."},{name:"path",sourceName:"Path",type:"string",description:"The path to the folder."}],outEdges:[{rel:"CONTAINS",toKind:"File",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"GitSettings",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],inEdges:[{rel:"CONTAINS",fromKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"LOCATED_IN",fromKind:"Project",category:"structural",description:"Links a project to the physical folder or files structure where it is located."}],sampleCypher:`MATCH (n:Folder)-[:CONTAINS]->(t:File)
RETURN n.name, n.path, t.name
LIMIT 25`},{kind:"GitSettings",pluralLabel:"Git Settings",layerId:1,layerKey:"L1_physical",icon:"git-commit",order:3,idScheme:"{workspaceId}:git",purpose:"Represents the Git repository configuration settings for the workspace.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"branch",sourceName:"Branch",type:"string",description:"The currently checked-out branch name."},{name:"origin_url",sourceName:"OriginUrl",type:"string",description:"The remote origin repository URL."},{name:"user_name",sourceName:"UserName",type:"string",description:"The git user name."},{name:"user_email",sourceName:"UserEmail",type:"string",description:"The git user email address."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"commit_hash",sourceName:"CommitHash",type:"string",description:"Current Git HEAD commit hash."},{name:"head_commit",sourceName:"HeadCommit",type:"string",description:"Current Git HEAD commit hash alias."},{name:"repo_name",sourceName:"RepoName",type:"string",description:"Git repository name."}],outEdges:[],inEdges:[{rel:"USES_GIT",fromKind:"Project",category:"type_dependency",description:"Links the workspace to its repository configuration."},{rel:"CONTAINS",fromKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (s:Project)-[:USES_GIT]->(n:GitSettings)
RETURN s.name, n.name, n.branch, n.origin_url
LIMIT 25`}],relationships:[{rel:"CALLED_BY",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"ExternalService",toKind:"Function"},{fromKind:"Service",toKind:"Function"}]},{rel:"CALLS",category:"control_flow",description:"Links a calling function to the function it directly invokes.",properties:[{name:"line",type:"integer",description:"1-indexed source line of the primary invocation site."},{name:"call_count",type:"integer",description:"Aggregated number of call sites between caller and callee."},{name:"in_loop",type:"boolean",description:"True when invoked inside a for/foreach/while/do loop (N+1 risk signal)."},{name:"loop_depth",type:"integer",description:"Nesting depth of enclosing loops at the call site."},{name:"is_conditional",type:"boolean",description:"True when guarded by an if/switch/ternary branch."},{name:"in_try",type:"boolean",description:"True when enclosed inside a try/catch block."},{name:"arg_flow",type:"string",description:"Parameter-to-argument dataflow slot mapping across the call boundary."}],pairs:[{fromKind:"Endpoint",toKind:"Function"},{fromKind:"Function",toKind:"Function"},{fromKind:"Function",toKind:"Type"},{fromKind:"Procedure",toKind:"Procedure"},{fromKind:"Scope",toKind:"Function"},{fromKind:"Type",toKind:"Function"}]},{rel:"CALLS_ENDPOINT",category:"control_flow",description:"Links an external API call to the target HTTP endpoint it invokes.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"ExternalService",toKind:"Endpoint"}]},{rel:"ENFORCED_BY",category:"control_flow",description:"Links a Layer 4 authorization Policy to the guard or middleware function enforcing it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Policy",toKind:"Function"}]},{rel:"EXECUTES_IN_TX",category:"control_flow",description:"Links a function to an active Layer 4 database Transaction boundary under which it executes.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Transaction"}]},{rel:"EXPOSED_BY",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"Function"},{fromKind:"Endpoint",toKind:"Type"}]},{rel:"EXPOSES",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Service",toKind:"Endpoint"},{fromKind:"Workload",toKind:"Job"}]},{rel:"GUARDED_BY",category:"control_flow",description:"Links an Endpoint or CQRS Message contract to a Layer 4 authorization Policy or Guard.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"Policy"},{fromKind:"Message",toKind:"Policy"}]},{rel:"HANDLES_COMMAND",category:"control_flow",description:"Links a command handler function to the Layer 4 CQRS Command message contract it executes.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Message"}]},{rel:"HANDLES_EVENT",category:"control_flow",description:"Links an event or notification handler function to the Layer 4 Domain or Integration Event message contract it processes.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Message"}]},{rel:"HANDLES_QUERY",category:"control_flow",description:"Links a query handler function to the Layer 4 CQRS Query message contract it executes.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Message"}]},{rel:"HAS_ENTRYPOINT",category:"control_flow",description:"Links an executable ProjectNode to its binary or process EntryPointNode.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Project",toKind:"EntryPoint"}]},{rel:"IMPLEMENTS",category:"control_flow",description:"Links a class/struct declaration to the interface it implements.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"EntryPoint"},{fromKind:"Type",toKind:"Type"}]},{rel:"INSTANTIATES",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"EntryPoint"},{fromKind:"Function",toKind:"Function"},{fromKind:"Function",toKind:"Type"}]},{rel:"SERVICE_CALL",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Project",toKind:"Project"},{fromKind:"Workload",toKind:"ExternalService"},{fromKind:"Workload",toKind:"Service"},{fromKind:"Workload",toKind:"Workload"}]},{rel:"TRANSITIVELY_CALLS",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph.",properties:[{name:"hops",type:"integer",description:"Shortest call-chain hop distance from the calling function to the sink."}],pairs:[{fromKind:"ExternalService",toKind:"Function"},{fromKind:"Query",toKind:"Function"}]},{rel:"TRIGGERS",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"Function"},{fromKind:"EntryPoint",toKind:"File"},{fromKind:"Job",toKind:"Function"},{fromKind:"Message",toKind:"Function"},{fromKind:"Table",toKind:"Procedure"}]},{rel:"WRAPS",category:"control_flow",description:"Links a Layer 4 Transaction boundary to the root function or handler it wraps.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Transaction",toKind:"Function"}]},{rel:"ACCESSED_BY",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Cache",toKind:"Function"},{fromKind:"ExternalService",toKind:"Function"},{fromKind:"ExternalService",toKind:"Type"},{fromKind:"Member",toKind:"Function"},{fromKind:"Procedure",toKind:"Function"},{fromKind:"Query",toKind:"Function"},{fromKind:"Query",toKind:"Type"},{fromKind:"Table",toKind:"Function"},{fromKind:"Table",toKind:"Query"},{fromKind:"Table",toKind:"Type"}]},{rel:"ATTRIBUTED_TO",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain.",properties:[{name:"hops",type:"integer",description:"Call-chain hop distance from the entrypoint or endpoint to the sink."},{name:"sink_kind",type:"string",description:"Target sink classification (Database, Query, ExternalService, Topic)."}],pairs:[{fromKind:"Endpoint",toKind:"Database"},{fromKind:"Endpoint",toKind:"ExternalService"},{fromKind:"Endpoint",toKind:"Query"}]},{rel:"FLOWS_TO",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the value transfer occurs."},{name:"arg_index",type:"integer",description:"0-indexed target parameter or argument slot."}],pairs:[{fromKind:"CallArgument",toKind:"Parameter"},{fromKind:"Parameter",toKind:"CallArgument"}]},{rel:"HAS_PARAMETER",category:"data_flow",description:"Links a function or method to one of its semantically colored parameters.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Parameter"}]},{rel:"INVALIDATES_CACHE",category:"data_flow",description:"Links a function to a Layer 4 Cache region/key it evicts or deletes.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Cache"}]},{rel:"PASSES_ARGUMENT",category:"data_flow",description:"Links a calling function or scope to a semantically colored call argument.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the value transfer occurs."},{name:"arg_index",type:"integer",description:"0-indexed target parameter or argument slot."}],pairs:[{fromKind:"Function",toKind:"CallArgument"}]},{rel:"PASSES_LITERAL",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the literal argument is passed."}],pairs:[{fromKind:"Endpoint",toKind:"CallLiteral"},{fromKind:"Function",toKind:"CallLiteral"},{fromKind:"Type",toKind:"CallLiteral"}]},{rel:"PUBLISHED_BY",category:"data_flow",description:"Links a topic to the function that publishes to it.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Topic",toKind:"Function"}]},{rel:"PUBLISHES_TO",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Workload",toKind:"Topic"}]},{rel:"READS_CACHE",category:"data_flow",description:"Links a function to a Layer 4 Cache region/key it reads from.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Cache"}]},{rel:"READS_FROM",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Procedure",toKind:"Table"},{fromKind:"Query",toKind:"Table"},{fromKind:"Table",toKind:"Table"}]},{rel:"SUBSCRIBED_BY",category:"data_flow",description:"Links a topic to the function that subscribes to it.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Topic",toKind:"Function"}]},{rel:"SUBSCRIBES_TO",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Endpoint",toKind:"Topic"},{fromKind:"Worker",toKind:"Topic"}]},{rel:"TARGETED_BY",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the value transfer occurs."},{name:"arg_index",type:"integer",description:"0-indexed target parameter or argument slot."}],pairs:[{fromKind:"Database",toKind:"CallArgument"},{fromKind:"Endpoint",toKind:"CallArgument"},{fromKind:"ExternalService",toKind:"CallArgument"},{fromKind:"Procedure",toKind:"CallArgument"},{fromKind:"Table",toKind:"CallArgument"},{fromKind:"Topic",toKind:"CallArgument"}]},{rel:"USES_DB",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Workload",toKind:"Database"}]},{rel:"WRITES_CACHE",category:"data_flow",description:"Links a function to a Layer 4 Cache region/key it populates or updates.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Cache"}]},{rel:"WRITES_TO",category:"data_flow",description:"Links a query or workload to the table or cloud resource it writes to.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Procedure",toKind:"Table"},{fromKind:"Query",toKind:"Table"}]},{rel:"DEPENDS_ON",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"BoundedContext"},{fromKind:"CloudResource",toKind:"CloudResource"},{fromKind:"CloudResource",toKind:"Member"},{fromKind:"Domain",toKind:"Domain"},{fromKind:"Module",toKind:"Library"},{fromKind:"Module",toKind:"Package"},{fromKind:"Project",toKind:"Package"},{fromKind:"Project",toKind:"Project"},{fromKind:"Query",toKind:"Table"}]},{rel:"DEPLOYS",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"CloudResource",toKind:"Database"},{fromKind:"CloudResource",toKind:"ExternalService"},{fromKind:"CloudResource",toKind:"Topic"},{fromKind:"CloudResource",toKind:"Workload"}]},{rel:"HOSTED_IN",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source or config line where the cloud hosting binding is declared."}],pairs:[{fromKind:"CloudResource",toKind:"Cloud"},{fromKind:"Database",toKind:"Cloud"},{fromKind:"ExternalService",toKind:"Cloud"},{fromKind:"Topic",toKind:"Cloud"},{fromKind:"Workload",toKind:"Cloud"}]},{rel:"IMPLEMENTED_BY",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Database",toKind:"CloudResource"},{fromKind:"ExternalService",toKind:"CloudResource"},{fromKind:"Message",toKind:"Type"},{fromKind:"Module",toKind:"Project"},{fromKind:"Package",toKind:"Project"},{fromKind:"Policy",toKind:"Type"},{fromKind:"Table",toKind:"Type"},{fromKind:"Topic",toKind:"CloudResource"},{fromKind:"Workload",toKind:"CloudResource"}]},{rel:"INHERITS_FROM",category:"type_dependency",description:"Links a class/interface to its base class or inherited interface.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Type"}]},{rel:"OF_TYPE",category:"type_dependency",description:"Links a member variable or field to its declared type.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Member",toKind:"Type"}]},{rel:"POTENTIAL_TYPE",category:"type_dependency",description:"Links a variable or parameter to concrete classes that implement its declared interface type.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Type"}]},{rel:"PRODUCES",category:"type_dependency",description:"Indicates that a library project builds and produces a package.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Package",toKind:"Project"},{fromKind:"Project",toKind:"Package"}]},{rel:"REFERENCES",category:"type_dependency",description:"Links a child database table to a parent table via a foreign key constraint.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Table",toKind:"Table"}]},{rel:"TESTS",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"TestSuite",toKind:"Module"}]},{rel:"USES_GIT",category:"type_dependency",description:"Links the workspace to its repository configuration.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Project",toKind:"GitSettings"}]},{rel:"USES_TYPE",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Type"},{fromKind:"Type",toKind:"Type"}]},{rel:"BELONGS_TO",category:"structural",description:"Links an entity to its parent project or container.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"EntryPoint",toKind:"Project"}]},{rel:"CONTAINS",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"Database"},{fromKind:"BoundedContext",toKind:"Module"},{fromKind:"BoundedContext",toKind:"Topic"},{fromKind:"Database",toKind:"DbSchema"},{fromKind:"DbSchema",toKind:"Procedure"},{fromKind:"DbSchema",toKind:"Table"},{fromKind:"Domain",toKind:"BoundedContext"},{fromKind:"Folder",toKind:"File"},{fromKind:"Folder",toKind:"Folder"},{fromKind:"Folder",toKind:"GitSettings"},{fromKind:"Module",toKind:"Cache"},{fromKind:"Module",toKind:"Job"},{fromKind:"Module",toKind:"Message"},{fromKind:"Module",toKind:"Policy"},{fromKind:"Module",toKind:"Transaction"},{fromKind:"Procedure",toKind:"Query"},{fromKind:"Project",toKind:"EntryPoint"},{fromKind:"Project",toKind:"File"},{fromKind:"Project",toKind:"Project"},{fromKind:"Scope",toKind:"Scope"}]},{rel:"DECLARED_IN",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Cache",toKind:"File"},{fromKind:"CallArgument",toKind:"File"},{fromKind:"CallLiteral",toKind:"File"},{fromKind:"Cloud",toKind:"File"},{fromKind:"CloudResource",toKind:"File"},{fromKind:"Database",toKind:"File"},{fromKind:"DbSchema",toKind:"File"},{fromKind:"Endpoint",toKind:"File"},{fromKind:"Endpoint",toKind:"Project"},{fromKind:"EntryPoint",toKind:"File"},{fromKind:"ExternalService",toKind:"File"},{fromKind:"ExternalService",toKind:"Project"},{fromKind:"Function",toKind:"File"},{fromKind:"Job",toKind:"File"},{fromKind:"Member",toKind:"File"},{fromKind:"Message",toKind:"File"},{fromKind:"Parameter",toKind:"File"},{fromKind:"Policy",toKind:"File"},{fromKind:"Query",toKind:"File"},{fromKind:"Scope",toKind:"File"},{fromKind:"Topic",toKind:"File"},{fromKind:"Transaction",toKind:"File"},{fromKind:"Type",toKind:"File"}]},{rel:"DECLARES",category:"structural",description:"Indicates that a container entity declares a sub-entity.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"Scope"},{fromKind:"Function",toKind:"Function"},{fromKind:"Function",toKind:"Member"},{fromKind:"Function",toKind:"Scope"},{fromKind:"Function",toKind:"Type"},{fromKind:"Type",toKind:"Type"}]},{rel:"DEFINES",category:"structural",description:"Indicates that an entity defines a particular property or configuration.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"Type"}]},{rel:"HAS_MEMBER",category:"structural",description:"Links a type declaration to its declared member variables or fields.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Member"}]},{rel:"HAS_METHOD",category:"structural",description:"Links a type declaration to its declared methods or functions.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Function"}]},{rel:"LOCATED_IN",category:"structural",description:"Links a project to the physical folder or files structure where it is located.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Project",toKind:"Folder"}]},{rel:"OWNS",category:"structural",description:"Links a Module or BoundedContext to a database Table whose schema or write lifecycle it owns.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"Table"},{fromKind:"Module",toKind:"Table"}]}]},w="graph",h="Endpoint",Q=null,j=!1,x=null,X=null,de="",te={control_flow:!0,data_flow:!0,type_dependency:!0,structural:!0},ve={5:!0,4:!0,3:!0,2:!0,1:!0},ke={},he=0;he<d.nodes.length;he++)d.nodes[he].subtypes&&d.nodes[he].subtypes.length>0&&(ke[d.nodes[he].kind]=!0);var Ce="";function W(o){return getComputedStyle(document.documentElement).getPropertyValue(o).trim()}for(var k={canvasBg:W("--ont-canvas-bg"),gridDot:W("--ont-grid-dot"),logical:W("--ont-c-logical"),module:W("--ont-c-module"),workload:W("--ont-c-workload"),contract:W("--ont-c-contract"),data:W("--ont-c-data"),cloud:W("--ont-c-cloud"),syntax:W("--ont-c-syntax"),slicing:W("--ont-c-slicing"),project:W("--ont-c-project"),physical:W("--ont-c-physical"),capOuterFill:W("--ont-capsule-outer-fill"),capOuterBorder:W("--ont-capsule-outer-border"),capInnerFill:W("--ont-capsule-inner-fill"),capInnerBorder:W("--ont-capsule-inner-border"),capHoverBorder:W("--ont-capsule-hover-border"),capHeadBg:W("--ont-capsule-head-bg"),capHeadHoverBg:W("--ont-capsule-head-hover-bg"),abstractRing:W("--ont-abstract-ring"),badgeBg:W("--ont-badge-bg"),badgeBorder:W("--ont-badge-border"),pillBg:W("--ont-pill-bg"),pillBorder:W("--ont-pill-border"),pillBorderActive:W("--ont-pill-border-active"),edgeIdle:W("--ont-edge-idle"),edgeDim:W("--ont-edge-dim"),edgeControl:W("--ont-edge-control"),edgeData:W("--ont-edge-data"),edgeType:W("--ont-edge-type"),edgeStruct:W("--ont-edge-struct"),nodeRing:W("--ont-node-ring"),nodeHalo:W("--ont-node-halo"),nodeHover:W("--ont-node-hover"),inkBright:W("--ont-ink-bright"),inkSoft:W("--ont-ink-soft"),inkDim:W("--ont-ink-dim"),inkZero:W("--ont-ink-zero")},be={Domain:{fill:k.logical,glyph:"globe",cluster:8},BoundedContext:{fill:k.logical,glyph:"layers",cluster:8},Module:{fill:k.module,glyph:"package",cluster:0},Workload:{fill:k.workload,glyph:"layers",cluster:0},App:{fill:k.workload,glyph:"browser",cluster:0},Service:{fill:k.workload,glyph:"server",cluster:0},Worker:{fill:k.workload,glyph:"gear",cluster:0},CliTool:{fill:k.workload,glyph:"terminal",cluster:0},Library:{fill:k.module,glyph:"library",cluster:0},TestSuite:{fill:k.module,glyph:"beaker",cluster:0},Endpoint:{fill:k.contract,glyph:"endpoint",cluster:1},Topic:{fill:k.contract,glyph:"topic",cluster:1},Database:{fill:k.data,glyph:"database",cluster:2},DbSchema:{fill:k.data,glyph:"schema",cluster:2},Table:{fill:k.data,glyph:"table",cluster:2},Query:{fill:k.data,glyph:"query",cluster:2},Procedure:{fill:k.data,glyph:"procedure",cluster:2},ExternalService:{fill:k.cloud,glyph:"cloud",cluster:3},Cloud:{fill:k.cloud,glyph:"cloudUp",cluster:3},CloudResource:{fill:k.cloud,glyph:"server",cluster:3},Type:{fill:k.syntax,glyph:"type",cluster:4},Function:{fill:k.syntax,glyph:"function",cluster:4},Member:{fill:k.syntax,glyph:"member",cluster:4},Scope:{fill:k.syntax,glyph:"catch",cluster:4},Parameter:{fill:k.slicing,glyph:"param",cluster:5},CallArgument:{fill:k.slicing,glyph:"arg",cluster:5},CallLiteral:{fill:k.slicing,glyph:"literal",cluster:5},Project:{fill:k.project,glyph:"project",cluster:6},EntryPoint:{fill:k.project,glyph:"zap",cluster:6},Package:{fill:k.project,glyph:"package",cluster:6},File:{fill:k.physical,glyph:"file",cluster:7},Folder:{fill:k.physical,glyph:"folder",cluster:7},GitSettings:{fill:k.physical,glyph:"git",cluster:7}},Ae=[{x:255,y:235,rx:145,ry:105},{x:635,y:255,rx:145,ry:85},{x:1125,y:285,rx:135,ry:110},{x:925,y:155,rx:105,ry:60},{x:705,y:495,rx:165,ry:115},{x:1065,y:575,rx:125,ry:95},{x:305,y:605,rx:130,ry:90},{x:645,y:695,rx:155,ry:55},{x:595,y:115,rx:85,ry:24}],Oe={App:{x:115,y:145},CliTool:{x:115,y:265},Workload:{x:265,y:215},Service:{x:405,y:150},Worker:{x:405,y:270},Module:{x:295,y:385},Library:{x:135,y:385},TestSuite:{x:155,y:495}},kt={App:1,Worker:1,CliTool:1,Library:1,TestSuite:1},at={control_flow:k.edgeControl,data_flow:k.edgeData,type_dependency:k.edgeType,structural:k.edgeStruct},O={},V=0;V<d.nodes.length;V++)O[d.nodes[V].kind]=d.nodes[V];for(var pe={},me=0;me<d.relationships.length;me++)pe[d.relationships[me].rel]=d.relationships[me];function Re(o,g){var S=O[o];if(!S||!S.subtypes||S.subtypes.length===0)return[];for(var T=[],C=0;C<S.subtypes.length;C++){var p=S.subtypes[C];if(!g[p]){g[p]=!0,T.push(p);for(var N=Re(p,g),R=0;R<N.length;R++)T.push(N[R])}}return T}for(var Ge=[],Pe=0;Pe<d.nodes.length;Pe++){var We=d.nodes[Pe];We.subtypes&&We.subtypes.length>0&&Ge.push({kind:We.kind,depth:We.ancestors?We.ancestors.length:0,allDescendants:Re(We.kind,{})})}Ge.sort(function(o,g){return o.depth-g.depth||(o.kind<g.kind?-1:1)});function ct(o){for(var g={},S=0;S<o.length;S++)g[o[S].kind]=!0;for(var T={},C={},p={},N=0;N<Ge.length;N++){var R=Ge[N];if(!ke[R.kind]&&!(!g[R.kind]||T[R.kind])){for(var G=0,D=0;D<R.allDescendants.length;D++){var z=R.allDescendants[D];g[z]&&!T[z]&&(T[z]=!0,G++),C[z]||(C[z]=R.kind)}G>0&&(p[R.kind]=G)}}for(var F=[],K=0;K<o.length;K++)T[o[K].kind]||F.push(o[K]);return{visibleDtos:F,redirectMap:C,collapsedCounts:p}}function Z(o){return String(o||"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function L(o,g){if(!g||o.kind.toLowerCase().indexOf(g)!==-1||o.pluralLabel.toLowerCase().indexOf(g)!==-1||o.purpose.toLowerCase().indexOf(g)!==-1||o.idScheme.toLowerCase().indexOf(g)!==-1||o.extends&&o.extends.toLowerCase().indexOf(g)!==-1)return!0;if(o.subtypes){for(var S=0;S<o.subtypes.length;S++)if(o.subtypes[S].toLowerCase().indexOf(g)!==-1)return!0}for(var T=0;T<o.properties.length;T++){var C=o.properties[T];if(C.name.toLowerCase().indexOf(g)!==-1||C.description.toLowerCase().indexOf(g)!==-1)return!0}for(var p=o.inheritedOutEdges?o.outEdges.concat(o.inheritedOutEdges):o.outEdges,N=0;N<p.length;N++){var R=p[N];if(R.rel.toLowerCase().indexOf(g)!==-1||R.toKind.toLowerCase().indexOf(g)!==-1)return!0}for(var G=o.inheritedInEdges?o.inEdges.concat(o.inheritedInEdges):o.inEdges,D=0;D<G.length;D++){var z=G[D];if(z.rel.toLowerCase().indexOf(g)!==-1||z.fromKind.toLowerCase().indexOf(g)!==-1)return!0}return!1}var we=690,Be=440,Fe=.0228,B=.001,se=.6,M=[],y={},E=[],ne=1,ge=1,ae=0,Ee=0,Se=!1,Le=null,Ve="";try{var Pt=localStorage.getItem("ce_ontology_air");if(Pt!==null){var Gn=parseFloat(Pt);!isNaN(Gn)&&Gn>=.1&&Gn<=2.4&&(ne=Gn)}var Rr=localStorage.getItem("ce_ontology_hub");if(Rr!==null){var Hn=parseFloat(Rr);!isNaN(Hn)&&Hn>=0&&Hn<=5&&(ge=Hn)}}catch{}function xr(o){for(var g={},S=0;S<o.length;S++){var T=o[S],C=be[T.kind]||{fill:k.physical,glyph:"file",cluster:7};g[C.cluster]||(g[C.cluster]=[]),g[C.cluster].push(T)}var p={};for(var N in g)for(var R=g[N],G=Ae[N]||Ae[0],D=0;D<R.length;D++){var z=R[D],F=be[z.kind]||{fill:k.physical,glyph:"file",cluster:7},K=Number(N)===0&&R.length>1?Oe[z.kind]:null,re=R.length===1?0:D/R.length*Math.PI*2-Math.PI/2,xe=K?K.x:G.x+(R.length===1?0:Math.cos(re)*G.rx),ue=K?K.y:G.y+(R.length===1?0:Math.sin(re)*G.ry);p[z.kind]={ax:xe,ay:ue,style:F,anchorStrength:K?.072:.048}}return p}function fs(o,g){var S=[],T={},C={};function p(Ue,qe,$e){if(Ue!==qe){var tt=o[Ue],ot=o[qe];if(!(!tt||!ot)){var ye=Ue<qe?Ue:qe,ze=Ue<qe?qe:Ue,bt=ye+"::"+ze;T[bt]||(T[bt]=!0,S.push({a:tt,b:ot,isExt:$e}),C[Ue]=(C[Ue]||0)+1,C[qe]=(C[qe]||0)+1)}}}for(var N=0;N<d.nodes.length;N++){var R=d.nodes[N];R.extends&&p(R.kind,R.extends,!0)}for(var G=0;G<d.relationships.length;G++){var D=d.relationships[G];if(te[D.category])for(var z=0;z<D.pairs.length;z++){var F=D.pairs[z],K=g&&g[F.fromKind]||F.fromKind,re=g&&g[F.toKind]||F.toKind;p(K,re,!1)}}for(var xe in o)o[xe].degree=Math.max(1,C[xe]||1);for(var ue=[],Me=0;Me<S.length;Me++){var Te=S[Me],Ne=Te.a.degree||1,Ke=Te.b.degree||1,oe=Math.max(.22,Math.min(.78,Ne/(Ne+Ke))),U=Math.max(1,Math.min(Ne,Ke)),ee=Te.isExt?.54:.48/Math.sqrt(U),He=Math.max(.16,Math.min(.56,ee)),Ze=Te.a.style.cluster===Te.b.style.cluster,et=Te.isExt?126:Ze?144:212;ue.push({source:Te.a,target:Te.b,distance:et,strength:He,bias:oe})}return ue}function Ja(o,g,S,T,C){for(var p=typeof C=="number"?C:1,N=Math.pow(540*T*(1+p*.25),2),R=-620*T*S,G=0;G<o.length;G++)for(var D=o[G],z=D.degree||1,F=1+p*.24*Math.max(0,z-2),K=G+1;K<o.length;K++){var re=o[K],xe=re.degree||1,ue=re.x-D.x,Me=re.y-D.y,Te=ue*ue+Me*Me;if(!(Te>N)){Te<1&&(ue=G%2===0?1:-1,Me=K%2===0?.5:-.5,Te=1.25);var Ne=1+p*.24*Math.max(0,xe-2),Ke=.5*(F+Ne)*(1+p*.08*Math.min(z,xe)),oe=R*Ke/Math.max(Te,900);D.fx===null&&(D.vx+=ue*oe,D.vy+=Me*oe),re.fx===null&&(re.vx-=ue*oe,re.vy-=Me*oe)}}for(var U=0;U<g.length;U++){var ee=g[U],He=ee.target.x+ee.target.vx-ee.source.x-ee.source.vx,Ze=ee.target.y+ee.target.vy-ee.source.y-ee.source.vy,et=Math.sqrt(He*He+Ze*Ze)||1,Ue=Math.max(0,(ee.source.degree||1)+(ee.target.degree||1)-4),qe=1+p*.042*Ue,$e=1/(1+p*.055*Ue),tt=ee.distance*T*qe,ot=(et-tt)/et*S*ee.strength*$e;He*=ot,Ze*=ot,ee.target.fx===null&&(ee.target.vx-=He*ee.bias,ee.target.vy-=Ze*ee.bias),ee.source.fx===null&&(ee.source.vx+=He*(1-ee.bias),ee.source.vy+=Ze*(1-ee.bias))}for(var ye=0;ye<o.length;ye++){var ze=o[ye];if(ze.fx===null){var bt=we+(ze.ax-we)*T,Mt=Be+(ze.ay-Be)*T,sn=ze.anchorStrength/(1+p*.35+p*.06*Math.max(0,(ze.degree||1)-2));ze.vx+=(bt-ze.x)*sn*S,ze.vy+=(Mt-ze.y)*sn*S}}for(var Dt=26*Math.min(T,2.2),yt=0;yt<2;yt++)for(var mt=0;mt<o.length;mt++)for(var Ye=o[mt],Ot=Dt*(1+p*.035*Math.max(0,(Ye.degree||1)-2)),Et=mt+1;Et<o.length;Et++){var Xe=o[Et],bn=Dt*(1+p*.035*Math.max(0,(Xe.degree||1)-2)),Nt=Ot+bn,ft=Xe.x+Xe.vx-(Ye.x+Ye.vx),Xt=Xe.y+Xe.vy-(Ye.y+Ye.vy),qt=ft*ft+Xt*Xt;if(!(qt>=Nt*Nt)){qt<.01&&(ft=.5,Xt=.5,qt=.5);var Bt=Math.sqrt(qt),wn=(Nt-Bt)/Bt*.42,Qt=ft*wn,zt=Xt*wn;Ye.fx===null&&Xe.fx===null?(Ye.vx-=Qt,Ye.vy-=zt,Xe.vx+=Qt,Xe.vy+=zt):Ye.fx!==null&&Xe.fx===null?(Xe.vx+=Qt*2,Xe.vy+=zt*2):Xe.fx!==null&&Ye.fx===null&&(Ye.vx-=Qt*2,Ye.vy-=zt*2)}}for(var Wn=0;Wn<o.length;Wn++){var Je=o[Wn];if(Je.fx!==null&&Je.fy!==null){Je.x=Je.fx,Je.y=Je.fy,Je.vx=0,Je.vy=0;continue}Je.vx*=se,Je.vy*=se;var Yt=Math.sqrt(Je.vx*Je.vx+Je.vy*Je.vy);Yt>28&&(Je.vx=Je.vx/Yt*28,Je.vy=Je.vy/Yt*28),Je.x+=Je.vx,Je.y+=Je.vy}}function _a(o){Lt=[],dn={};for(var g=0;g<M.length;g++){var S=M[g],T=!1;if(S.dto.subtypes){for(var C=0;C<S.dto.subtypes.length;C++)if(y[S.dto.subtypes[C]]){T=!0;break}}var p={kind:S.dto.kind,x:S.x,y:S.y,ax:S.ax,ay:S.ay,radius:25,fill:S.style.fill,glyph:S.style.glyph,isAbstract:!!S.dto.isAbstract,extendsKind:S.dto.extends||null,collapsedCount:o&&o[S.dto.kind]?o[S.dto.kind]:0,hasExpandedSubtypes:T};Lt.push(p),dn[p.kind]=p}}var Aa={};function Pr(o,g,S){Aa=S||{};for(var T=M.length===0,C=y,p=xr(o),N=[],R={},G=[],D=0;D<o.length;D++){var z=o[D];G.push(z.kind);var F=p[z.kind],K=C[z.kind];if(K){K.dto=z,K.style=F.style,K.ax=F.ax,K.ay=F.ay,K.anchorStrength=F.anchorStrength,N.push(K),R[z.kind]=K;continue}var re=we+(F.ax-we)*ne,xe=Be+(F.ay-Be)*ne,ue=re,Me=xe;if(!T&&z.extends){var Te=C[z.extends]||R[z.extends];Te&&(ue=Te.x+(re-Te.x)*.32,Me=Te.y+(xe-Te.y)*.32)}var Ne={dto:z,style:F.style,x:ue,y:Me,ax:F.ax,ay:F.ay,vx:0,vy:0,fx:null,fy:null,anchorStrength:F.anchorStrength};N.push(Ne),R[z.kind]=Ne}M=N,y=R,E=fs(R,g);var Ke=[];for(var oe in te)te[oe]&&Ke.push(oe);var U=G.join(",")+"|"+Ke.join(",");if(T){for(var ee=1,He=0;He<220;He++)ee+=(0-ee)*Fe,Ja(M,E,ee,ne,ge);for(var Ze=0;Ze<M.length;Ze++)M[Ze].vx=0,M[Ze].vy=0;ae=0}else U!==Ve&&!Se&&(ae=Math.max(ae,.42));Ve=U,_a(Aa)}function Ia(){for(var o=0;o<M.length;o++){var g=M[o];g.x=we+(g.ax-we)*ne,g.y=Be+(g.ay-Be)*ne,g.vx=0,g.vy=0,g.fx=null,g.fy=null}for(var S=1,T=0;T<220;T++)S+=(0-S)*Fe,Ja(M,E,S,ne,ge);for(var C=0;C<M.length;C++)M[C].vx=0,M[C].vy=0;ae=0,Ee=0,_a(Aa)}function ra(o,g){for(var S={},T=[],C=0;C<d.nodes.length;C++){var p=d.nodes[C];if(p.extends&&!(!o[p.kind]||!o[p.extends])){var N=p.kind+"|EXTENDS|"+p.extends,R={fromKind:p.kind,toKind:p.extends,rel:"EXTENDS",category:"type_dependency",isExtends:!0,priority:1};S[N]=R,T.push(R)}}for(var G=0;G<d.relationships.length;G++){var D=d.relationships[G];if(te[D.category])for(var z=0;z<D.pairs.length;z++){var F=D.pairs[z],K=g&&g[F.fromKind]||F.fromKind,re=g&&g[F.toKind]||F.toKind;if(!(!o[K]||!o[re])&&!(K===re&&F.fromKind!==F.toKind)){var xe=1;D.rel==="DECLARED_IN"||D.rel==="BELONGS_TO"?xe=4:kt[K]||kt[re]?xe=3:(K==="Service"||re==="Service"||K==="Workload"||re==="Workload"||D.category==="structural")&&(xe=2);var ue=K+"|"+D.rel+"|"+re;if(S[ue])xe<S[ue].priority&&(S[ue].priority=xe);else{var Me={fromKind:K,toKind:re,rel:D.rel,category:D.category,priority:xe};S[ue]=Me,T.push(Me)}}}}for(var Te={},Ne={},Ke=[],oe=0;oe<T.length;oe++){var U=T[oe];if(U.fromKind===U.toKind){var ee=Te[U.fromKind]||0;Te[U.fromKind]=ee+1,Ke.push({fromKind:U.fromKind,toKind:U.toKind,rel:U.rel,category:U.category,isSelf:!0,slot:ee,offset:0,priority:U.priority})}else{var He=U.fromKind<U.toKind?U.fromKind:U.toKind,Ze=U.fromKind<U.toKind?U.toKind:U.fromKind,et=He+"::"+Ze;Ne[et]||(Ne[et]=[]),Ne[et].push(U)}}for(var Ue in Ne)for(var qe=Ne[Ue],$e=0;$e<qe.length;$e++){var tt=qe[$e],ot=22;qe.length>1&&(ot=($e-(qe.length-1)/2)*34,Math.abs(ot)<6&&(ot=14)),Ke.push({fromKind:tt.fromKind,toKind:tt.toKind,rel:tt.rel,category:tt.category,isExtends:!!tt.isExtends,isSelf:!1,slot:0,offset:ot,priority:tt.priority})}return Ke.sort(function(ye,ze){return ye.priority-ze.priority}),Ke}var Lt=[],dn={},vn=[],ce={panX:0,panY:0,zoom:.72},Za=!1,rn=!0,La=0,Mr=0,Qe=null;function Wt(o,g,S,T,C,p){var N=Math.min(p,T/2,C/2);o.beginPath(),o.moveTo(g+N,S),o.arcTo(g+T,S,g+T,S+C,N),o.arcTo(g+T,S+C,g,S+C,N),o.arcTo(g,S+C,g,S,N),o.arcTo(g,S,g+T,S,N),o.closePath()}function fo(o,g,S,T){o.save(),o.translate(S,T),o.strokeStyle=k.inkBright,o.fillStyle=k.inkBright,o.lineWidth=1.65,o.lineCap="round",o.lineJoin="round",g==="globe"?(o.beginPath(),o.arc(0,0,6.8,0,Math.PI*2),o.stroke(),o.beginPath(),o.ellipse(0,0,3.1,6.8,0,0,Math.PI*2),o.stroke(),o.beginPath(),o.moveTo(-6.6,0),o.lineTo(6.6,0),o.stroke()):g==="database"||g==="procedure"?(o.beginPath(),o.ellipse(0,-5,6.5,2.6,0,0,Math.PI*2),o.stroke(),o.beginPath(),o.moveTo(-6.5,-5),o.lineTo(-6.5,5),o.arc(0,5,6.5,Math.PI,0,!0),o.lineTo(6.5,-5),o.stroke()):g==="schema"?(Wt(o,-6.5,-6,13,4.5,1.5),o.stroke(),Wt(o,-6.5,1.5,5.2,4.5,1.2),o.stroke(),Wt(o,1.3,1.5,5.2,4.5,1.2),o.stroke(),o.beginPath(),o.moveTo(0,-1.5),o.lineTo(0,0),o.moveTo(-3.9,0),o.lineTo(3.9,0),o.moveTo(-3.9,0),o.lineTo(-3.9,1.5),o.moveTo(3.9,0),o.lineTo(3.9,1.5),o.stroke()):g==="layers"?(o.beginPath(),o.moveTo(0,-6.5),o.lineTo(7,-3),o.lineTo(0,.5),o.lineTo(-7,-3),o.closePath(),o.stroke(),o.beginPath(),o.moveTo(-7,.8),o.lineTo(0,4.2),o.lineTo(7,.8),o.moveTo(-7,4.2),o.lineTo(0,7.2),o.lineTo(7,4.2),o.stroke()):g==="table"?(Wt(o,-7,-6,14,12,2),o.stroke(),o.beginPath(),o.moveTo(-7,-1.5),o.lineTo(7,-1.5),o.moveTo(-1.5,-1.5),o.lineTo(-1.5,6),o.stroke()):g==="query"?(o.beginPath(),o.arc(-1.5,-1.5,5,0,Math.PI*2),o.moveTo(2.2,2.2),o.lineTo(6.8,6.8),o.stroke()):g==="endpoint"?(o.beginPath(),o.arc(0,0,2.2,0,Math.PI*2),o.fill(),o.beginPath(),o.arc(0,0,5.5,-Math.PI*.7,Math.PI*.7),o.stroke(),o.beginPath(),o.arc(0,0,5.5,Math.PI*.3,Math.PI*1.7),o.stroke()):g==="topic"?(Wt(o,-7,-5,14,10,2),o.stroke(),o.beginPath(),o.moveTo(-6.5,-4.2),o.lineTo(0,1),o.lineTo(6.5,-4.2),o.stroke()):g==="cloud"||g==="cloudUp"?(o.beginPath(),o.arc(-2.5,1,3.8,Math.PI*.5,Math.PI*1.5),o.arc(1,-1.5,4.2,Math.PI,Math.PI*1.95),o.arc(4.2,1.5,3.2,Math.PI*1.5,Math.PI*.5),o.closePath(),o.stroke()):g==="function"?(o.beginPath(),o.moveTo(2.5,-6.5),o.quadraticCurveTo(-1,-6.5,-1,-2),o.lineTo(-1,6.5),o.moveTo(-4.5,-1),o.lineTo(3,-1),o.stroke()):g==="type"?(o.beginPath(),o.moveTo(0,-7),o.lineTo(6.5,-3.5),o.lineTo(6.5,3.5),o.lineTo(0,7),o.lineTo(-6.5,3.5),o.lineTo(-6.5,-3.5),o.closePath(),o.stroke()):g==="param"||g==="arg"||g==="literal"?(o.beginPath(),o.moveTo(-4,-5.5),o.lineTo(-7,0),o.lineTo(-4,5.5),o.moveTo(4,-5.5),o.lineTo(7,0),o.lineTo(4,5.5),g!=="param"&&(o.moveTo(-1.5,0),o.lineTo(1.5,0)),o.stroke()):g==="folder"?(o.beginPath(),o.moveTo(-7,-5),o.lineTo(-2,-5),o.lineTo(0,-3),o.lineTo(7,-3),o.lineTo(7,5.5),o.lineTo(-7,5.5),o.closePath(),o.stroke()):g==="file"?(o.beginPath(),o.moveTo(-5,-6.5),o.lineTo(2,-6.5),o.lineTo(5.5,-3),o.lineTo(5.5,6.5),o.lineTo(-5,6.5),o.closePath(),o.stroke()):g==="package"||g==="project"?(Wt(o,-6.5,-5.5,13,11,2),o.stroke(),o.beginPath(),o.moveTo(-6.5,-1.5),o.lineTo(6.5,-1.5),o.stroke()):g==="zap"?(o.beginPath(),o.moveTo(1,-7),o.lineTo(-5,.5),o.lineTo(0,.5),o.lineTo(-1,7),o.lineTo(5,-.5),o.lineTo(0,-.5),o.closePath(),o.stroke()):(Wt(o,-6.5,-5.5,13,11,2.5),o.stroke(),o.beginPath(),o.moveTo(-3.5,0),o.lineTo(3.5,0),o.stroke()),o.restore()}function Dr(o,g,S,T,C,p){o.save(),o.translate(g,S),o.rotate(T),o.fillStyle=C,o.beginPath(),o.moveTo(0,0),o.lineTo(-p,-p*.54),o.lineTo(-p*.78,0),o.lineTo(-p,p*.54),o.closePath(),o.fill(),o.restore()}function vs(o,g,S,T,C,p){o.save(),o.translate(g,S),o.rotate(T),o.fillStyle=k.canvasBg,o.strokeStyle=C,o.lineWidth=1.5,o.lineJoin="round",o.beginPath(),o.moveTo(0,0),o.lineTo(-p,-p*.58),o.lineTo(-p,p*.58),o.closePath(),o.fill(),o.stroke(),o.restore()}function Nn(){var o=document.getElementById("ont-canvas-frame");if(!(!o||Lt.length===0)){var g=o.clientWidth||760,S=o.clientHeight||640,T=document.getElementById("ont-filter-overlay"),C=0;T&&getComputedStyle(T).position==="absolute"&&(C=(T.offsetHeight||30)+14);for(var p=Math.max(160,S-C),N=1e9,R=1e9,G=-1e9,D=-1e9,z=0;z<Lt.length;z++){var F=Lt[z],K=F.x,re=F.y;if(ae>.05&&typeof F.ax=="number"){var xe=we+(F.ax-we)*ne,ue=Be+(F.ay-Be)*ne;xe-68<N&&(N=xe-68),ue-74<R&&(R=ue-74),xe+68>G&&(G=xe+68),ue+88>D&&(D=ue+88)}K-68<N&&(N=K-68),re-74<R&&(R=re-74),K+68>G&&(G=K+68),re+108>D&&(D=re+108)}var Me=Math.max(200,G-N),Te=Math.max(200,D-R),Ne=Math.max(.22,Math.min(1.85,Math.min((g-40)/Me,(p-36)/Te)));ce.zoom=Ne,ce.panX=g/2-(N+G)/2*Ne,ce.panY=C+p/2-(R+D)/2*Ne}}function er(o,g){for(var S=(o-ce.panX)/ce.zoom,T=(g-ce.panY)/ce.zoom,C=Lt.length-1;C>=0;C--){var p=Lt[C],N=!!(p.collapsedCount&&p.collapsedCount>0||p.hasExpandedSubtypes);if(N){var R=p.x-p.radius*.78-14,G=p.y-p.radius*.74-9;if(S>=R-4&&S<=R+34&&T>=G-4&&T<=G+22)return p.kind}}return null}function tr(o,g){for(var S=(o-ce.panX)/ce.zoom,T=(g-ce.panY)/ce.zoom,C=Lt.length-1;C>=0;C--){var p=Lt[C],N=S-p.x,R=T-p.y;if(N*N+R*R<=1024)return p}return null}function Tt(){if(w==="graph"){var o=document.getElementById("ont-graph-canvas"),g=document.getElementById("ont-canvas-frame");if(!(!o||!g)){var S=g.clientWidth||760,T=g.clientHeight||640;!Za&&S>80&&Lt.length>0&&(Za=!0,Nn());var C=window.devicePixelRatio||1;o.width=Math.max(1,Math.floor(S*C)),o.height=Math.max(1,Math.floor(T*C));var p=o.getContext("2d");if(p){p.setTransform(C,0,0,C,0,0),p.fillStyle=k.canvasBg,p.fillRect(0,0,S,T),p.fillStyle=k.gridDot;for(var N=32,R=(ce.panX%N+N)%N,G=(ce.panY%N+N)%N,D=R;D<S;D+=N)for(var z=G;z<T;z+=N)p.fillRect(D,z,1.4,1.4);var F=Q,K={};if(F){K[F]=!0;for(var re=dn[F],xe={};re&&re.extendsKind&&!xe[re.extendsKind];)xe[re.extendsKind]=!0,K[re.extendsKind]=!0,re=dn[re.extendsKind]}var ue={};for(var Me in K)ue[Me]=!0;if(F)for(var Te=0;Te<vn.length;Te++)K[vn[Te].fromKind]&&(ue[vn[Te].toKind]=!0),K[vn[Te].toKind]&&(ue[vn[Te].fromKind]=!0);p.save(),p.translate(ce.panX,ce.panY),p.scale(ce.zoom,ce.zoom);for(var Ne=[],Ke=0;Ke<vn.length;Ke++){var oe=vn[Ke],U=dn[oe.fromKind],ee=dn[oe.toKind];if(!(!U||!ee)){var He=!!(F&&(K[oe.fromKind]||K[oe.toKind])),Ze=!!(F&&!He),et=Ze?k.edgeDim:oe.isExtends?He?k.capHoverBorder:k.abstractRing:He?at[oe.category]||k.edgeControl:k.edgeIdle;if(p.strokeStyle=et,p.lineWidth=He?1.9:oe.isExtends?1.5:1.25,oe.isSelf){var Ue=-Math.PI*.64-oe.slot*.55,qe=Ue-.42,$e=Ue+.42,tt=U.radius,ot=U.x+Math.cos(qe)*tt,ye=U.y+Math.sin(qe)*tt,ze=U.x+Math.cos($e)*(tt+3),bt=U.y+Math.sin($e)*(tt+3),Mt=tt+42+oe.slot*16,sn=U.x+Math.cos(qe-.18)*Mt,Dt=U.y+Math.sin(qe-.18)*Mt,yt=U.x+Math.cos($e+.18)*Mt,mt=U.y+Math.sin($e+.18)*Mt;p.beginPath(),p.moveTo(ot,ye),p.bezierCurveTo(sn,Dt,yt,mt,ze,bt),p.stroke(),Ze||(Dr(p,ze,bt,Math.atan2(bt-mt,ze-yt),et,7),Ne.push({mx:.125*ot+.375*sn+.375*yt+.125*ze,my:.125*ye+.375*Dt+.375*mt+.125*bt,text:oe.rel,active:He,prio:He?0:oe.priority}));continue}var Ye=oe.fromKind<oe.toKind,Ot=Ye?U.x:ee.x,Et=Ye?U.y:ee.y,Xe=Ye?ee.x:U.x,bn=Ye?ee.y:U.y,Nt=Xe-Ot,ft=bn-Et,Xt=Math.sqrt(Nt*Nt+ft*ft)||1,qt=(U.x+ee.x)/2+-ft/Xt*oe.offset,Bt=(U.y+ee.y)/2+Nt/Xt*oe.offset,wn=qt-U.x,Qt=Bt-U.y,zt=Math.sqrt(wn*wn+Qt*Qt)||1,Wn=U.x+wn/zt*U.radius,Je=U.y+Qt/zt*U.radius,Yt=ee.x-qt,Sn=ee.y-Bt,Un=Math.sqrt(Yt*Yt+Sn*Sn)||1,qn=ee.x-Yt/Un*(ee.radius+(oe.isExtends?4:3)),sr=ee.y-Sn/Un*(ee.radius+(oe.isExtends?4:3));p.save(),oe.isExtends&&p.setLineDash([5,4]),p.beginPath(),p.moveTo(Wn,Je),p.quadraticCurveTo(qt,Bt,qn,sr),p.stroke(),p.restore(),Ze||(oe.isExtends?vs(p,qn,sr,Math.atan2(Sn,Yt),et,He?9.5:8.4):Dr(p,qn,sr,Math.atan2(Sn,Yt),et,He?7.8:6.6),Ne.push({mx:.25*Wn+.5*qt+.25*qn,my:.25*Je+.5*Bt+.25*sr,text:oe.rel,active:He,isExtends:oe.isExtends,prio:He?0:oe.priority}))}}for(var or=[],Oa=0;Oa<Lt.length;Oa++){var _e=Lt[Oa],ca=_e.kind===Q,Ss=_e.kind===x,kn=!!(F&&!ue[_e.kind]),ir=_e.x*ce.zoom+ce.panX,Ba=_e.y*ce.zoom+ce.panY,Fa=(_e.radius+14)*ce.zoom;if(or.push({minX:ir-Fa,minY:Ba-Fa,maxX:ir+Fa,maxY:Ba+Fa+18*ce.zoom}),p.save(),kn&&(p.globalAlpha=.34),(ca||Ss)&&(p.beginPath(),p.arc(_e.x,_e.y,_e.radius+6.5,0,Math.PI*2),p.fillStyle=ca?k.nodeHalo:k.nodeHover,p.fill()),_e.isAbstract&&(p.save(),p.beginPath(),p.arc(_e.x,_e.y,_e.radius+4,0,Math.PI*2),p.setLineDash([3.5,3]),p.lineWidth=1.3,p.strokeStyle=k.abstractRing,p.stroke(),p.restore()),p.beginPath(),p.arc(_e.x,_e.y,_e.radius,0,Math.PI*2),p.fillStyle=_e.fill,p.fill(),p.lineWidth=ca?2.4:1.7,p.strokeStyle=ca?k.edgeControl:k.nodeRing,p.stroke(),fo(p,_e.glyph,_e.x,_e.y),_e.collapsedCount&&_e.collapsedCount>0||_e.hasExpandedSubtypes){var Ka=_e.collapsedCount&&_e.collapsedCount>0?"+"+_e.collapsedCount:"−";p.font='400 10px "Atkinson Hyperlegible Mono", ui-monospace, monospace';var da=Math.max(22,p.measureText(Ka).width+10),lr=17,Ft=_e.x-_e.radius*.78-da/2,Qn=_e.y-_e.radius*.74-lr/2,cr=X===_e.kind;Wt(p,Ft,Qn,da,lr,8.5),p.fillStyle=cr?k.capHeadHoverBg:k.capHeadBg,p.fill(),p.lineWidth=1.2,p.strokeStyle=cr?k.capHoverBorder:k.capInnerBorder,p.stroke(),p.fillStyle=k.inkBright,p.textAlign="center",p.textBaseline="middle",p.fillText(Ka,Ft+da/2,Qn+lr/2+.5)}p.font='400 12px "Host Grotesk", system-ui, sans-serif',p.fillStyle=kn?k.inkDim:k.inkBright,p.textAlign="center",p.textBaseline="top",p.fillText(_e.kind,_e.x,_e.y+_e.radius+7),p.restore()}if(p.restore(),ce.zoom>=.42){Ne.sort(function(Hr,vo){return Hr.prio-vo.prio}),p.font='400 9.5px "Atkinson Hyperlegible Mono", ui-monospace, monospace',p.textAlign="center",p.textBaseline="middle";for(var zn=0;zn<Ne.length;zn++){var Tn=Ne[zn],Mn=Tn.mx*ce.zoom+ce.panX,Dn=Tn.my*ce.zoom+ce.panY;if(!(Mn<-40||Mn>S+40||Dn<-20||Dn>T+20)){var On=p.measureText(Tn.text).width+11,Yn=16,pa={minX:Mn-On/2-3,minY:Dn-Yn/2-2,maxX:Mn+On/2+3,maxY:Dn+Yn/2+2},ks=!1;if(!Tn.active)for(var Gr=0;Gr<or.length;Gr++){var dr=or[Gr];if(!(pa.maxX<dr.minX||pa.minX>dr.maxX||pa.maxY<dr.minY||pa.minY>dr.maxY)){ks=!0;break}}ks||(or.push(pa),Wt(p,Mn-On/2,Dn-Yn/2,On,Yn,5),p.fillStyle=k.pillBg,p.fill(),p.lineWidth=1,p.strokeStyle=Tn.active?k.pillBorderActive:Tn.isExtends?k.capInnerBorder:k.pillBorder,p.stroke(),p.fillStyle=Tn.active?k.inkBright:Tn.isExtends?k.capHoverBorder:k.inkSoft,p.fillText(Tn.text,Mn,Dn+.5))}}}}}}}function Or(o){var g={},S=[],T={},C=O[o];if(!C)return{badges:g,conns:S};for(var p=C.inheritedOutEdges?C.outEdges.concat(C.inheritedOutEdges):C.outEdges,N=0;N<p.length;N++){var R=p[N];if(te[R.category]){var G="out|"+R.rel+"|"+R.toKind;T[G]||(T[G]=!0,g[R.toKind]||(g[R.toKind]=[]),g[R.toKind].push({dir:"out",rel:R.rel,cat:R.category}),S.push({peer:R.toKind,rel:R.rel,dir:"out",cat:R.category}))}}for(var D=C.inheritedInEdges?C.inEdges.concat(C.inheritedInEdges):C.inEdges,z=0;z<D.length;z++){var F=D[z];if(te[F.category]){var K="in|"+F.rel+"|"+F.fromKind;T[K]||(T[K]=!0,g[F.fromKind]||(g[F.fromKind]=[]),g[F.fromKind].push({dir:"in",rel:F.rel,cat:F.category}),S.push({peer:F.fromKind,rel:F.rel,dir:"in",cat:F.category}))}}return{badges:g,conns:S}}function Br(o,g){if(o){var S=document.getElementById("insp-layer-pill");S.textContent="Layer "+o.layerId,S.className="ont-layer-pill ont-layer-pill--l"+o.layerId;var T=document.getElementById("insp-abstract-pill");T&&(T.hidden=!o.isAbstract);var C=document.getElementById("insp-lineage");if(C){var p="";if(o.ancestors&&o.ancestors.length>0){p+='<div class="ont-lineage-row"><span class="ont-layer-sub">Extends</span>';for(var N=0;N<o.ancestors.length;N++){var R=o.ancestors[N];p+='<button type="button" class="ont-jump-btn" data-select-kind="'+Z(R)+'">:'+Z(R)+"</button>"}p+="</div>"}if(o.subtypes&&o.subtypes.length>0){p+='<div class="ont-lineage-row"><span class="ont-layer-sub">Subtypes ('+o.subtypes.length+")</span>";for(var G=0;G<o.subtypes.length;G++){var D=o.subtypes[G];p+='<button type="button" class="ont-jump-btn" data-select-kind="'+Z(D)+'">:'+Z(D)+"</button>"}p+="</div>"}C.innerHTML=p,C.hidden=!p}document.getElementById("insp-plural").textContent=o.pluralLabel,document.getElementById("insp-title").textContent=":"+o.kind,document.getElementById("insp-purpose").textContent=o.purpose,document.getElementById("insp-urn").textContent=o.idScheme||"n/a",document.getElementById("insp-props-heading").textContent="Collected properties ("+o.properties.length+" + "+d.commonProperties.length+" base)";for(var z="",F=0;F<o.properties.length;F++){var K=o.properties[F],re=g&&(K.name.toLowerCase().indexOf(g)!==-1||K.description.toLowerCase().indexOf(g)!==-1),xe=K.inheritedFrom?'<code class="ont-edge-prop">via :'+Z(K.inheritedFrom)+"</code>":"";z+='<div class="ont-prop-item'+(re?" is-match":"")+'"><div class="ont-prop-top"><code class="ont-prop-name">'+Z(K.name)+'</code><span class="ont-prop-right">'+xe+'<code class="ont-prop-type">'+Z(K.type)+'</code></span></div><span class="ont-prop-desc">'+Z(K.description)+"</span></div>"}document.getElementById("insp-props-list").innerHTML=z;var ue=o.inheritedOutEdges?o.outEdges.concat(o.inheritedOutEdges):o.outEdges,Me=o.inheritedInEdges?o.inEdges.concat(o.inheritedInEdges):o.inEdges;document.getElementById("insp-rels-heading").textContent="Typed relationships ("+ue.length+" out, "+Me.length+" in)";for(var Te="",Ne=0;Ne<ue.length;Ne++){var Ke=ue[Ne],oe=pe[Ke.rel],U="";if(oe&&oe.properties&&oe.properties.length>1){U='<div class="ont-edge-props-row">';for(var ee=0;ee<oe.properties.length;ee++){var He=oe.properties[ee];U+='<code class="ont-edge-prop" title="'+Z(He.description)+'">r.'+Z(He.name)+"</code>"}U+="</div>"}var Ze=Ke.inheritedFrom?'<code class="ont-edge-prop">via :'+Z(Ke.inheritedFrom)+"</code>":"";Te+='<div class="ont-rel-row"><div class="ont-rel-line"><code class="ont-edge-tag ont-edge-tag--'+Z(Ke.category)+'">-[:'+Z(Ke.rel)+']-&gt;</code><button type="button" class="ont-jump-btn" data-select-kind="'+Z(Ke.toKind)+'">:'+Z(Ke.toKind)+"</button>"+Ze+'</div><span class="ont-prop-desc">'+Z(Ke.description)+"</span>"+U+"</div>"}for(var et=0;et<Me.length;et++){var Ue=Me[et],qe=Ue.inheritedFrom?'<code class="ont-edge-prop">via :'+Z(Ue.inheritedFrom)+"</code>":"";Te+='<div class="ont-rel-row"><div class="ont-rel-line"><button type="button" class="ont-jump-btn" data-select-kind="'+Z(Ue.fromKind)+'">:'+Z(Ue.fromKind)+'</button><code class="ont-edge-tag ont-edge-tag--'+Z(Ue.category)+'">-[:'+Z(Ue.rel)+"]-&gt;</code>"+qe+'</div><span class="ont-prop-desc">'+Z(Ue.description)+"</span></div>"}document.getElementById("insp-rels-list").innerHTML=Te,document.getElementById("insp-cypher").textContent=o.sampleCypher;var $e=document.getElementById("insp-compact-props-label");$e&&($e.textContent="Properties ("+o.properties.length+" + "+d.commonProperties.length+" base)");var tt=document.getElementById("insp-compact-props");if(tt){for(var ot="",ye=Math.min(6,o.properties.length),ze=0;ze<ye;ze++){var bt=o.properties[ze];ot+='<code class="ont-edge-prop" title="'+Z(bt.description)+'">'+Z(bt.name)+": "+Z(bt.type)+"</code>"}o.properties.length>6&&(ot+='<code class="ont-edge-prop">+'+(o.properties.length-6)+" more</code>"),tt.innerHTML=ot}var Mt=document.getElementById("insp-compact-rels-label");Mt&&(Mt.textContent="Connected entities ("+ue.length+" out, "+Me.length+" in)");var sn=document.getElementById("insp-compact-rels");if(sn){for(var Dt={},yt=[],mt=0;mt<ue.length;mt++){var Ye=ue[mt].toKind;Dt[Ye]||(Dt[Ye]=!0,yt.push(Ye))}for(var Ot=0;Ot<Me.length;Ot++){var Et=Me[Ot].fromKind;Dt[Et]||(Dt[Et]=!0,yt.push(Et))}for(var Xe="",bn=Math.min(6,yt.length),Nt=0;Nt<bn;Nt++){var ft=yt[Nt];Xe+='<button type="button" class="ont-jump-btn" data-select-kind="'+Z(ft)+'">:'+Z(ft)+"</button>"}yt.length>6&&(Xe+='<code class="ont-edge-prop">+'+(yt.length-6)+" more</code>"),sn.innerHTML=Xe}var Xt=document.getElementById("entity-inspector"),qt=document.getElementById("ont-graph-panel"),Bt=!!(qt&&qt.classList.contains("is-fullscreen"));if(Xt){Xt.hidden=w==="graph"&&!Q;var wn=w==="layers"||Bt||j;Xt.classList.toggle("is-compact",!wn),Xt.classList.toggle("is-expanded-view",wn)}var Qt=document.getElementById("insp-toggle-mode-btn");Qt&&(Qt.hidden=w==="layers"||Bt,Qt.textContent=j?"Compact":"Full");var zt=document.getElementById("insp-close-btn");zt&&(zt.hidden=w==="layers")}}function sa(o){var g=document.getElementById("ont-focus-label"),S=document.getElementById("ont-focus-count"),T=document.getElementById("ont-focus-links");if(!(!g||!S||!T)){if(g.textContent="Active connections for :"+h,S.textContent=o.length+" active links",o.length===0){T.innerHTML='<span class="ont-layer-sub">Enable relationship category filters above to display connected entities.</span>';return}for(var C=Math.min(12,o.length),p="",N=0;N<C;N++){var R=o[N],G=R.dir==="out"?"out: "+Z(R.rel)+" :"+Z(R.peer):"in: :"+Z(R.peer)+" "+Z(R.rel);p+='<button type="button" class="ont-focus-chip ont-focus-chip--'+Z(R.cat)+'" data-select-kind="'+Z(R.peer)+'"><code>'+G+"</code></button>"}T.innerHTML=p}}function Rn(o,g){return!!ve[o.layerId]&&L(o,g)}function xn(){if(Le!==null||Se)return;function o(){if(Le=null,!(Se||w!=="graph")){if(ae+=(Ee-ae)*Fe,ae<B&&Ee===0){ae=0;return}Ja(M,E,ae,ne,ge),_a(Aa),Tt(),Le=requestAnimationFrame(o)}}Le=requestAnimationFrame(o)}function bs(o){for(var g=[],S=0;S<d.nodes.length;S++){var T=d.nodes[S];Rn(T,o)&&g.push(T)}for(var C=ct(g),p=C.visibleDtos,N={},R=[],G=0;G<p.length;G++)N[p[G].kind]=!0,R.push(p[G].kind);for(var D=[],z=0;z<Ge.length;z++){var F=Ge[z].kind;D.push(F+":"+(ke[F]?"1":"0"))}var K=R.join(",")+"|"+D.join(","),re=!!(Ce&&Ce!==K);Ce=K,Q&&!N[Q]&&(Q=C.redirectMap[Q]||null),Pr(p,C.redirectMap,C.collapsedCounts),vn=ra(N,C.redirectMap),re&&Za&&Lt.length>0&&(rn=!0,Nn()),!Se&&(ae>=B||Ee>0)&&xn()}function Ct(){var o=de.trim().toLowerCase(),g=O[h];if(!g||!ve[g.layerId]){for(var S=0;S<d.nodes.length;S++)if(Rn(d.nodes[S],o)){h=d.nodes[S].kind;break}if(!O[h]||!ve[O[h].layerId]){for(var T=0;T<d.nodes.length;T++)if(ve[d.nodes[T].layerId]){h=d.nodes[T].kind;break}}}for(var C=Or(h),p=document.querySelectorAll(".ont-node-btn"),N=["control_flow","data_flow","type_dependency","structural"],R=0;R<p.length;R++){var G=p[R],D=G.getAttribute("data-kind"),z=O[D];if(z){var F=Rn(z,o);G.hidden=!F;var K=D===h,re=C.badges[D]||[],xe=re.length>0,ue=xe?re[0].cat:"";G.setAttribute("aria-selected",K?"true":"false"),G.classList.toggle("is-neighbor",!K&&xe),G.classList.toggle("is-dimmed",!K&&!xe);for(var Me=0;Me<N.length;Me++)G.classList.toggle("is-neighbor--"+N[Me],!K&&xe&&ue===N[Me])}}for(var Te=document.querySelectorAll(".ont-layer[data-layer-id]"),Ne=0;Ne<Te.length;Ne++){var Ke=Number(Te[Ne].getAttribute("data-layer-id"));Te[Ne].hidden=!ve[Ke]}for(var oe=document.querySelectorAll(".ont-layer-btn"),U=0;U<oe.length;U++){var ee=Number(oe[U].getAttribute("data-layer"));oe[U].setAttribute("aria-pressed",ve[ee]?"true":"false")}var He=document.getElementById("ont-capsule-group");He&&(He.hidden=w!=="graph"||!ve[4]);for(var Ze=document.querySelectorAll(".ont-capsule-btn"),et=0;et<Ze.length;et++){var Ue=Ze[et].getAttribute("data-hierarchy"),qe=!!ke[Ue],$e=O[Ue];if($e&&$e.ancestors)for(var tt=0;tt<$e.ancestors.length;tt++)ke[$e.ancestors[tt]]||(qe=!1);Ze[et].setAttribute("aria-pressed",qe?"true":"false")}var ot=document.getElementById("ont-graph-panel"),ye=document.getElementById("ont-canvas-frame"),ze=document.getElementById("ont-graph-foot"),bt=document.getElementById("ont-graph-zoom-group"),Mt=document.getElementById("ont-swimlanes"),sn=ye?ye.hidden:!1;ot&&ot.classList.toggle("is-layers-mode",w==="layers"),ye&&(ye.hidden=w!=="graph"),ze&&(ze.hidden=w!=="graph"),bt&&(bt.hidden=w!=="graph"),Mt&&(Mt.hidden=w!=="layers");var Dt=document.getElementById("graph-show-all");Dt&&(Dt.hidden=!Q);for(var yt=document.querySelectorAll(".ont-mode-btn"),mt=0;mt<yt.length;mt++){var Ye=yt[mt].getAttribute("data-mode");yt[mt].setAttribute("aria-pressed",Ye===w?"true":"false")}bs(o),sn&&w==="graph"&&(rn=!0,Nn()),Tt(),sa(C.conns),Br(O[h],o)}function Fr(o){if(o){var g=!ke[o],S=O[o];if(S&&S.ancestors)for(var T=0;T<S.ancestors.length;T++)ke[S.ancestors[T]]||(g=!0,ke[S.ancestors[T]]=!0);ke[o]=g,rn=!0,Ct(),Nn(),Tt()}}document.addEventListener("click",function(o){var g=o.target.closest("#insp-close-btn");if(g){Q=null,j=!1,Ct();return}var S=o.target.closest("#insp-toggle-mode-btn");if(S){j=!j,Ct();return}var T=o.target.closest(".ont-mode-btn");if(T){w=T.getAttribute("data-mode")||"graph",Ct();return}var C=o.target.closest(".ont-capsule-btn");if(C){Fr(C.getAttribute("data-hierarchy"));return}var p=o.target.closest(".ont-layer-btn");if(p){var N=Number(p.getAttribute("data-layer"));ve[N]=!ve[N],!ve[5]&&!ve[4]&&!ve[3]&&!ve[2]&&!ve[1]&&(ve={5:!0,4:!0,3:!0,2:!0,1:!0}),Ct();return}var R=o.target.closest(".ont-node-btn");if(R){h=R.getAttribute("data-kind"),Ct();return}var G=o.target.closest("[data-select-kind]");if(G){h=G.getAttribute("data-select-kind");var D=O[h];if(D&&(ve[D.layerId]=!0,D.ancestors))for(var z=0;z<D.ancestors.length;z++)ke[D.ancestors[z]]=!0;w==="graph"&&(Q=h),Ct();return}var F=o.target.closest(".ont-cat-btn");if(F){var K=F.getAttribute("data-cat");te[K]=!te[K],F.setAttribute("aria-pressed",te[K]?"true":"false"),Ct();return}});var gt=document.getElementById("ont-graph-canvas");gt&&(gt.style.cursor="grab",gt.addEventListener("pointerdown",function(o){var g=gt.getBoundingClientRect(),S=o.clientX-g.left,T=o.clientY-g.top,C=er(S,T);if(C){gt.setPointerCapture(o.pointerId),Qe={mode:"capsule",kind:C,sx:o.clientX,sy:o.clientY,ox:0,oy:0,moved:!1};return}var p=tr(S,T);if(gt.setPointerCapture(o.pointerId),gt.style.cursor="grabbing",p){Qe={mode:"node",kind:p.kind,sx:o.clientX,sy:o.clientY,ox:p.x,oy:p.y,moved:!1};var N=y[p.kind];N&&(N.fx=N.x,N.fy=N.y),Se||(Ee=.3,ae=Math.max(ae,.3),xn())}else Qe={mode:"pan",kind:null,sx:o.clientX,sy:o.clientY,ox:ce.panX,oy:ce.panY,moved:!1}}),gt.addEventListener("pointermove",function(o){var g=gt.getBoundingClientRect();if(Qe){var S=o.clientX-Qe.sx,T=o.clientY-Qe.sy;if((Math.abs(S)>4||Math.abs(T)>4)&&(Qe.moved=!0),Qe.mode==="pan")Qe.moved&&(rn=!1),ce.panX=Qe.ox+S,ce.panY=Qe.oy+T,Tt();else if(Qe.mode==="node"&&Qe.kind){var C=Qe.ox+S/ce.zoom,p=Qe.oy+T/ce.zoom,N=y[Qe.kind];N&&(N.fx=C,N.fy=p,N.x=C,N.y=p),dn[Qe.kind]&&(dn[Qe.kind].x=C,dn[Qe.kind].y=p),Se||(ae=Math.max(ae,.3),xn()),Tt()}return}var R=o.clientX-g.left,G=o.clientY-g.top,D=er(R,G),z=D?null:tr(R,G),F=z?z.kind:null;gt.style.cursor=D||F?"pointer":"grab",(D!==X||F!==x)&&(X=D,x=F,Tt())}),gt.addEventListener("pointerup",function(o){gt.hasPointerCapture(o.pointerId)&&gt.releasePointerCapture(o.pointerId);var g=Qe;if(Qe=null,gt.style.cursor=X||x?"pointer":"grab",g&&g.mode==="node"&&g.kind){Ee=0;var S=y[g.kind];S&&!Se&&(S.fx=null,S.fy=null),Se||xn()}!g||g.moved||(g.mode==="capsule"&&g.kind?(X=null,Fr(g.kind)):g.mode==="node"&&g.kind?(h=g.kind,Q=Q===g.kind?null:g.kind,Ct()):Q&&(Q=null,Ct()))}),gt.addEventListener("pointerleave",function(){!Qe&&(x||X)&&(x=null,X=null,Tt())}),gt.addEventListener("wheel",function(o){o.preventDefault(),rn=!1;var g=gt.getBoundingClientRect(),S=o.clientX-g.left,T=o.clientY-g.top,C=o.deltaY<0?1.12:.89,p=Math.max(.22,Math.min(2.8,ce.zoom*C)),N=p/ce.zoom;ce.zoom=p,ce.panX=S-(S-ce.panX)*N,ce.panY=T-(T-ce.panY)*N,Tt()},{passive:!1}));function nr(o){rn=!1;var g=document.getElementById("ont-canvas-frame"),S=(g?g.clientWidth:760)/2,T=(g?g.clientHeight:640)/2,C=Math.max(.22,Math.min(2.8,ce.zoom*o)),p=C/ce.zoom;ce.zoom=C,ce.panX=S-(S-ce.panX)*p,ce.panY=T-(T-ce.panY)*p,Tt()}var Kr=document.getElementById("graph-zoom-in"),oa=document.getElementById("graph-zoom-out"),ar=document.getElementById("graph-zoom-fit"),ia=document.getElementById("graph-show-all"),Na=document.getElementById("graph-fullscreen"),Ut=document.getElementById("ont-graph-panel"),Pn=document.getElementById("ont-canvas-frame");function Ra(){if(Pn){var o=Pn.clientWidth||0,g=Pn.clientHeight||0;o<=0||g<=0||o===La&&g===Mr||(La=o,Mr=g,rn&&Nn(),Tt())}}function xa(o){Ut&&(rn=!0,Ut.classList.toggle("is-fullscreen",o),document.body.classList.toggle("has-fullscreen-graph",o),Na&&(Na.setAttribute("aria-pressed",o?"true":"false"),Na.textContent=o?"Exit":"Expand"),Ct(),Nn(),Tt(),requestAnimationFrame(function(){Nn(),Tt()}))}Na&&Ut&&Na.addEventListener("click",function(){var o=Ut.classList.contains("is-fullscreen");xa(!o)}),document.addEventListener("keydown",function(o){o.key==="Escape"&&Ut&&Ut.classList.contains("is-fullscreen")&&xa(!1)}),document.addEventListener("mousedown",function(o){Ut&&Ut.classList.contains("is-fullscreen")&&!Ut.contains(o.target)&&xa(!1)});var Pa=document.getElementById("graph-air-slider"),rr=document.getElementById("graph-air-val"),Ma=document.getElementById("graph-hub-slider"),pn=document.getElementById("graph-hub-val"),Da=document.getElementById("graph-freeze");if(Pa&&(Pa.value=String(ne)),rr&&(rr.textContent=ne.toFixed(1)+"x"),Ma&&(Ma.value=String(ge)),pn&&(pn.textContent=ge.toFixed(1)+"x"),Pa&&Pa.addEventListener("input",function(){var o=parseFloat(Pa.value)||1;ne=Math.max(.1,Math.min(2.4,o)),rr&&(rr.textContent=ne.toFixed(1)+"x");try{localStorage.setItem("ce_ontology_air",String(ne))}catch{}Se||(ae=Math.max(ae,.38),xn())}),Ma&&Ma.addEventListener("input",function(){var o=parseFloat(Ma.value),g=isNaN(o)?1:o;ge=Math.max(0,Math.min(5,g)),pn&&(pn.textContent=ge.toFixed(1)+"x");try{localStorage.setItem("ce_ontology_hub",String(ge))}catch{}Se||(ae=Math.max(ae,.42),xn())}),Da&&Da.addEventListener("click",function(){if(Se=!Se,Da.setAttribute("aria-pressed",Se?"true":"false"),Da.textContent=Se?"Frozen":"Freeze",Se){ae=0,Ee=0;for(var o=0;o<M.length;o++)M[o].vx=0,M[o].vy=0}else{for(var g=0;g<M.length;g++)M[g].fx=null,M[g].fy=null;ae=Math.max(ae,.28),xn()}}),Kr&&Kr.addEventListener("click",function(){nr(1.18)}),oa&&oa.addEventListener("click",function(){nr(.84)}),ar&&ar.addEventListener("click",function(){Q=null,rn=!0,Ia(),Ct(),Nn(),Tt()}),ia&&ia.addEventListener("click",function(){Q=null,Ct()}),Pn&&typeof ResizeObserver<"u"){var ws=new ResizeObserver(function(){Ra()});ws.observe(Pn)}window.addEventListener("resize",function(){Ra(),Tt()});var jr=document.getElementById("ont-search");jr&&jr.addEventListener("input",function(){de=jr.value||"",Ct()});var la=document.getElementById("copy-cypher-btn");return la&&la.addEventListener("click",function(){var o=document.getElementById("insp-cypher").textContent;navigator.clipboard&&navigator.clipboard.writeText(o).then(function(){var g=la.textContent;la.textContent="Copied",setTimeout(function(){la.textContent=g},1500)})}),Ct(),()=>{document.body.classList.remove("has-fullscreen-graph"),typeof Le<"u"&&Le!==null&&cancelAnimationFrame(Le)}}const Cu="/ontology",Eu=()=>{lt.useEffect(()=>Tu(),[]);const d=aa[Cu];return l.jsx("main",{id:"main-content",className:"ontology-page",dangerouslySetInnerHTML:{__html:d.mainHtml}})},_u="/benchmarks",Au=()=>{const d=aa[_u];return l.jsx("main",{id:"main-content",dangerouslySetInnerHTML:{__html:d.mainHtml}})},Iu="/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty",Lu=({routePath:d})=>{const w=aa[d]||aa[Iu];return l.jsx("main",{id:"main-content",dangerouslySetInnerHTML:{__html:w.mainHtml}},d)},Nu="/privacy",Ru=()=>{const d=aa[Nu];return l.jsx("main",{id:"main-content",className:"page-privacy",dangerouslySetInnerHTML:{__html:d.mainHtml}})},zi="/",ys="/",bd="";function Yi(d){const w=d.trim();if(!w||w===ys)return zi;const h=w.startsWith(ys)?w:`${ys}${w}`,Q=h.length>1&&h.endsWith(ys)?h.slice(0,-1):h;return aa[Q]?Q:zi}function xu(d,w,h){const Q=`${w}${h}`,j=new URL(d,Q);if(j.origin!==w)return{route:bd,hash:bd,isSpaRoute:!1};const x=j.pathname,X=x.length>1&&x.endsWith(ys)?x.slice(0,-1):x,de=!!aa[X];return{route:de?X:zi,hash:j.hash,isSpaRoute:de}}const Pu="/",Mu="/architectures",Du="/ontology",Ou="/benchmarks",Bu="/privacy",wd="popstate",Sd="click",Fu="a[href]",Ku="href",ju="target",Gu="download",Hu="data-open-drawer",Wu="_blank",Uu="smooth",Cd="#",kd=0;function Td(d){if(d&&d.startsWith(Cd)&&d.length>1){const w=d.slice(1),h=document.getElementById(w);if(h){h.scrollIntoView({behavior:Uu});return}}window.scrollTo(kd,kd)}const qu=({initialPath:d})=>{const[w,h]=lt.useState(()=>Yi(d));lt.useEffect(()=>{const x=aa[w];x&&typeof document<"u"&&(document.title=x.title)},[w]),lt.useEffect(()=>{const x=()=>{const de=Yi(window.location.pathname);h(de);const te=window.location.hash;requestAnimationFrame(()=>Td(te))},X=de=>{if(de.defaultPrevented||de.button!==0||de.metaKey||de.ctrlKey||de.shiftKey||de.altKey)return;const te=de.target;if(!te||typeof te.closest!="function")return;const ve=te.closest(Fu);if(!ve||ve.getAttribute(ju)===Wu||ve.hasAttribute(Gu)||ve.hasAttribute(Hu))return;const ke=ve.getAttribute(Ku);if(!ke||ke.startsWith(Cd))return;const he=xu(ke,window.location.origin,window.location.pathname);if(!he.isSpaRoute)return;de.preventDefault();const Ce=`${he.route}${he.hash}`;window.history.pushState(null,"",Ce),h(he.route),requestAnimationFrame(()=>Td(he.hash))};return window.addEventListener(wd,x),document.addEventListener(Sd,X),()=>{window.removeEventListener(wd,x),document.removeEventListener(Sd,X)}},[]);let Q,j=null;return w===Pu?(Q=l.jsx(yu,{}),j=l.jsx(fu,{})):w===Mu?Q=l.jsx(ku,{}):w===Du?Q=l.jsx(Eu,{}):w===Ou?Q=l.jsx(Au,{}):w===Bu?Q=l.jsx(Ru,{}):Q=l.jsx(Lu,{routePath:w}),l.jsx(mu,{currentRoute:w,afterFooter:j,children:Q})},Qu="root",zu="/",gs=document.getElementById(Qu);if(gs){const d=window.location.pathname,w=Yi(d),h=l.jsx(qu,{initialPath:d});gs.hasChildNodes()&&w===zu?gd.hydrateRoot(gs,h):(gs.innerHTML="",gd.createRoot(gs).render(h))}
