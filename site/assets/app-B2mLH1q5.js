(function(){const D=document.createElement("link").relList;if(D&&D.supports&&D.supports("modulepreload"))return;for(const G of document.querySelectorAll('link[rel="modulepreload"]'))V(G);new MutationObserver(G=>{for(const re of G)if(re.type==="childList")for(const be of re.addedNodes)be.tagName==="LINK"&&be.rel==="modulepreload"&&V(be)}).observe(document,{childList:!0,subtree:!0});function g(G){const re={};return G.integrity&&(re.integrity=G.integrity),G.referrerPolicy&&(re.referrerPolicy=G.referrerPolicy),G.crossOrigin==="use-credentials"?re.credentials="include":G.crossOrigin==="anonymous"?re.credentials="omit":re.credentials="same-origin",re}function V(G){if(G.ep)return;G.ep=!0;const re=g(G);fetch(G.href,re)}})();var Ki={exports:{}},ms={},ji={exports:{}},Te={};/**
 * @license React
 * react.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var ad;function Wp(){if(ad)return Te;ad=1;var w=Symbol.for("react.element"),D=Symbol.for("react.portal"),g=Symbol.for("react.fragment"),V=Symbol.for("react.strict_mode"),G=Symbol.for("react.profiler"),re=Symbol.for("react.provider"),be=Symbol.for("react.context"),ye=Symbol.for("react.forward_ref"),Z=Symbol.for("react.suspense"),de=Symbol.for("react.memo"),we=Symbol.for("react.lazy"),b=Symbol.iterator;function M(m){return m===null||typeof m!="object"?null:(m=b&&m[b]||m["@@iterator"],typeof m=="function"?m:null)}var j={isMounted:function(){return!1},enqueueForceUpdate:function(){},enqueueReplaceState:function(){},enqueueSetState:function(){}},_=Object.assign,Ie={};function Ce(m,C,ee){this.props=m,this.context=C,this.refs=Ie,this.updater=ee||j}Ce.prototype.isReactComponent={},Ce.prototype.setState=function(m,C){if(typeof m!="object"&&typeof m!="function"&&m!=null)throw Error("setState(...): takes an object of state variables to update or a function which returns an object of state variables.");this.updater.enqueueSetState(this,m,C,"setState")},Ce.prototype.forceUpdate=function(m){this.updater.enqueueForceUpdate(this,m,"forceUpdate")};function it(){}it.prototype=Ce.prototype;function yt(m,C,ee){this.props=m,this.context=C,this.refs=Ie,this.updater=ee||j}var ft=yt.prototype=new it;ft.constructor=yt,_(ft,Ce.prototype),ft.isPureReactComponent=!0;var A=Array.isArray,ne=Object.prototype.hasOwnProperty,$={current:null},Ee={key:!0,ref:!0,__self:!0,__source:!0};function Xe(m,C,ee){var me,te={},ke=null,fe=null;if(C!=null)for(me in C.ref!==void 0&&(fe=C.ref),C.key!==void 0&&(ke=""+C.key),C)ne.call(C,me)&&!Ee.hasOwnProperty(me)&&(te[me]=C[me]);var _e=arguments.length-2;if(_e===1)te.children=ee;else if(1<_e){for(var He=Array(_e),Nt=0;Nt<_e;Nt++)He[Nt]=arguments[Nt+2];te.children=He}if(m&&m.defaultProps)for(me in _e=m.defaultProps,_e)te[me]===void 0&&(te[me]=_e[me]);return{$$typeof:w,type:m,key:ke,ref:fe,props:te,_owner:$.current}}function Me(m,C){return{$$typeof:w,type:m.type,key:C,ref:m.ref,props:m.props,_owner:m._owner}}function vt(m){return typeof m=="object"&&m!==null&&m.$$typeof===w}function Lt(m){var C={"=":"=0",":":"=2"};return"$"+m.replace(/[=:]/g,function(ee){return C[ee]})}var ln=/\/+/g;function ue(m,C){return typeof m=="object"&&m!==null&&m.key!=null?Lt(""+m.key):C.toString(36)}function tn(m,C,ee,me,te){var ke=typeof m;(ke==="undefined"||ke==="boolean")&&(m=null);var fe=!1;if(m===null)fe=!0;else switch(ke){case"string":case"number":fe=!0;break;case"object":switch(m.$$typeof){case w:case D:fe=!0}}if(fe)return fe=m,te=te(fe),m=me===""?"."+ue(fe,0):me,A(te)?(ee="",m!=null&&(ee=m.replace(ln,"$&/")+"/"),tn(te,C,ee,"",function(Nt){return Nt})):te!=null&&(vt(te)&&(te=Me(te,ee+(!te.key||fe&&fe.key===te.key?"":(""+te.key).replace(ln,"$&/")+"/")+m)),C.push(te)),1;if(fe=0,me=me===""?".":me+":",A(m))for(var _e=0;_e<m.length;_e++){ke=m[_e];var He=me+ue(ke,_e);fe+=tn(ke,C,ee,He,te)}else if(He=M(m),typeof He=="function")for(m=He.call(m),_e=0;!(ke=m.next()).done;)ke=ke.value,He=me+ue(ke,_e++),fe+=tn(ke,C,ee,He,te);else if(ke==="object")throw C=String(m),Error("Objects are not valid as a React child (found: "+(C==="[object Object]"?"object with keys {"+Object.keys(m).join(", ")+"}":C)+"). If you meant to render a collection of children, use an array instead.");return fe}function ut(m,C,ee){if(m==null)return m;var me=[],te=0;return tn(m,me,"","",function(ke){return C.call(ee,ke,te++)}),me}function tt(m){if(m._status===-1){var C=m._result;C=C(),C.then(function(ee){(m._status===0||m._status===-1)&&(m._status=1,m._result=ee)},function(ee){(m._status===0||m._status===-1)&&(m._status=2,m._result=ee)}),m._status===-1&&(m._status=0,m._result=C)}if(m._status===1)return m._result.default;throw m._result}var qe={current:null},B={transition:null},se={ReactCurrentDispatcher:qe,ReactCurrentBatchConfig:B,ReactCurrentOwner:$};function x(){throw Error("act(...) is not supported in production builds of React.")}return Te.Children={map:ut,forEach:function(m,C,ee){ut(m,function(){C.apply(this,arguments)},ee)},count:function(m){var C=0;return ut(m,function(){C++}),C},toArray:function(m){return ut(m,function(C){return C})||[]},only:function(m){if(!vt(m))throw Error("React.Children.only expected to receive a single React element child.");return m}},Te.Component=Ce,Te.Fragment=g,Te.Profiler=G,Te.PureComponent=yt,Te.StrictMode=V,Te.Suspense=Z,Te.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED=se,Te.act=x,Te.cloneElement=function(m,C,ee){if(m==null)throw Error("React.cloneElement(...): The argument must be a React element, but you passed "+m+".");var me=_({},m.props),te=m.key,ke=m.ref,fe=m._owner;if(C!=null){if(C.ref!==void 0&&(ke=C.ref,fe=$.current),C.key!==void 0&&(te=""+C.key),m.type&&m.type.defaultProps)var _e=m.type.defaultProps;for(He in C)ne.call(C,He)&&!Ee.hasOwnProperty(He)&&(me[He]=C[He]===void 0&&_e!==void 0?_e[He]:C[He])}var He=arguments.length-2;if(He===1)me.children=ee;else if(1<He){_e=Array(He);for(var Nt=0;Nt<He;Nt++)_e[Nt]=arguments[Nt+2];me.children=_e}return{$$typeof:w,type:m.type,key:te,ref:ke,props:me,_owner:fe}},Te.createContext=function(m){return m={$$typeof:be,_currentValue:m,_currentValue2:m,_threadCount:0,Provider:null,Consumer:null,_defaultValue:null,_globalName:null},m.Provider={$$typeof:re,_context:m},m.Consumer=m},Te.createElement=Xe,Te.createFactory=function(m){var C=Xe.bind(null,m);return C.type=m,C},Te.createRef=function(){return{current:null}},Te.forwardRef=function(m){return{$$typeof:ye,render:m}},Te.isValidElement=vt,Te.lazy=function(m){return{$$typeof:we,_payload:{_status:-1,_result:m},_init:tt}},Te.memo=function(m,C){return{$$typeof:de,type:m,compare:C===void 0?null:C}},Te.startTransition=function(m){var C=B.transition;B.transition={};try{m()}finally{B.transition=C}},Te.unstable_act=x,Te.useCallback=function(m,C){return qe.current.useCallback(m,C)},Te.useContext=function(m){return qe.current.useContext(m)},Te.useDebugValue=function(){},Te.useDeferredValue=function(m){return qe.current.useDeferredValue(m)},Te.useEffect=function(m,C){return qe.current.useEffect(m,C)},Te.useId=function(){return qe.current.useId()},Te.useImperativeHandle=function(m,C,ee){return qe.current.useImperativeHandle(m,C,ee)},Te.useInsertionEffect=function(m,C){return qe.current.useInsertionEffect(m,C)},Te.useLayoutEffect=function(m,C){return qe.current.useLayoutEffect(m,C)},Te.useMemo=function(m,C){return qe.current.useMemo(m,C)},Te.useReducer=function(m,C,ee){return qe.current.useReducer(m,C,ee)},Te.useRef=function(m){return qe.current.useRef(m)},Te.useState=function(m){return qe.current.useState(m)},Te.useSyncExternalStore=function(m,C,ee){return qe.current.useSyncExternalStore(m,C,ee)},Te.useTransition=function(){return qe.current.useTransition()},Te.version="18.3.1",Te}var rd;function Qi(){return rd||(rd=1,ji.exports=Wp()),ji.exports}/**
 * @license React
 * react-jsx-runtime.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var sd;function Gp(){if(sd)return ms;sd=1;var w=Qi(),D=Symbol.for("react.element"),g=Symbol.for("react.fragment"),V=Object.prototype.hasOwnProperty,G=w.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED.ReactCurrentOwner,re={key:!0,ref:!0,__self:!0,__source:!0};function be(ye,Z,de){var we,b={},M=null,j=null;de!==void 0&&(M=""+de),Z.key!==void 0&&(M=""+Z.key),Z.ref!==void 0&&(j=Z.ref);for(we in Z)V.call(Z,we)&&!re.hasOwnProperty(we)&&(b[we]=Z[we]);if(ye&&ye.defaultProps)for(we in Z=ye.defaultProps,Z)b[we]===void 0&&(b[we]=Z[we]);return{$$typeof:D,type:ye,key:M,ref:j,props:b,_owner:G.current}}return ms.Fragment=g,ms.jsx=be,ms.jsxs=be,ms}var od;function Up(){return od||(od=1,Ki.exports=Gp()),Ki.exports}var R=Up(),go={},Hi={exports:{}},en={},Wi={exports:{}},Gi={};/**
 * @license React
 * scheduler.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var id;function qp(){return id||(id=1,(function(w){function D(B,se){var x=B.length;B.push(se);e:for(;0<x;){var m=x-1>>>1,C=B[m];if(0<G(C,se))B[m]=se,B[x]=C,x=m;else break e}}function g(B){return B.length===0?null:B[0]}function V(B){if(B.length===0)return null;var se=B[0],x=B.pop();if(x!==se){B[0]=x;e:for(var m=0,C=B.length,ee=C>>>1;m<ee;){var me=2*(m+1)-1,te=B[me],ke=me+1,fe=B[ke];if(0>G(te,x))ke<C&&0>G(fe,te)?(B[m]=fe,B[ke]=x,m=ke):(B[m]=te,B[me]=x,m=me);else if(ke<C&&0>G(fe,x))B[m]=fe,B[ke]=x,m=ke;else break e}}return se}function G(B,se){var x=B.sortIndex-se.sortIndex;return x!==0?x:B.id-se.id}if(typeof performance=="object"&&typeof performance.now=="function"){var re=performance;w.unstable_now=function(){return re.now()}}else{var be=Date,ye=be.now();w.unstable_now=function(){return be.now()-ye}}var Z=[],de=[],we=1,b=null,M=3,j=!1,_=!1,Ie=!1,Ce=typeof setTimeout=="function"?setTimeout:null,it=typeof clearTimeout=="function"?clearTimeout:null,yt=typeof setImmediate<"u"?setImmediate:null;typeof navigator<"u"&&navigator.scheduling!==void 0&&navigator.scheduling.isInputPending!==void 0&&navigator.scheduling.isInputPending.bind(navigator.scheduling);function ft(B){for(var se=g(de);se!==null;){if(se.callback===null)V(de);else if(se.startTime<=B)V(de),se.sortIndex=se.expirationTime,D(Z,se);else break;se=g(de)}}function A(B){if(Ie=!1,ft(B),!_)if(g(Z)!==null)_=!0,tt(ne);else{var se=g(de);se!==null&&qe(A,se.startTime-B)}}function ne(B,se){_=!1,Ie&&(Ie=!1,it(Xe),Xe=-1),j=!0;var x=M;try{for(ft(se),b=g(Z);b!==null&&(!(b.expirationTime>se)||B&&!Lt());){var m=b.callback;if(typeof m=="function"){b.callback=null,M=b.priorityLevel;var C=m(b.expirationTime<=se);se=w.unstable_now(),typeof C=="function"?b.callback=C:b===g(Z)&&V(Z),ft(se)}else V(Z);b=g(Z)}if(b!==null)var ee=!0;else{var me=g(de);me!==null&&qe(A,me.startTime-se),ee=!1}return ee}finally{b=null,M=x,j=!1}}var $=!1,Ee=null,Xe=-1,Me=5,vt=-1;function Lt(){return!(w.unstable_now()-vt<Me)}function ln(){if(Ee!==null){var B=w.unstable_now();vt=B;var se=!0;try{se=Ee(!0,B)}finally{se?ue():($=!1,Ee=null)}}else $=!1}var ue;if(typeof yt=="function")ue=function(){yt(ln)};else if(typeof MessageChannel<"u"){var tn=new MessageChannel,ut=tn.port2;tn.port1.onmessage=ln,ue=function(){ut.postMessage(null)}}else ue=function(){Ce(ln,0)};function tt(B){Ee=B,$||($=!0,ue())}function qe(B,se){Xe=Ce(function(){B(w.unstable_now())},se)}w.unstable_IdlePriority=5,w.unstable_ImmediatePriority=1,w.unstable_LowPriority=4,w.unstable_NormalPriority=3,w.unstable_Profiling=null,w.unstable_UserBlockingPriority=2,w.unstable_cancelCallback=function(B){B.callback=null},w.unstable_continueExecution=function(){_||j||(_=!0,tt(ne))},w.unstable_forceFrameRate=function(B){0>B||125<B?console.error("forceFrameRate takes a positive int between 0 and 125, forcing frame rates higher than 125 fps is not supported"):Me=0<B?Math.floor(1e3/B):5},w.unstable_getCurrentPriorityLevel=function(){return M},w.unstable_getFirstCallbackNode=function(){return g(Z)},w.unstable_next=function(B){switch(M){case 1:case 2:case 3:var se=3;break;default:se=M}var x=M;M=se;try{return B()}finally{M=x}},w.unstable_pauseExecution=function(){},w.unstable_requestPaint=function(){},w.unstable_runWithPriority=function(B,se){switch(B){case 1:case 2:case 3:case 4:case 5:break;default:B=3}var x=M;M=B;try{return se()}finally{M=x}},w.unstable_scheduleCallback=function(B,se,x){var m=w.unstable_now();switch(typeof x=="object"&&x!==null?(x=x.delay,x=typeof x=="number"&&0<x?m+x:m):x=m,B){case 1:var C=-1;break;case 2:C=250;break;case 5:C=1073741823;break;case 4:C=1e4;break;default:C=5e3}return C=x+C,B={id:we++,callback:se,priorityLevel:B,startTime:x,expirationTime:C,sortIndex:-1},x>m?(B.sortIndex=x,D(de,B),g(Z)===null&&B===g(de)&&(Ie?(it(Xe),Xe=-1):Ie=!0,qe(A,x-m))):(B.sortIndex=C,D(Z,B),_||j||(_=!0,tt(ne))),B},w.unstable_shouldYield=Lt,w.unstable_wrapCallback=function(B){var se=M;return function(){var x=M;M=se;try{return B.apply(this,arguments)}finally{M=x}}}})(Gi)),Gi}var ld;function Qp(){return ld||(ld=1,Wi.exports=qp()),Wi.exports}/**
 * @license React
 * react-dom.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var cd;function zp(){if(cd)return en;cd=1;var w=Qi(),D=Qp();function g(e){for(var t="https://reactjs.org/docs/error-decoder.html?invariant="+e,n=1;n<arguments.length;n++)t+="&args[]="+encodeURIComponent(arguments[n]);return"Minified React error #"+e+"; visit "+t+" for the full message or use the non-minified dev environment for full errors and additional helpful warnings."}var V=new Set,G={};function re(e,t){be(e,t),be(e+"Capture",t)}function be(e,t){for(G[e]=t,e=0;e<t.length;e++)V.add(t[e])}var ye=!(typeof window>"u"||typeof window.document>"u"||typeof window.document.createElement>"u"),Z=Object.prototype.hasOwnProperty,de=/^[:A-Z_a-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD][:A-Z_a-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD\-.0-9\u00B7\u0300-\u036F\u203F-\u2040]*$/,we={},b={};function M(e){return Z.call(b,e)?!0:Z.call(we,e)?!1:de.test(e)?b[e]=!0:(we[e]=!0,!1)}function j(e,t,n,a){if(n!==null&&n.type===0)return!1;switch(typeof t){case"function":case"symbol":return!0;case"boolean":return a?!1:n!==null?!n.acceptsBooleans:(e=e.toLowerCase().slice(0,5),e!=="data-"&&e!=="aria-");default:return!1}}function _(e,t,n,a){if(t===null||typeof t>"u"||j(e,t,n,a))return!0;if(a)return!1;if(n!==null)switch(n.type){case 3:return!t;case 4:return t===!1;case 5:return isNaN(t);case 6:return isNaN(t)||1>t}return!1}function Ie(e,t,n,a,r,s,i){this.acceptsBooleans=t===2||t===3||t===4,this.attributeName=a,this.attributeNamespace=r,this.mustUseProperty=n,this.propertyName=e,this.type=t,this.sanitizeURL=s,this.removeEmptyString=i}var Ce={};"children dangerouslySetInnerHTML defaultValue defaultChecked innerHTML suppressContentEditableWarning suppressHydrationWarning style".split(" ").forEach(function(e){Ce[e]=new Ie(e,0,!1,e,null,!1,!1)}),[["acceptCharset","accept-charset"],["className","class"],["htmlFor","for"],["httpEquiv","http-equiv"]].forEach(function(e){var t=e[0];Ce[t]=new Ie(t,1,!1,e[1],null,!1,!1)}),["contentEditable","draggable","spellCheck","value"].forEach(function(e){Ce[e]=new Ie(e,2,!1,e.toLowerCase(),null,!1,!1)}),["autoReverse","externalResourcesRequired","focusable","preserveAlpha"].forEach(function(e){Ce[e]=new Ie(e,2,!1,e,null,!1,!1)}),"allowFullScreen async autoFocus autoPlay controls default defer disabled disablePictureInPicture disableRemotePlayback formNoValidate hidden loop noModule noValidate open playsInline readOnly required reversed scoped seamless itemScope".split(" ").forEach(function(e){Ce[e]=new Ie(e,3,!1,e.toLowerCase(),null,!1,!1)}),["checked","multiple","muted","selected"].forEach(function(e){Ce[e]=new Ie(e,3,!0,e,null,!1,!1)}),["capture","download"].forEach(function(e){Ce[e]=new Ie(e,4,!1,e,null,!1,!1)}),["cols","rows","size","span"].forEach(function(e){Ce[e]=new Ie(e,6,!1,e,null,!1,!1)}),["rowSpan","start"].forEach(function(e){Ce[e]=new Ie(e,5,!1,e.toLowerCase(),null,!1,!1)});var it=/[\-:]([a-z])/g;function yt(e){return e[1].toUpperCase()}"accent-height alignment-baseline arabic-form baseline-shift cap-height clip-path clip-rule color-interpolation color-interpolation-filters color-profile color-rendering dominant-baseline enable-background fill-opacity fill-rule flood-color flood-opacity font-family font-size font-size-adjust font-stretch font-style font-variant font-weight glyph-name glyph-orientation-horizontal glyph-orientation-vertical horiz-adv-x horiz-origin-x image-rendering letter-spacing lighting-color marker-end marker-mid marker-start overline-position overline-thickness paint-order panose-1 pointer-events rendering-intent shape-rendering stop-color stop-opacity strikethrough-position strikethrough-thickness stroke-dasharray stroke-dashoffset stroke-linecap stroke-linejoin stroke-miterlimit stroke-opacity stroke-width text-anchor text-decoration text-rendering underline-position underline-thickness unicode-bidi unicode-range units-per-em v-alphabetic v-hanging v-ideographic v-mathematical vector-effect vert-adv-y vert-origin-x vert-origin-y word-spacing writing-mode xmlns:xlink x-height".split(" ").forEach(function(e){var t=e.replace(it,yt);Ce[t]=new Ie(t,1,!1,e,null,!1,!1)}),"xlink:actuate xlink:arcrole xlink:role xlink:show xlink:title xlink:type".split(" ").forEach(function(e){var t=e.replace(it,yt);Ce[t]=new Ie(t,1,!1,e,"http://www.w3.org/1999/xlink",!1,!1)}),["xml:base","xml:lang","xml:space"].forEach(function(e){var t=e.replace(it,yt);Ce[t]=new Ie(t,1,!1,e,"http://www.w3.org/XML/1998/namespace",!1,!1)}),["tabIndex","crossOrigin"].forEach(function(e){Ce[e]=new Ie(e,1,!1,e.toLowerCase(),null,!1,!1)}),Ce.xlinkHref=new Ie("xlinkHref",1,!1,"xlink:href","http://www.w3.org/1999/xlink",!0,!1),["src","href","action","formAction"].forEach(function(e){Ce[e]=new Ie(e,1,!1,e.toLowerCase(),null,!0,!0)});function ft(e,t,n,a){var r=Ce.hasOwnProperty(t)?Ce[t]:null;(r!==null?r.type!==0:a||!(2<t.length)||t[0]!=="o"&&t[0]!=="O"||t[1]!=="n"&&t[1]!=="N")&&(_(t,n,r,a)&&(n=null),a||r===null?M(t)&&(n===null?e.removeAttribute(t):e.setAttribute(t,""+n)):r.mustUseProperty?e[r.propertyName]=n===null?r.type===3?!1:"":n:(t=r.attributeName,a=r.attributeNamespace,n===null?e.removeAttribute(t):(r=r.type,n=r===3||r===4&&n===!0?"":""+n,a?e.setAttributeNS(a,t,n):e.setAttribute(t,n))))}var A=w.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED,ne=Symbol.for("react.element"),$=Symbol.for("react.portal"),Ee=Symbol.for("react.fragment"),Xe=Symbol.for("react.strict_mode"),Me=Symbol.for("react.profiler"),vt=Symbol.for("react.provider"),Lt=Symbol.for("react.context"),ln=Symbol.for("react.forward_ref"),ue=Symbol.for("react.suspense"),tn=Symbol.for("react.suspense_list"),ut=Symbol.for("react.memo"),tt=Symbol.for("react.lazy"),qe=Symbol.for("react.offscreen"),B=Symbol.iterator;function se(e){return e===null||typeof e!="object"?null:(e=B&&e[B]||e["@@iterator"],typeof e=="function"?e:null)}var x=Object.assign,m;function C(e){if(m===void 0)try{throw Error()}catch(n){var t=n.stack.trim().match(/\n( *(at )?)/);m=t&&t[1]||""}return`
`+m+e}var ee=!1;function me(e,t){if(!e||ee)return"";ee=!0;var n=Error.prepareStackTrace;Error.prepareStackTrace=void 0;try{if(t)if(t=function(){throw Error()},Object.defineProperty(t.prototype,"props",{set:function(){throw Error()}}),typeof Reflect=="object"&&Reflect.construct){try{Reflect.construct(t,[])}catch(f){var a=f}Reflect.construct(e,[],t)}else{try{t.call()}catch(f){a=f}e.call(t.prototype)}else{try{throw Error()}catch(f){a=f}e()}}catch(f){if(f&&a&&typeof f.stack=="string"){for(var r=f.stack.split(`
`),s=a.stack.split(`
`),i=r.length-1,l=s.length-1;1<=i&&0<=l&&r[i]!==s[l];)l--;for(;1<=i&&0<=l;i--,l--)if(r[i]!==s[l]){if(i!==1||l!==1)do if(i--,l--,0>l||r[i]!==s[l]){var d=`
`+r[i].replace(" at new "," at ");return e.displayName&&d.includes("<anonymous>")&&(d=d.replace("<anonymous>",e.displayName)),d}while(1<=i&&0<=l);break}}}finally{ee=!1,Error.prepareStackTrace=n}return(e=e?e.displayName||e.name:"")?C(e):""}function te(e){switch(e.tag){case 5:return C(e.type);case 16:return C("Lazy");case 13:return C("Suspense");case 19:return C("SuspenseList");case 0:case 2:case 15:return e=me(e.type,!1),e;case 11:return e=me(e.type.render,!1),e;case 1:return e=me(e.type,!0),e;default:return""}}function ke(e){if(e==null)return null;if(typeof e=="function")return e.displayName||e.name||null;if(typeof e=="string")return e;switch(e){case Ee:return"Fragment";case $:return"Portal";case Me:return"Profiler";case Xe:return"StrictMode";case ue:return"Suspense";case tn:return"SuspenseList"}if(typeof e=="object")switch(e.$$typeof){case Lt:return(e.displayName||"Context")+".Consumer";case vt:return(e._context.displayName||"Context")+".Provider";case ln:var t=e.render;return e=e.displayName,e||(e=t.displayName||t.name||"",e=e!==""?"ForwardRef("+e+")":"ForwardRef"),e;case ut:return t=e.displayName||null,t!==null?t:ke(e.type)||"Memo";case tt:t=e._payload,e=e._init;try{return ke(e(t))}catch{}}return null}function fe(e){var t=e.type;switch(e.tag){case 24:return"Cache";case 9:return(t.displayName||"Context")+".Consumer";case 10:return(t._context.displayName||"Context")+".Provider";case 18:return"DehydratedFragment";case 11:return e=t.render,e=e.displayName||e.name||"",t.displayName||(e!==""?"ForwardRef("+e+")":"ForwardRef");case 7:return"Fragment";case 5:return t;case 4:return"Portal";case 3:return"Root";case 6:return"Text";case 16:return ke(t);case 8:return t===Xe?"StrictMode":"Mode";case 22:return"Offscreen";case 12:return"Profiler";case 21:return"Scope";case 13:return"Suspense";case 19:return"SuspenseList";case 25:return"TracingMarker";case 1:case 0:case 17:case 2:case 14:case 15:if(typeof t=="function")return t.displayName||t.name||null;if(typeof t=="string")return t}return null}function _e(e){switch(typeof e){case"boolean":case"number":case"string":case"undefined":return e;case"object":return e;default:return""}}function He(e){var t=e.type;return(e=e.nodeName)&&e.toLowerCase()==="input"&&(t==="checkbox"||t==="radio")}function Nt(e){var t=He(e)?"checked":"value",n=Object.getOwnPropertyDescriptor(e.constructor.prototype,t),a=""+e[t];if(!e.hasOwnProperty(t)&&typeof n<"u"&&typeof n.get=="function"&&typeof n.set=="function"){var r=n.get,s=n.set;return Object.defineProperty(e,t,{configurable:!0,get:function(){return r.call(this)},set:function(i){a=""+i,s.call(this,i)}}),Object.defineProperty(e,t,{enumerable:n.enumerable}),{getValue:function(){return a},setValue:function(i){a=""+i},stopTracking:function(){e._valueTracker=null,delete e[t]}}}}function Hn(e){e._valueTracker||(e._valueTracker=Nt(e))}function Rr(e){if(!e)return!1;var t=e._valueTracker;if(!t)return!0;var n=t.getValue(),a="";return e&&(a=He(e)?e.checked?"true":"false":e.value),e=a,e!==n?(t.setValue(e),!0):!1}function Wn(e){if(e=e||(typeof document<"u"?document:void 0),typeof e>"u")return null;try{return e.activeElement||e.body}catch{return e.body}}function Pr(e,t){var n=t.checked;return x({},t,{defaultChecked:void 0,defaultValue:void 0,value:void 0,checked:n??e._wrapperState.initialChecked})}function ys(e,t){var n=t.defaultValue==null?"":t.defaultValue,a=t.checked!=null?t.checked:t.defaultChecked;n=_e(t.value!=null?t.value:n),e._wrapperState={initialChecked:a,initialValue:n,controlled:t.type==="checkbox"||t.type==="radio"?t.checked!=null:t.value!=null}}function Xa(e,t){t=t.checked,t!=null&&ft(e,"checked",t,!1)}function _a(e,t){Xa(e,t);var n=_e(t.value),a=t.type;if(n!=null)a==="number"?(n===0&&e.value===""||e.value!=n)&&(e.value=""+n):e.value!==""+n&&(e.value=""+n);else if(a==="submit"||a==="reset"){e.removeAttribute("value");return}t.hasOwnProperty("value")?xr(e,t.type,n):t.hasOwnProperty("defaultValue")&&xr(e,t.type,_e(t.defaultValue)),t.checked==null&&t.defaultChecked!=null&&(e.defaultChecked=!!t.defaultChecked)}function Aa(e,t,n){if(t.hasOwnProperty("value")||t.hasOwnProperty("defaultValue")){var a=t.type;if(!(a!=="submit"&&a!=="reset"||t.value!==void 0&&t.value!==null))return;t=""+e._wrapperState.initialValue,n||t===e.value||(e.value=t),e.defaultValue=t}n=e.name,n!==""&&(e.name=""),e.defaultChecked=!!e._wrapperState.initialChecked,n!==""&&(e.name=n)}function xr(e,t,n){(t!=="number"||Wn(e.ownerDocument)!==e)&&(n==null?e.defaultValue=""+e._wrapperState.initialValue:e.defaultValue!==""+n&&(e.defaultValue=""+n))}var Ia=Array.isArray;function aa(e,t,n,a){if(e=e.options,t){t={};for(var r=0;r<n.length;r++)t["$"+n[r]]=!0;for(n=0;n<e.length;n++)r=t.hasOwnProperty("$"+e[n].value),e[n].selected!==r&&(e[n].selected=r),r&&a&&(e[n].defaultSelected=!0)}else{for(n=""+_e(n),t=null,r=0;r<e.length;r++){if(e[r].value===n){e[r].selected=!0,a&&(e[r].defaultSelected=!0);return}t!==null||e[r].disabled||(t=e[r])}t!==null&&(t.selected=!0)}}function Et(e,t){if(t.dangerouslySetInnerHTML!=null)throw Error(g(91));return x({},t,{value:void 0,defaultValue:void 0,children:""+e._wrapperState.initialValue})}function cn(e,t){var n=t.value;if(n==null){if(n=t.children,t=t.defaultValue,n!=null){if(t!=null)throw Error(g(92));if(Ia(n)){if(1<n.length)throw Error(g(93));n=n[0]}t=n}t==null&&(t=""),n=t}e._wrapperState={initialValue:_e(n)}}function fn(e,t){var n=_e(t.value),a=_e(t.defaultValue);n!=null&&(n=""+n,n!==e.value&&(e.value=n),t.defaultValue==null&&e.defaultValue!==n&&(e.defaultValue=n)),a!=null&&(e.defaultValue=""+a)}function ce(e){var t=e.textContent;t===e._wrapperState.initialValue&&t!==""&&t!==null&&(e.value=t)}function Za(e){switch(e){case"svg":return"http://www.w3.org/2000/svg";case"math":return"http://www.w3.org/1998/Math/MathML";default:return"http://www.w3.org/1999/xhtml"}}function nn(e,t){return e==null||e==="http://www.w3.org/1999/xhtml"?Za(t):e==="http://www.w3.org/2000/svg"&&t==="foreignObject"?"http://www.w3.org/1999/xhtml":e}var La,Mr=(function(e){return typeof MSApp<"u"&&MSApp.execUnsafeLocalFunction?function(t,n,a,r){MSApp.execUnsafeLocalFunction(function(){return e(t,n,a,r)})}:e})(function(e,t){if(e.namespaceURI!=="http://www.w3.org/2000/svg"||"innerHTML"in e)e.innerHTML=t;else{for(La=La||document.createElement("div"),La.innerHTML="<svg>"+t.valueOf().toString()+"</svg>",t=La.firstChild;e.firstChild;)e.removeChild(e.firstChild);for(;t.firstChild;)e.appendChild(t.firstChild)}});function Fe(e,t){if(t){var n=e.firstChild;if(n&&n===e.lastChild&&n.nodeType===3){n.nodeValue=t;return}}e.textContent=t}var jt={animationIterationCount:!0,aspectRatio:!0,borderImageOutset:!0,borderImageSlice:!0,borderImageWidth:!0,boxFlex:!0,boxFlexGroup:!0,boxOrdinalGroup:!0,columnCount:!0,columns:!0,flex:!0,flexGrow:!0,flexPositive:!0,flexShrink:!0,flexNegative:!0,flexOrder:!0,gridArea:!0,gridRow:!0,gridRowEnd:!0,gridRowSpan:!0,gridRowStart:!0,gridColumn:!0,gridColumnEnd:!0,gridColumnSpan:!0,gridColumnStart:!0,fontWeight:!0,lineClamp:!0,lineHeight:!0,opacity:!0,order:!0,orphans:!0,tabSize:!0,widows:!0,zIndex:!0,zoom:!0,fillOpacity:!0,floodOpacity:!0,stopOpacity:!0,strokeDasharray:!0,strokeDashoffset:!0,strokeMiterlimit:!0,strokeOpacity:!0,strokeWidth:!0},yo=["Webkit","ms","Moz","O"];Object.keys(jt).forEach(function(e){yo.forEach(function(t){t=t+e.charAt(0).toUpperCase()+e.substring(1),jt[t]=jt[e]})});function Dr(e,t,n){return t==null||typeof t=="boolean"||t===""?"":n||typeof t!="number"||t===0||jt.hasOwnProperty(e)&&jt[e]?(""+t).trim():t+"px"}function fs(e,t){e=e.style;for(var n in t)if(t.hasOwnProperty(n)){var a=n.indexOf("--")===0,r=Dr(n,t[n],a);n==="float"&&(n="cssFloat"),a?e.setProperty(n,r):e[n]=r}}var Ln=x({menuitem:!0},{area:!0,base:!0,br:!0,col:!0,embed:!0,hr:!0,img:!0,input:!0,keygen:!0,link:!0,meta:!0,param:!0,source:!0,track:!0,wbr:!0});function er(e,t){if(t){if(Ln[e]&&(t.children!=null||t.dangerouslySetInnerHTML!=null))throw Error(g(137,e));if(t.dangerouslySetInnerHTML!=null){if(t.children!=null)throw Error(g(60));if(typeof t.dangerouslySetInnerHTML!="object"||!("__html"in t.dangerouslySetInnerHTML))throw Error(g(61))}if(t.style!=null&&typeof t.style!="object")throw Error(g(62))}}function tr(e,t){if(e.indexOf("-")===-1)return typeof t.is=="string";switch(e){case"annotation-xml":case"color-profile":case"font-face":case"font-face-src":case"font-face-uri":case"font-face-format":case"font-face-name":case"missing-glyph":return!1;default:return!0}}var bt=null;function Or(e){return e=e.target||e.srcElement||window,e.correspondingUseElement&&(e=e.correspondingUseElement),e.nodeType===3?e.parentNode:e}var Br=null,ra=null,Nn=null;function Rn(e){if(e=Xr(e)){if(typeof Br!="function")throw Error(g(280));var t=e.stateNode;t&&(t=Ls(t),Br(e.stateNode,e.type,t))}}function vs(e){ra?Nn?Nn.push(e):Nn=[e]:ra=e}function wt(){if(ra){var e=ra,t=Nn;if(Nn=ra=null,Rn(e),t)for(e=0;e<t.length;e++)Rn(t[e])}}function Fr(e,t){return e(t)}function lt(){}var nr=!1;function Kr(e,t,n){if(nr)return e(t,n);nr=!0;try{return Fr(e,t,n)}finally{nr=!1,(ra!==null||Nn!==null)&&(lt(),wt())}}function sa(e,t){var n=e.stateNode;if(n===null)return null;var a=Ls(n);if(a===null)return null;n=a[t];e:switch(t){case"onClick":case"onClickCapture":case"onDoubleClick":case"onDoubleClickCapture":case"onMouseDown":case"onMouseDownCapture":case"onMouseMove":case"onMouseMoveCapture":case"onMouseUp":case"onMouseUpCapture":case"onMouseEnter":(a=!a.disabled)||(e=e.type,a=!(e==="button"||e==="input"||e==="select"||e==="textarea")),e=!a;break e;default:e=!1}if(e)return null;if(n&&typeof n!="function")throw Error(g(231,t,typeof n));return n}var ar=!1;if(ye)try{var oa={};Object.defineProperty(oa,"passive",{get:function(){ar=!0}}),window.addEventListener("test",oa,oa),window.removeEventListener("test",oa,oa)}catch{ar=!1}function Na(e,t,n,a,r,s,i,l,d){var f=Array.prototype.slice.call(arguments,3);try{t.apply(n,f)}catch(E){this.onError(E)}}var Ht=!1,Pn=null,Ra=!1,Pa=null,xa={onError:function(e){Ht=!0,Pn=e}};function rr(e,t,n,a,r,s,i,l,d){Ht=!1,Pn=null,Na.apply(xa,arguments)}function Ma(e,t,n,a,r,s,i,l,d){if(rr.apply(this,arguments),Ht){if(Ht){var f=Pn;Ht=!1,Pn=null}else throw Error(g(198));Ra||(Ra=!0,Pa=f)}}function dn(e){var t=e,n=e;if(e.alternate)for(;t.return;)t=t.return;else{e=t;do t=e,(t.flags&4098)!==0&&(n=t.return),e=t.return;while(e)}return t.tag===3?n:null}function Da(e){if(e.tag===13){var t=e.memoizedState;if(t===null&&(e=e.alternate,e!==null&&(t=e.memoizedState)),t!==null)return t.dehydrated}return null}function bs(e){if(dn(e)!==e)throw Error(g(188))}function jr(e){var t=e.alternate;if(!t){if(t=dn(e),t===null)throw Error(g(188));return t!==e?null:e}for(var n=e,a=t;;){var r=n.return;if(r===null)break;var s=r.alternate;if(s===null){if(a=r.return,a!==null){n=a;continue}break}if(r.child===s.child){for(s=r.child;s;){if(s===n)return bs(r),e;if(s===a)return bs(r),t;s=s.sibling}throw Error(g(188))}if(n.return!==a.return)n=r,a=s;else{for(var i=!1,l=r.child;l;){if(l===n){i=!0,n=r,a=s;break}if(l===a){i=!0,a=r,n=s;break}l=l.sibling}if(!i){for(l=s.child;l;){if(l===n){i=!0,n=s,a=r;break}if(l===a){i=!0,a=s,n=r;break}l=l.sibling}if(!i)throw Error(g(189))}}if(n.alternate!==a)throw Error(g(190))}if(n.tag!==3)throw Error(g(188));return n.stateNode.current===n?e:t}function ia(e){return e=jr(e),e!==null?o(e):null}function o(e){if(e.tag===5||e.tag===6)return e;for(e=e.child;e!==null;){var t=o(e);if(t!==null)return t;e=e.sibling}return null}var u=D.unstable_scheduleCallback,v=D.unstable_cancelCallback,k=D.unstable_shouldYield,S=D.unstable_requestPaint,c=D.unstable_now,L=D.unstable_getCurrentPriorityLevel,N=D.unstable_ImmediatePriority,H=D.unstable_UserBlockingPriority,O=D.unstable_NormalPriority,Q=D.unstable_LowPriority,F=D.unstable_IdlePriority,K=null,ae=null;function Le(e){if(ae&&typeof ae.onCommitFiberRoot=="function")try{ae.onCommitFiberRoot(K,e,void 0,(e.current.flags&128)===128)}catch{}}var pe=Math.clz32?Math.clz32:Ae,Ne=Math.log,ve=Math.LN2;function Ae(e){return e>>>=0,e===0?32:31-(Ne(e)/ve|0)|0}var Pe=64,oe=4194304;function U(e){switch(e&-e){case 1:return 1;case 2:return 2;case 4:return 4;case 8:return 8;case 16:return 16;case 32:return 32;case 64:case 128:case 256:case 512:case 1024:case 2048:case 4096:case 8192:case 16384:case 32768:case 65536:case 131072:case 262144:case 524288:case 1048576:case 2097152:return e&4194240;case 4194304:case 8388608:case 16777216:case 33554432:case 67108864:return e&130023424;case 134217728:return 134217728;case 268435456:return 268435456;case 536870912:return 536870912;case 1073741824:return 1073741824;default:return e}}function X(e,t){var n=e.pendingLanes;if(n===0)return 0;var a=0,r=e.suspendedLanes,s=e.pingedLanes,i=n&268435455;if(i!==0){var l=i&~r;l!==0?a=U(l):(s&=i,s!==0&&(a=U(s)))}else i=n&~r,i!==0?a=U(i):s!==0&&(a=U(s));if(a===0)return 0;if(t!==0&&t!==a&&(t&r)===0&&(r=a&-a,s=t&-t,r>=s||r===16&&(s&4194240)!==0))return t;if((a&4)!==0&&(a|=n&16),t=e.entangledLanes,t!==0)for(e=e.entanglements,t&=a;0<t;)n=31-pe(t),r=1<<n,a|=e[n],t&=~r;return a}function De(e,t){switch(e){case 1:case 2:case 4:return t+250;case 8:case 16:case 32:case 64:case 128:case 256:case 512:case 1024:case 2048:case 4096:case 8192:case 16384:case 32768:case 65536:case 131072:case 262144:case 524288:case 1048576:case 2097152:return t+5e3;case 4194304:case 8388608:case 16777216:case 33554432:case 67108864:return-1;case 134217728:case 268435456:case 536870912:case 1073741824:return-1;default:return-1}}function Qe(e,t){for(var n=e.suspendedLanes,a=e.pingedLanes,r=e.expirationTimes,s=e.pendingLanes;0<s;){var i=31-pe(s),l=1<<i,d=r[i];d===-1?((l&n)===0||(l&a)!==0)&&(r[i]=De(l,t)):d<=t&&(e.expiredLanes|=l),s&=~l}}function ze(e){return e=e.pendingLanes&-1073741825,e!==0?e:e&1073741824?1073741824:0}function Oe(){var e=Pe;return Pe<<=1,(Pe&4194240)===0&&(Pe=64),e}function Be(e){for(var t=[],n=0;31>n;n++)t.push(e);return t}function We(e,t,n){e.pendingLanes|=t,t!==536870912&&(e.suspendedLanes=0,e.pingedLanes=0),e=e.eventTimes,t=31-pe(t),e[t]=n}function Ye(e,t){var n=e.pendingLanes&~t;e.pendingLanes=t,e.suspendedLanes=0,e.pingedLanes=0,e.expiredLanes&=t,e.mutableReadLanes&=t,e.entangledLanes&=t,t=e.entanglements;var a=e.eventTimes;for(e=e.expirationTimes;0<n;){var r=31-pe(n),s=1<<r;t[r]=0,a[r]=-1,e[r]=-1,n&=~s}}function Ze(e,t){var n=e.entangledLanes|=t;for(e=e.entanglements;n;){var a=31-pe(n),r=1<<a;r&t|e[a]&t&&(e[a]|=t),n&=~r}}var he=0;function Ke(e){return e&=-e,1<e?4<e?(e&268435455)!==0?16:536870912:4:1}var mt,Rt,an,Pt,ct,st=!1,je=[],xt=null,kt=null,Ge=null,vn=new Map,_t=new Map,dt=[],Yt="mousedown mouseup touchcancel touchend touchstart auxclick dblclick pointercancel pointerdown pointerup dragend dragstart drop compositionend compositionstart keydown keypress keyup input textInput copy cut paste click change contextmenu reset submit".split(" ");function Wt(e,t){switch(e){case"focusin":case"focusout":xt=null;break;case"dragenter":case"dragleave":kt=null;break;case"mouseover":case"mouseout":Ge=null;break;case"pointerover":case"pointerout":vn.delete(t.pointerId);break;case"gotpointercapture":case"lostpointercapture":_t.delete(t.pointerId)}}function Mt(e,t,n,a,r,s){return e===null||e.nativeEvent!==s?(e={blockedOn:t,domEventName:n,eventSystemFlags:a,nativeEvent:s,targetContainers:[r]},t!==null&&(t=Xr(t),t!==null&&Rt(t)),e):(e.eventSystemFlags|=a,t=e.targetContainers,r!==null&&t.indexOf(r)===-1&&t.push(r),e)}function bn(e,t,n,a,r){switch(t){case"focusin":return xt=Mt(xt,e,t,n,a,r),!0;case"dragenter":return kt=Mt(kt,e,t,n,a,r),!0;case"mouseover":return Ge=Mt(Ge,e,t,n,a,r),!0;case"pointerover":var s=r.pointerId;return vn.set(s,Mt(vn.get(s)||null,e,t,n,a,r)),!0;case"gotpointercapture":return s=r.pointerId,_t.set(s,Mt(_t.get(s)||null,e,t,n,a,r)),!0}return!1}function Gt(e){var t=ja(e.target);if(t!==null){var n=dn(t);if(n!==null){if(t=n.tag,t===13){if(t=Da(n),t!==null){e.blockedOn=t,ct(e.priority,function(){an(n)});return}}else if(t===3&&n.stateNode.current.memoizedState.isDehydrated){e.blockedOn=n.tag===3?n.stateNode.containerInfo:null;return}}}e.blockedOn=null}function Ut(e){if(e.blockedOn!==null)return!1;for(var t=e.targetContainers;0<t.length;){var n=la(e.domEventName,e.eventSystemFlags,t[0],e.nativeEvent);if(n===null){n=e.nativeEvent;var a=new n.constructor(n.type,n);bt=a,n.target.dispatchEvent(a),bt=null}else return t=Xr(n),t!==null&&Rt(t),e.blockedOn=n,!1;t.shift()}return!0}function Gn(e,t,n){Ut(e)&&n.delete(t)}function Ue(){st=!1,xt!==null&&Ut(xt)&&(xt=null),kt!==null&&Ut(kt)&&(kt=null),Ge!==null&&Ut(Ge)&&(Ge=null),vn.forEach(Gn),_t.forEach(Gn)}function qt(e,t){e.blockedOn===t&&(e.blockedOn=null,st||(st=!0,D.unstable_scheduleCallback(D.unstable_NormalPriority,Ue)))}function wn(e){function t(r){return qt(r,e)}if(0<je.length){qt(je[0],e);for(var n=1;n<je.length;n++){var a=je[n];a.blockedOn===e&&(a.blockedOn=null)}}for(xt!==null&&qt(xt,e),kt!==null&&qt(kt,e),Ge!==null&&qt(Ge,e),vn.forEach(t),_t.forEach(t),n=0;n<dt.length;n++)a=dt[n],a.blockedOn===e&&(a.blockedOn=null);for(;0<dt.length&&(n=dt[0],n.blockedOn===null);)Gt(n),n.blockedOn===null&&dt.shift()}var Un=A.ReactCurrentBatchConfig,qn=!0;function sr(e,t,n,a){var r=he,s=Un.transition;Un.transition=null;try{he=1,Oa(e,t,n,a)}finally{he=r,Un.transition=s}}function or(e,t,n,a){var r=he,s=Un.transition;Un.transition=null;try{he=4,Oa(e,t,n,a)}finally{he=r,Un.transition=s}}function Oa(e,t,n,a){if(qn){var r=la(e,t,n,a);if(r===null)Lo(e,t,a,Se,n),Wt(e,a);else if(bn(r,e,t,n,a))a.stopPropagation();else if(Wt(e,a),t&4&&-1<Yt.indexOf(e)){for(;r!==null;){var s=Xr(r);if(s!==null&&mt(s),s=la(e,t,n,a),s===null&&Lo(e,t,a,Se,n),s===r)break;r=s}r!==null&&a.stopPropagation()}else Lo(e,t,a,null,n)}}var Se=null;function la(e,t,n,a){if(Se=null,e=Or(a),e=ja(e),e!==null)if(t=dn(e),t===null)e=null;else if(n=t.tag,n===13){if(e=Da(t),e!==null)return e;e=null}else if(n===3){if(t.stateNode.current.memoizedState.isDehydrated)return t.tag===3?t.stateNode.containerInfo:null;e=null}else t!==e&&(e=null);return Se=e,null}function ws(e){switch(e){case"cancel":case"click":case"close":case"contextmenu":case"copy":case"cut":case"auxclick":case"dblclick":case"dragend":case"dragstart":case"drop":case"focusin":case"focusout":case"input":case"invalid":case"keydown":case"keypress":case"keyup":case"mousedown":case"mouseup":case"paste":case"pause":case"play":case"pointercancel":case"pointerdown":case"pointerup":case"ratechange":case"reset":case"resize":case"seeked":case"submit":case"touchcancel":case"touchend":case"touchstart":case"volumechange":case"change":case"selectionchange":case"textInput":case"compositionstart":case"compositionend":case"compositionupdate":case"beforeblur":case"afterblur":case"beforeinput":case"blur":case"fullscreenchange":case"focus":case"hashchange":case"popstate":case"select":case"selectstart":return 1;case"drag":case"dragenter":case"dragexit":case"dragleave":case"dragover":case"mousemove":case"mouseout":case"mouseover":case"pointermove":case"pointerout":case"pointerover":case"scroll":case"toggle":case"touchmove":case"wheel":case"mouseenter":case"mouseleave":case"pointerenter":case"pointerleave":return 4;case"message":switch(L()){case N:return 1;case H:return 4;case O:case Q:return 16;case F:return 536870912;default:return 16}default:return 16}}var kn=null,ir=null,Ba=null;function Fa(){if(Ba)return Ba;var e,t=ir,n=t.length,a,r="value"in kn?kn.value:kn.textContent,s=r.length;for(e=0;e<n&&t[e]===r[e];e++);var i=n-e;for(a=1;a<=i&&t[n-a]===r[s-a];a++);return Ba=r.slice(e,1<a?1-a:void 0)}function Ka(e){var t=e.keyCode;return"charCode"in e?(e=e.charCode,e===0&&t===13&&(e=13)):e=t,e===10&&(e=13),32<=e||e===13?e:0}function ca(){return!0}function lr(){return!1}function Dt(e){function t(n,a,r,s,i){this._reactName=n,this._targetInst=r,this.type=a,this.nativeEvent=s,this.target=i,this.currentTarget=null;for(var l in e)e.hasOwnProperty(l)&&(n=e[l],this[l]=n?n(s):s[l]);return this.isDefaultPrevented=(s.defaultPrevented!=null?s.defaultPrevented:s.returnValue===!1)?ca:lr,this.isPropagationStopped=lr,this}return x(t.prototype,{preventDefault:function(){this.defaultPrevented=!0;var n=this.nativeEvent;n&&(n.preventDefault?n.preventDefault():typeof n.returnValue!="unknown"&&(n.returnValue=!1),this.isDefaultPrevented=ca)},stopPropagation:function(){var n=this.nativeEvent;n&&(n.stopPropagation?n.stopPropagation():typeof n.cancelBubble!="unknown"&&(n.cancelBubble=!0),this.isPropagationStopped=ca)},persist:function(){},isPersistent:ca}),t}var Qn={eventPhase:0,bubbles:0,cancelable:0,timeStamp:function(e){return e.timeStamp||Date.now()},defaultPrevented:0,isTrusted:0},cr=Dt(Qn),zn=x({},Qn,{view:0,detail:0}),Sn=Dt(zn),xn,Mn,Dn,Yn=x({},zn,{screenX:0,screenY:0,clientX:0,clientY:0,pageX:0,pageY:0,ctrlKey:0,shiftKey:0,altKey:0,metaKey:0,getModifierState:vo,button:0,buttons:0,relatedTarget:function(e){return e.relatedTarget===void 0?e.fromElement===e.srcElement?e.toElement:e.fromElement:e.relatedTarget},movementX:function(e){return"movementX"in e?e.movementX:(e!==Dn&&(Dn&&e.type==="mousemove"?(xn=e.screenX-Dn.screenX,Mn=e.screenY-Dn.screenY):Mn=xn=0,Dn=e),xn)},movementY:function(e){return"movementY"in e?e.movementY:Mn}}),da=Dt(Yn),ks=x({},Yn,{dataTransfer:0}),Hr=Dt(ks),dr=x({},zn,{relatedTarget:0}),Wr=Dt(dr),fo=x({},Qn,{animationName:0,elapsedTime:0,pseudoElement:0}),wd=Dt(fo),kd=x({},Qn,{clipboardData:function(e){return"clipboardData"in e?e.clipboardData:window.clipboardData}}),Sd=Dt(kd),Cd=x({},Qn,{data:0}),zi=Dt(Cd),Td={Esc:"Escape",Spacebar:" ",Left:"ArrowLeft",Up:"ArrowUp",Right:"ArrowRight",Down:"ArrowDown",Del:"Delete",Win:"OS",Menu:"ContextMenu",Apps:"ContextMenu",Scroll:"ScrollLock",MozPrintableKey:"Unidentified"},Ed={8:"Backspace",9:"Tab",12:"Clear",13:"Enter",16:"Shift",17:"Control",18:"Alt",19:"Pause",20:"CapsLock",27:"Escape",32:" ",33:"PageUp",34:"PageDown",35:"End",36:"Home",37:"ArrowLeft",38:"ArrowUp",39:"ArrowRight",40:"ArrowDown",45:"Insert",46:"Delete",112:"F1",113:"F2",114:"F3",115:"F4",116:"F5",117:"F6",118:"F7",119:"F8",120:"F9",121:"F10",122:"F11",123:"F12",144:"NumLock",145:"ScrollLock",224:"Meta"},_d={Alt:"altKey",Control:"ctrlKey",Meta:"metaKey",Shift:"shiftKey"};function Ad(e){var t=this.nativeEvent;return t.getModifierState?t.getModifierState(e):(e=_d[e])?!!t[e]:!1}function vo(){return Ad}var Id=x({},zn,{key:function(e){if(e.key){var t=Td[e.key]||e.key;if(t!=="Unidentified")return t}return e.type==="keypress"?(e=Ka(e),e===13?"Enter":String.fromCharCode(e)):e.type==="keydown"||e.type==="keyup"?Ed[e.keyCode]||"Unidentified":""},code:0,location:0,ctrlKey:0,shiftKey:0,altKey:0,metaKey:0,repeat:0,locale:0,getModifierState:vo,charCode:function(e){return e.type==="keypress"?Ka(e):0},keyCode:function(e){return e.type==="keydown"||e.type==="keyup"?e.keyCode:0},which:function(e){return e.type==="keypress"?Ka(e):e.type==="keydown"||e.type==="keyup"?e.keyCode:0}}),Ld=Dt(Id),Nd=x({},Yn,{pointerId:0,width:0,height:0,pressure:0,tangentialPressure:0,tiltX:0,tiltY:0,twist:0,pointerType:0,isPrimary:0}),Yi=Dt(Nd),Rd=x({},zn,{touches:0,targetTouches:0,changedTouches:0,altKey:0,metaKey:0,ctrlKey:0,shiftKey:0,getModifierState:vo}),Pd=Dt(Rd),xd=x({},Qn,{propertyName:0,elapsedTime:0,pseudoElement:0}),Md=Dt(xd),Dd=x({},Yn,{deltaX:function(e){return"deltaX"in e?e.deltaX:"wheelDeltaX"in e?-e.wheelDeltaX:0},deltaY:function(e){return"deltaY"in e?e.deltaY:"wheelDeltaY"in e?-e.wheelDeltaY:"wheelDelta"in e?-e.wheelDelta:0},deltaZ:0,deltaMode:0}),Od=Dt(Dd),Bd=[9,13,27,32],bo=ye&&"CompositionEvent"in window,Gr=null;ye&&"documentMode"in document&&(Gr=document.documentMode);var Fd=ye&&"TextEvent"in window&&!Gr,Vi=ye&&(!bo||Gr&&8<Gr&&11>=Gr),$i=" ",Ji=!1;function Xi(e,t){switch(e){case"keyup":return Bd.indexOf(t.keyCode)!==-1;case"keydown":return t.keyCode!==229;case"keypress":case"mousedown":case"focusout":return!0;default:return!1}}function Zi(e){return e=e.detail,typeof e=="object"&&"data"in e?e.data:null}var pr=!1;function Kd(e,t){switch(e){case"compositionend":return Zi(t);case"keypress":return t.which!==32?null:(Ji=!0,$i);case"textInput":return e=t.data,e===$i&&Ji?null:e;default:return null}}function jd(e,t){if(pr)return e==="compositionend"||!bo&&Xi(e,t)?(e=Fa(),Ba=ir=kn=null,pr=!1,e):null;switch(e){case"paste":return null;case"keypress":if(!(t.ctrlKey||t.altKey||t.metaKey)||t.ctrlKey&&t.altKey){if(t.char&&1<t.char.length)return t.char;if(t.which)return String.fromCharCode(t.which)}return null;case"compositionend":return Vi&&t.locale!=="ko"?null:t.data;default:return null}}var Hd={color:!0,date:!0,datetime:!0,"datetime-local":!0,email:!0,month:!0,number:!0,password:!0,range:!0,search:!0,tel:!0,text:!0,time:!0,url:!0,week:!0};function el(e){var t=e&&e.nodeName&&e.nodeName.toLowerCase();return t==="input"?!!Hd[e.type]:t==="textarea"}function tl(e,t,n,a){vs(a),t=_s(t,"onChange"),0<t.length&&(n=new cr("onChange","change",null,n,a),e.push({event:n,listeners:t}))}var Ur=null,qr=null;function Wd(e){bl(e,0)}function Ss(e){var t=yr(e);if(Rr(t))return e}function Gd(e,t){if(e==="change")return t}var nl=!1;if(ye){var wo;if(ye){var ko="oninput"in document;if(!ko){var al=document.createElement("div");al.setAttribute("oninput","return;"),ko=typeof al.oninput=="function"}wo=ko}else wo=!1;nl=wo&&(!document.documentMode||9<document.documentMode)}function rl(){Ur&&(Ur.detachEvent("onpropertychange",sl),qr=Ur=null)}function sl(e){if(e.propertyName==="value"&&Ss(qr)){var t=[];tl(t,qr,e,Or(e)),Kr(Wd,t)}}function Ud(e,t,n){e==="focusin"?(rl(),Ur=t,qr=n,Ur.attachEvent("onpropertychange",sl)):e==="focusout"&&rl()}function qd(e){if(e==="selectionchange"||e==="keyup"||e==="keydown")return Ss(qr)}function Qd(e,t){if(e==="click")return Ss(t)}function zd(e,t){if(e==="input"||e==="change")return Ss(t)}function Yd(e,t){return e===t&&(e!==0||1/e===1/t)||e!==e&&t!==t}var Cn=typeof Object.is=="function"?Object.is:Yd;function Qr(e,t){if(Cn(e,t))return!0;if(typeof e!="object"||e===null||typeof t!="object"||t===null)return!1;var n=Object.keys(e),a=Object.keys(t);if(n.length!==a.length)return!1;for(a=0;a<n.length;a++){var r=n[a];if(!Z.call(t,r)||!Cn(e[r],t[r]))return!1}return!0}function ol(e){for(;e&&e.firstChild;)e=e.firstChild;return e}function il(e,t){var n=ol(e);e=0;for(var a;n;){if(n.nodeType===3){if(a=e+n.textContent.length,e<=t&&a>=t)return{node:n,offset:t-e};e=a}e:{for(;n;){if(n.nextSibling){n=n.nextSibling;break e}n=n.parentNode}n=void 0}n=ol(n)}}function ll(e,t){return e&&t?e===t?!0:e&&e.nodeType===3?!1:t&&t.nodeType===3?ll(e,t.parentNode):"contains"in e?e.contains(t):e.compareDocumentPosition?!!(e.compareDocumentPosition(t)&16):!1:!1}function cl(){for(var e=window,t=Wn();t instanceof e.HTMLIFrameElement;){try{var n=typeof t.contentWindow.location.href=="string"}catch{n=!1}if(n)e=t.contentWindow;else break;t=Wn(e.document)}return t}function So(e){var t=e&&e.nodeName&&e.nodeName.toLowerCase();return t&&(t==="input"&&(e.type==="text"||e.type==="search"||e.type==="tel"||e.type==="url"||e.type==="password")||t==="textarea"||e.contentEditable==="true")}function Vd(e){var t=cl(),n=e.focusedElem,a=e.selectionRange;if(t!==n&&n&&n.ownerDocument&&ll(n.ownerDocument.documentElement,n)){if(a!==null&&So(n)){if(t=a.start,e=a.end,e===void 0&&(e=t),"selectionStart"in n)n.selectionStart=t,n.selectionEnd=Math.min(e,n.value.length);else if(e=(t=n.ownerDocument||document)&&t.defaultView||window,e.getSelection){e=e.getSelection();var r=n.textContent.length,s=Math.min(a.start,r);a=a.end===void 0?s:Math.min(a.end,r),!e.extend&&s>a&&(r=a,a=s,s=r),r=il(n,s);var i=il(n,a);r&&i&&(e.rangeCount!==1||e.anchorNode!==r.node||e.anchorOffset!==r.offset||e.focusNode!==i.node||e.focusOffset!==i.offset)&&(t=t.createRange(),t.setStart(r.node,r.offset),e.removeAllRanges(),s>a?(e.addRange(t),e.extend(i.node,i.offset)):(t.setEnd(i.node,i.offset),e.addRange(t)))}}for(t=[],e=n;e=e.parentNode;)e.nodeType===1&&t.push({element:e,left:e.scrollLeft,top:e.scrollTop});for(typeof n.focus=="function"&&n.focus(),n=0;n<t.length;n++)e=t[n],e.element.scrollLeft=e.left,e.element.scrollTop=e.top}}var $d=ye&&"documentMode"in document&&11>=document.documentMode,ur=null,Co=null,zr=null,To=!1;function dl(e,t,n){var a=n.window===n?n.document:n.nodeType===9?n:n.ownerDocument;To||ur==null||ur!==Wn(a)||(a=ur,"selectionStart"in a&&So(a)?a={start:a.selectionStart,end:a.selectionEnd}:(a=(a.ownerDocument&&a.ownerDocument.defaultView||window).getSelection(),a={anchorNode:a.anchorNode,anchorOffset:a.anchorOffset,focusNode:a.focusNode,focusOffset:a.focusOffset}),zr&&Qr(zr,a)||(zr=a,a=_s(Co,"onSelect"),0<a.length&&(t=new cr("onSelect","select",null,t,n),e.push({event:t,listeners:a}),t.target=ur)))}function Cs(e,t){var n={};return n[e.toLowerCase()]=t.toLowerCase(),n["Webkit"+e]="webkit"+t,n["Moz"+e]="moz"+t,n}var mr={animationend:Cs("Animation","AnimationEnd"),animationiteration:Cs("Animation","AnimationIteration"),animationstart:Cs("Animation","AnimationStart"),transitionend:Cs("Transition","TransitionEnd")},Eo={},pl={};ye&&(pl=document.createElement("div").style,"AnimationEvent"in window||(delete mr.animationend.animation,delete mr.animationiteration.animation,delete mr.animationstart.animation),"TransitionEvent"in window||delete mr.transitionend.transition);function Ts(e){if(Eo[e])return Eo[e];if(!mr[e])return e;var t=mr[e],n;for(n in t)if(t.hasOwnProperty(n)&&n in pl)return Eo[e]=t[n];return e}var ul=Ts("animationend"),ml=Ts("animationiteration"),hl=Ts("animationstart"),gl=Ts("transitionend"),yl=new Map,fl="abort auxClick cancel canPlay canPlayThrough click close contextMenu copy cut drag dragEnd dragEnter dragExit dragLeave dragOver dragStart drop durationChange emptied encrypted ended error gotPointerCapture input invalid keyDown keyPress keyUp load loadedData loadedMetadata loadStart lostPointerCapture mouseDown mouseMove mouseOut mouseOver mouseUp paste pause play playing pointerCancel pointerDown pointerMove pointerOut pointerOver pointerUp progress rateChange reset resize seeked seeking stalled submit suspend timeUpdate touchCancel touchEnd touchStart volumeChange scroll toggle touchMove waiting wheel".split(" ");function pa(e,t){yl.set(e,t),re(t,[e])}for(var _o=0;_o<fl.length;_o++){var Ao=fl[_o],Jd=Ao.toLowerCase(),Xd=Ao[0].toUpperCase()+Ao.slice(1);pa(Jd,"on"+Xd)}pa(ul,"onAnimationEnd"),pa(ml,"onAnimationIteration"),pa(hl,"onAnimationStart"),pa("dblclick","onDoubleClick"),pa("focusin","onFocus"),pa("focusout","onBlur"),pa(gl,"onTransitionEnd"),be("onMouseEnter",["mouseout","mouseover"]),be("onMouseLeave",["mouseout","mouseover"]),be("onPointerEnter",["pointerout","pointerover"]),be("onPointerLeave",["pointerout","pointerover"]),re("onChange","change click focusin focusout input keydown keyup selectionchange".split(" ")),re("onSelect","focusout contextmenu dragend focusin keydown keyup mousedown mouseup selectionchange".split(" ")),re("onBeforeInput",["compositionend","keypress","textInput","paste"]),re("onCompositionEnd","compositionend focusout keydown keypress keyup mousedown".split(" ")),re("onCompositionStart","compositionstart focusout keydown keypress keyup mousedown".split(" ")),re("onCompositionUpdate","compositionupdate focusout keydown keypress keyup mousedown".split(" "));var Yr="abort canplay canplaythrough durationchange emptied encrypted ended error loadeddata loadedmetadata loadstart pause play playing progress ratechange resize seeked seeking stalled suspend timeupdate volumechange waiting".split(" "),Zd=new Set("cancel close invalid load scroll toggle".split(" ").concat(Yr));function vl(e,t,n){var a=e.type||"unknown-event";e.currentTarget=n,Ma(a,t,void 0,e),e.currentTarget=null}function bl(e,t){t=(t&4)!==0;for(var n=0;n<e.length;n++){var a=e[n],r=a.event;a=a.listeners;e:{var s=void 0;if(t)for(var i=a.length-1;0<=i;i--){var l=a[i],d=l.instance,f=l.currentTarget;if(l=l.listener,d!==s&&r.isPropagationStopped())break e;vl(r,l,f),s=d}else for(i=0;i<a.length;i++){if(l=a[i],d=l.instance,f=l.currentTarget,l=l.listener,d!==s&&r.isPropagationStopped())break e;vl(r,l,f),s=d}}}if(Ra)throw e=Pa,Ra=!1,Pa=null,e}function $e(e,t){var n=t[Do];n===void 0&&(n=t[Do]=new Set);var a=e+"__bubble";n.has(a)||(wl(t,e,2,!1),n.add(a))}function Io(e,t,n){var a=0;t&&(a|=4),wl(n,e,a,t)}var Es="_reactListening"+Math.random().toString(36).slice(2);function Vr(e){if(!e[Es]){e[Es]=!0,V.forEach(function(n){n!=="selectionchange"&&(Zd.has(n)||Io(n,!1,e),Io(n,!0,e))});var t=e.nodeType===9?e:e.ownerDocument;t===null||t[Es]||(t[Es]=!0,Io("selectionchange",!1,t))}}function wl(e,t,n,a){switch(ws(t)){case 1:var r=sr;break;case 4:r=or;break;default:r=Oa}n=r.bind(null,t,n,e),r=void 0,!ar||t!=="touchstart"&&t!=="touchmove"&&t!=="wheel"||(r=!0),a?r!==void 0?e.addEventListener(t,n,{capture:!0,passive:r}):e.addEventListener(t,n,!0):r!==void 0?e.addEventListener(t,n,{passive:r}):e.addEventListener(t,n,!1)}function Lo(e,t,n,a,r){var s=a;if((t&1)===0&&(t&2)===0&&a!==null)e:for(;;){if(a===null)return;var i=a.tag;if(i===3||i===4){var l=a.stateNode.containerInfo;if(l===r||l.nodeType===8&&l.parentNode===r)break;if(i===4)for(i=a.return;i!==null;){var d=i.tag;if((d===3||d===4)&&(d=i.stateNode.containerInfo,d===r||d.nodeType===8&&d.parentNode===r))return;i=i.return}for(;l!==null;){if(i=ja(l),i===null)return;if(d=i.tag,d===5||d===6){a=s=i;continue e}l=l.parentNode}}a=a.return}Kr(function(){var f=s,E=Or(n),I=[];e:{var T=yl.get(e);if(T!==void 0){var W=cr,z=e;switch(e){case"keypress":if(Ka(n)===0)break e;case"keydown":case"keyup":W=Ld;break;case"focusin":z="focus",W=Wr;break;case"focusout":z="blur",W=Wr;break;case"beforeblur":case"afterblur":W=Wr;break;case"click":if(n.button===2)break e;case"auxclick":case"dblclick":case"mousedown":case"mousemove":case"mouseup":case"mouseout":case"mouseover":case"contextmenu":W=da;break;case"drag":case"dragend":case"dragenter":case"dragexit":case"dragleave":case"dragover":case"dragstart":case"drop":W=Hr;break;case"touchcancel":case"touchend":case"touchmove":case"touchstart":W=Pd;break;case ul:case ml:case hl:W=wd;break;case gl:W=Md;break;case"scroll":W=Sn;break;case"wheel":W=Od;break;case"copy":case"cut":case"paste":W=Sd;break;case"gotpointercapture":case"lostpointercapture":case"pointercancel":case"pointerdown":case"pointermove":case"pointerout":case"pointerover":case"pointerup":W=Yi}var Y=(t&4)!==0,ot=!Y&&e==="scroll",h=Y?T!==null?T+"Capture":null:T;Y=[];for(var p=f,y;p!==null;){y=p;var P=y.stateNode;if(y.tag===5&&P!==null&&(y=P,h!==null&&(P=sa(p,h),P!=null&&Y.push($r(p,P,y)))),ot)break;p=p.return}0<Y.length&&(T=new W(T,z,null,n,E),I.push({event:T,listeners:Y}))}}if((t&7)===0){e:{if(T=e==="mouseover"||e==="pointerover",W=e==="mouseout"||e==="pointerout",T&&n!==bt&&(z=n.relatedTarget||n.fromElement)&&(ja(z)||z[Vn]))break e;if((W||T)&&(T=E.window===E?E:(T=E.ownerDocument)?T.defaultView||T.parentWindow:window,W?(z=n.relatedTarget||n.toElement,W=f,z=z?ja(z):null,z!==null&&(ot=dn(z),z!==ot||z.tag!==5&&z.tag!==6)&&(z=null)):(W=null,z=f),W!==z)){if(Y=da,P="onMouseLeave",h="onMouseEnter",p="mouse",(e==="pointerout"||e==="pointerover")&&(Y=Yi,P="onPointerLeave",h="onPointerEnter",p="pointer"),ot=W==null?T:yr(W),y=z==null?T:yr(z),T=new Y(P,p+"leave",W,n,E),T.target=ot,T.relatedTarget=y,P=null,ja(E)===f&&(Y=new Y(h,p+"enter",z,n,E),Y.target=y,Y.relatedTarget=ot,P=Y),ot=P,W&&z)t:{for(Y=W,h=z,p=0,y=Y;y;y=hr(y))p++;for(y=0,P=h;P;P=hr(P))y++;for(;0<p-y;)Y=hr(Y),p--;for(;0<y-p;)h=hr(h),y--;for(;p--;){if(Y===h||h!==null&&Y===h.alternate)break t;Y=hr(Y),h=hr(h)}Y=null}else Y=null;W!==null&&kl(I,T,W,Y,!1),z!==null&&ot!==null&&kl(I,ot,z,Y,!0)}}e:{if(T=f?yr(f):window,W=T.nodeName&&T.nodeName.toLowerCase(),W==="select"||W==="input"&&T.type==="file")var J=Gd;else if(el(T))if(nl)J=zd;else{J=qd;var ie=Ud}else(W=T.nodeName)&&W.toLowerCase()==="input"&&(T.type==="checkbox"||T.type==="radio")&&(J=Qd);if(J&&(J=J(e,f))){tl(I,J,n,E);break e}ie&&ie(e,T,f),e==="focusout"&&(ie=T._wrapperState)&&ie.controlled&&T.type==="number"&&xr(T,"number",T.value)}switch(ie=f?yr(f):window,e){case"focusin":(el(ie)||ie.contentEditable==="true")&&(ur=ie,Co=f,zr=null);break;case"focusout":zr=Co=ur=null;break;case"mousedown":To=!0;break;case"contextmenu":case"mouseup":case"dragend":To=!1,dl(I,n,E);break;case"selectionchange":if($d)break;case"keydown":case"keyup":dl(I,n,E)}var le;if(bo)e:{switch(e){case"compositionstart":var ge="onCompositionStart";break e;case"compositionend":ge="onCompositionEnd";break e;case"compositionupdate":ge="onCompositionUpdate";break e}ge=void 0}else pr?Xi(e,n)&&(ge="onCompositionEnd"):e==="keydown"&&n.keyCode===229&&(ge="onCompositionStart");ge&&(Vi&&n.locale!=="ko"&&(pr||ge!=="onCompositionStart"?ge==="onCompositionEnd"&&pr&&(le=Fa()):(kn=E,ir="value"in kn?kn.value:kn.textContent,pr=!0)),ie=_s(f,ge),0<ie.length&&(ge=new zi(ge,e,null,n,E),I.push({event:ge,listeners:ie}),le?ge.data=le:(le=Zi(n),le!==null&&(ge.data=le)))),(le=Fd?Kd(e,n):jd(e,n))&&(f=_s(f,"onBeforeInput"),0<f.length&&(E=new zi("onBeforeInput","beforeinput",null,n,E),I.push({event:E,listeners:f}),E.data=le))}bl(I,t)})}function $r(e,t,n){return{instance:e,listener:t,currentTarget:n}}function _s(e,t){for(var n=t+"Capture",a=[];e!==null;){var r=e,s=r.stateNode;r.tag===5&&s!==null&&(r=s,s=sa(e,n),s!=null&&a.unshift($r(e,s,r)),s=sa(e,t),s!=null&&a.push($r(e,s,r))),e=e.return}return a}function hr(e){if(e===null)return null;do e=e.return;while(e&&e.tag!==5);return e||null}function kl(e,t,n,a,r){for(var s=t._reactName,i=[];n!==null&&n!==a;){var l=n,d=l.alternate,f=l.stateNode;if(d!==null&&d===a)break;l.tag===5&&f!==null&&(l=f,r?(d=sa(n,s),d!=null&&i.unshift($r(n,d,l))):r||(d=sa(n,s),d!=null&&i.push($r(n,d,l)))),n=n.return}i.length!==0&&e.push({event:t,listeners:i})}var ep=/\r\n?/g,tp=/\u0000|\uFFFD/g;function Sl(e){return(typeof e=="string"?e:""+e).replace(ep,`
`).replace(tp,"")}function As(e,t,n){if(t=Sl(t),Sl(e)!==t&&n)throw Error(g(425))}function Is(){}var No=null,Ro=null;function Po(e,t){return e==="textarea"||e==="noscript"||typeof t.children=="string"||typeof t.children=="number"||typeof t.dangerouslySetInnerHTML=="object"&&t.dangerouslySetInnerHTML!==null&&t.dangerouslySetInnerHTML.__html!=null}var xo=typeof setTimeout=="function"?setTimeout:void 0,np=typeof clearTimeout=="function"?clearTimeout:void 0,Cl=typeof Promise=="function"?Promise:void 0,ap=typeof queueMicrotask=="function"?queueMicrotask:typeof Cl<"u"?function(e){return Cl.resolve(null).then(e).catch(rp)}:xo;function rp(e){setTimeout(function(){throw e})}function Mo(e,t){var n=t,a=0;do{var r=n.nextSibling;if(e.removeChild(n),r&&r.nodeType===8)if(n=r.data,n==="/$"){if(a===0){e.removeChild(r),wn(t);return}a--}else n!=="$"&&n!=="$?"&&n!=="$!"||a++;n=r}while(n);wn(t)}function ua(e){for(;e!=null;e=e.nextSibling){var t=e.nodeType;if(t===1||t===3)break;if(t===8){if(t=e.data,t==="$"||t==="$!"||t==="$?")break;if(t==="/$")return null}}return e}function Tl(e){e=e.previousSibling;for(var t=0;e;){if(e.nodeType===8){var n=e.data;if(n==="$"||n==="$!"||n==="$?"){if(t===0)return e;t--}else n==="/$"&&t++}e=e.previousSibling}return null}var gr=Math.random().toString(36).slice(2),On="__reactFiber$"+gr,Jr="__reactProps$"+gr,Vn="__reactContainer$"+gr,Do="__reactEvents$"+gr,sp="__reactListeners$"+gr,op="__reactHandles$"+gr;function ja(e){var t=e[On];if(t)return t;for(var n=e.parentNode;n;){if(t=n[Vn]||n[On]){if(n=t.alternate,t.child!==null||n!==null&&n.child!==null)for(e=Tl(e);e!==null;){if(n=e[On])return n;e=Tl(e)}return t}e=n,n=e.parentNode}return null}function Xr(e){return e=e[On]||e[Vn],!e||e.tag!==5&&e.tag!==6&&e.tag!==13&&e.tag!==3?null:e}function yr(e){if(e.tag===5||e.tag===6)return e.stateNode;throw Error(g(33))}function Ls(e){return e[Jr]||null}var Oo=[],fr=-1;function ma(e){return{current:e}}function Je(e){0>fr||(e.current=Oo[fr],Oo[fr]=null,fr--)}function Ve(e,t){fr++,Oo[fr]=e.current,e.current=t}var ha={},Ot=ma(ha),Vt=ma(!1),Ha=ha;function vr(e,t){var n=e.type.contextTypes;if(!n)return ha;var a=e.stateNode;if(a&&a.__reactInternalMemoizedUnmaskedChildContext===t)return a.__reactInternalMemoizedMaskedChildContext;var r={},s;for(s in n)r[s]=t[s];return a&&(e=e.stateNode,e.__reactInternalMemoizedUnmaskedChildContext=t,e.__reactInternalMemoizedMaskedChildContext=r),r}function $t(e){return e=e.childContextTypes,e!=null}function Ns(){Je(Vt),Je(Ot)}function El(e,t,n){if(Ot.current!==ha)throw Error(g(168));Ve(Ot,t),Ve(Vt,n)}function _l(e,t,n){var a=e.stateNode;if(t=t.childContextTypes,typeof a.getChildContext!="function")return n;a=a.getChildContext();for(var r in a)if(!(r in t))throw Error(g(108,fe(e)||"Unknown",r));return x({},n,a)}function Rs(e){return e=(e=e.stateNode)&&e.__reactInternalMemoizedMergedChildContext||ha,Ha=Ot.current,Ve(Ot,e),Ve(Vt,Vt.current),!0}function Al(e,t,n){var a=e.stateNode;if(!a)throw Error(g(169));n?(e=_l(e,t,Ha),a.__reactInternalMemoizedMergedChildContext=e,Je(Vt),Je(Ot),Ve(Ot,e)):Je(Vt),Ve(Vt,n)}var $n=null,Ps=!1,Bo=!1;function Il(e){$n===null?$n=[e]:$n.push(e)}function ip(e){Ps=!0,Il(e)}function ga(){if(!Bo&&$n!==null){Bo=!0;var e=0,t=he;try{var n=$n;for(he=1;e<n.length;e++){var a=n[e];do a=a(!0);while(a!==null)}$n=null,Ps=!1}catch(r){throw $n!==null&&($n=$n.slice(e+1)),u(N,ga),r}finally{he=t,Bo=!1}}return null}var br=[],wr=0,xs=null,Ms=0,pn=[],un=0,Wa=null,Jn=1,Xn="";function Ga(e,t){br[wr++]=Ms,br[wr++]=xs,xs=e,Ms=t}function Ll(e,t,n){pn[un++]=Jn,pn[un++]=Xn,pn[un++]=Wa,Wa=e;var a=Jn;e=Xn;var r=32-pe(a)-1;a&=~(1<<r),n+=1;var s=32-pe(t)+r;if(30<s){var i=r-r%5;s=(a&(1<<i)-1).toString(32),a>>=i,r-=i,Jn=1<<32-pe(t)+r|n<<r|a,Xn=s+e}else Jn=1<<s|n<<r|a,Xn=e}function Fo(e){e.return!==null&&(Ga(e,1),Ll(e,1,0))}function Ko(e){for(;e===xs;)xs=br[--wr],br[wr]=null,Ms=br[--wr],br[wr]=null;for(;e===Wa;)Wa=pn[--un],pn[un]=null,Xn=pn[--un],pn[un]=null,Jn=pn[--un],pn[un]=null}var rn=null,sn=null,et=!1,Tn=null;function Nl(e,t){var n=yn(5,null,null,0);n.elementType="DELETED",n.stateNode=t,n.return=e,t=e.deletions,t===null?(e.deletions=[n],e.flags|=16):t.push(n)}function Rl(e,t){switch(e.tag){case 5:var n=e.type;return t=t.nodeType!==1||n.toLowerCase()!==t.nodeName.toLowerCase()?null:t,t!==null?(e.stateNode=t,rn=e,sn=ua(t.firstChild),!0):!1;case 6:return t=e.pendingProps===""||t.nodeType!==3?null:t,t!==null?(e.stateNode=t,rn=e,sn=null,!0):!1;case 13:return t=t.nodeType!==8?null:t,t!==null?(n=Wa!==null?{id:Jn,overflow:Xn}:null,e.memoizedState={dehydrated:t,treeContext:n,retryLane:1073741824},n=yn(18,null,null,0),n.stateNode=t,n.return=e,e.child=n,rn=e,sn=null,!0):!1;default:return!1}}function jo(e){return(e.mode&1)!==0&&(e.flags&128)===0}function Ho(e){if(et){var t=sn;if(t){var n=t;if(!Rl(e,t)){if(jo(e))throw Error(g(418));t=ua(n.nextSibling);var a=rn;t&&Rl(e,t)?Nl(a,n):(e.flags=e.flags&-4097|2,et=!1,rn=e)}}else{if(jo(e))throw Error(g(418));e.flags=e.flags&-4097|2,et=!1,rn=e}}}function Pl(e){for(e=e.return;e!==null&&e.tag!==5&&e.tag!==3&&e.tag!==13;)e=e.return;rn=e}function Ds(e){if(e!==rn)return!1;if(!et)return Pl(e),et=!0,!1;var t;if((t=e.tag!==3)&&!(t=e.tag!==5)&&(t=e.type,t=t!=="head"&&t!=="body"&&!Po(e.type,e.memoizedProps)),t&&(t=sn)){if(jo(e))throw xl(),Error(g(418));for(;t;)Nl(e,t),t=ua(t.nextSibling)}if(Pl(e),e.tag===13){if(e=e.memoizedState,e=e!==null?e.dehydrated:null,!e)throw Error(g(317));e:{for(e=e.nextSibling,t=0;e;){if(e.nodeType===8){var n=e.data;if(n==="/$"){if(t===0){sn=ua(e.nextSibling);break e}t--}else n!=="$"&&n!=="$!"&&n!=="$?"||t++}e=e.nextSibling}sn=null}}else sn=rn?ua(e.stateNode.nextSibling):null;return!0}function xl(){for(var e=sn;e;)e=ua(e.nextSibling)}function kr(){sn=rn=null,et=!1}function Wo(e){Tn===null?Tn=[e]:Tn.push(e)}var lp=A.ReactCurrentBatchConfig;function Zr(e,t,n){if(e=n.ref,e!==null&&typeof e!="function"&&typeof e!="object"){if(n._owner){if(n=n._owner,n){if(n.tag!==1)throw Error(g(309));var a=n.stateNode}if(!a)throw Error(g(147,e));var r=a,s=""+e;return t!==null&&t.ref!==null&&typeof t.ref=="function"&&t.ref._stringRef===s?t.ref:(t=function(i){var l=r.refs;i===null?delete l[s]:l[s]=i},t._stringRef=s,t)}if(typeof e!="string")throw Error(g(284));if(!n._owner)throw Error(g(290,e))}return e}function Os(e,t){throw e=Object.prototype.toString.call(t),Error(g(31,e==="[object Object]"?"object with keys {"+Object.keys(t).join(", ")+"}":e))}function Ml(e){var t=e._init;return t(e._payload)}function Dl(e){function t(h,p){if(e){var y=h.deletions;y===null?(h.deletions=[p],h.flags|=16):y.push(p)}}function n(h,p){if(!e)return null;for(;p!==null;)t(h,p),p=p.sibling;return null}function a(h,p){for(h=new Map;p!==null;)p.key!==null?h.set(p.key,p):h.set(p.index,p),p=p.sibling;return h}function r(h,p){return h=Ca(h,p),h.index=0,h.sibling=null,h}function s(h,p,y){return h.index=y,e?(y=h.alternate,y!==null?(y=y.index,y<p?(h.flags|=2,p):y):(h.flags|=2,p)):(h.flags|=1048576,p)}function i(h){return e&&h.alternate===null&&(h.flags|=2),h}function l(h,p,y,P){return p===null||p.tag!==6?(p=xi(y,h.mode,P),p.return=h,p):(p=r(p,y),p.return=h,p)}function d(h,p,y,P){var J=y.type;return J===Ee?E(h,p,y.props.children,P,y.key):p!==null&&(p.elementType===J||typeof J=="object"&&J!==null&&J.$$typeof===tt&&Ml(J)===p.type)?(P=r(p,y.props),P.ref=Zr(h,p,y),P.return=h,P):(P=oo(y.type,y.key,y.props,null,h.mode,P),P.ref=Zr(h,p,y),P.return=h,P)}function f(h,p,y,P){return p===null||p.tag!==4||p.stateNode.containerInfo!==y.containerInfo||p.stateNode.implementation!==y.implementation?(p=Mi(y,h.mode,P),p.return=h,p):(p=r(p,y.children||[]),p.return=h,p)}function E(h,p,y,P,J){return p===null||p.tag!==7?(p=Ja(y,h.mode,P,J),p.return=h,p):(p=r(p,y),p.return=h,p)}function I(h,p,y){if(typeof p=="string"&&p!==""||typeof p=="number")return p=xi(""+p,h.mode,y),p.return=h,p;if(typeof p=="object"&&p!==null){switch(p.$$typeof){case ne:return y=oo(p.type,p.key,p.props,null,h.mode,y),y.ref=Zr(h,null,p),y.return=h,y;case $:return p=Mi(p,h.mode,y),p.return=h,p;case tt:var P=p._init;return I(h,P(p._payload),y)}if(Ia(p)||se(p))return p=Ja(p,h.mode,y,null),p.return=h,p;Os(h,p)}return null}function T(h,p,y,P){var J=p!==null?p.key:null;if(typeof y=="string"&&y!==""||typeof y=="number")return J!==null?null:l(h,p,""+y,P);if(typeof y=="object"&&y!==null){switch(y.$$typeof){case ne:return y.key===J?d(h,p,y,P):null;case $:return y.key===J?f(h,p,y,P):null;case tt:return J=y._init,T(h,p,J(y._payload),P)}if(Ia(y)||se(y))return J!==null?null:E(h,p,y,P,null);Os(h,y)}return null}function W(h,p,y,P,J){if(typeof P=="string"&&P!==""||typeof P=="number")return h=h.get(y)||null,l(p,h,""+P,J);if(typeof P=="object"&&P!==null){switch(P.$$typeof){case ne:return h=h.get(P.key===null?y:P.key)||null,d(p,h,P,J);case $:return h=h.get(P.key===null?y:P.key)||null,f(p,h,P,J);case tt:var ie=P._init;return W(h,p,y,ie(P._payload),J)}if(Ia(P)||se(P))return h=h.get(y)||null,E(p,h,P,J,null);Os(p,P)}return null}function z(h,p,y,P){for(var J=null,ie=null,le=p,ge=p=0,Tt=null;le!==null&&ge<y.length;ge++){le.index>ge?(Tt=le,le=null):Tt=le.sibling;var xe=T(h,le,y[ge],P);if(xe===null){le===null&&(le=Tt);break}e&&le&&xe.alternate===null&&t(h,le),p=s(xe,p,ge),ie===null?J=xe:ie.sibling=xe,ie=xe,le=Tt}if(ge===y.length)return n(h,le),et&&Ga(h,ge),J;if(le===null){for(;ge<y.length;ge++)le=I(h,y[ge],P),le!==null&&(p=s(le,p,ge),ie===null?J=le:ie.sibling=le,ie=le);return et&&Ga(h,ge),J}for(le=a(h,le);ge<y.length;ge++)Tt=W(le,h,ge,y[ge],P),Tt!==null&&(e&&Tt.alternate!==null&&le.delete(Tt.key===null?ge:Tt.key),p=s(Tt,p,ge),ie===null?J=Tt:ie.sibling=Tt,ie=Tt);return e&&le.forEach(function(Ta){return t(h,Ta)}),et&&Ga(h,ge),J}function Y(h,p,y,P){var J=se(y);if(typeof J!="function")throw Error(g(150));if(y=J.call(y),y==null)throw Error(g(151));for(var ie=J=null,le=p,ge=p=0,Tt=null,xe=y.next();le!==null&&!xe.done;ge++,xe=y.next()){le.index>ge?(Tt=le,le=null):Tt=le.sibling;var Ta=T(h,le,xe.value,P);if(Ta===null){le===null&&(le=Tt);break}e&&le&&Ta.alternate===null&&t(h,le),p=s(Ta,p,ge),ie===null?J=Ta:ie.sibling=Ta,ie=Ta,le=Tt}if(xe.done)return n(h,le),et&&Ga(h,ge),J;if(le===null){for(;!xe.done;ge++,xe=y.next())xe=I(h,xe.value,P),xe!==null&&(p=s(xe,p,ge),ie===null?J=xe:ie.sibling=xe,ie=xe);return et&&Ga(h,ge),J}for(le=a(h,le);!xe.done;ge++,xe=y.next())xe=W(le,h,ge,xe.value,P),xe!==null&&(e&&xe.alternate!==null&&le.delete(xe.key===null?ge:xe.key),p=s(xe,p,ge),ie===null?J=xe:ie.sibling=xe,ie=xe);return e&&le.forEach(function(Hp){return t(h,Hp)}),et&&Ga(h,ge),J}function ot(h,p,y,P){if(typeof y=="object"&&y!==null&&y.type===Ee&&y.key===null&&(y=y.props.children),typeof y=="object"&&y!==null){switch(y.$$typeof){case ne:e:{for(var J=y.key,ie=p;ie!==null;){if(ie.key===J){if(J=y.type,J===Ee){if(ie.tag===7){n(h,ie.sibling),p=r(ie,y.props.children),p.return=h,h=p;break e}}else if(ie.elementType===J||typeof J=="object"&&J!==null&&J.$$typeof===tt&&Ml(J)===ie.type){n(h,ie.sibling),p=r(ie,y.props),p.ref=Zr(h,ie,y),p.return=h,h=p;break e}n(h,ie);break}else t(h,ie);ie=ie.sibling}y.type===Ee?(p=Ja(y.props.children,h.mode,P,y.key),p.return=h,h=p):(P=oo(y.type,y.key,y.props,null,h.mode,P),P.ref=Zr(h,p,y),P.return=h,h=P)}return i(h);case $:e:{for(ie=y.key;p!==null;){if(p.key===ie)if(p.tag===4&&p.stateNode.containerInfo===y.containerInfo&&p.stateNode.implementation===y.implementation){n(h,p.sibling),p=r(p,y.children||[]),p.return=h,h=p;break e}else{n(h,p);break}else t(h,p);p=p.sibling}p=Mi(y,h.mode,P),p.return=h,h=p}return i(h);case tt:return ie=y._init,ot(h,p,ie(y._payload),P)}if(Ia(y))return z(h,p,y,P);if(se(y))return Y(h,p,y,P);Os(h,y)}return typeof y=="string"&&y!==""||typeof y=="number"?(y=""+y,p!==null&&p.tag===6?(n(h,p.sibling),p=r(p,y),p.return=h,h=p):(n(h,p),p=xi(y,h.mode,P),p.return=h,h=p),i(h)):n(h,p)}return ot}var Sr=Dl(!0),Ol=Dl(!1),Bs=ma(null),Fs=null,Cr=null,Go=null;function Uo(){Go=Cr=Fs=null}function qo(e){var t=Bs.current;Je(Bs),e._currentValue=t}function Qo(e,t,n){for(;e!==null;){var a=e.alternate;if((e.childLanes&t)!==t?(e.childLanes|=t,a!==null&&(a.childLanes|=t)):a!==null&&(a.childLanes&t)!==t&&(a.childLanes|=t),e===n)break;e=e.return}}function Tr(e,t){Fs=e,Go=Cr=null,e=e.dependencies,e!==null&&e.firstContext!==null&&((e.lanes&t)!==0&&(Jt=!0),e.firstContext=null)}function mn(e){var t=e._currentValue;if(Go!==e)if(e={context:e,memoizedValue:t,next:null},Cr===null){if(Fs===null)throw Error(g(308));Cr=e,Fs.dependencies={lanes:0,firstContext:e}}else Cr=Cr.next=e;return t}var Ua=null;function zo(e){Ua===null?Ua=[e]:Ua.push(e)}function Bl(e,t,n,a){var r=t.interleaved;return r===null?(n.next=n,zo(t)):(n.next=r.next,r.next=n),t.interleaved=n,Zn(e,a)}function Zn(e,t){e.lanes|=t;var n=e.alternate;for(n!==null&&(n.lanes|=t),n=e,e=e.return;e!==null;)e.childLanes|=t,n=e.alternate,n!==null&&(n.childLanes|=t),n=e,e=e.return;return n.tag===3?n.stateNode:null}var ya=!1;function Yo(e){e.updateQueue={baseState:e.memoizedState,firstBaseUpdate:null,lastBaseUpdate:null,shared:{pending:null,interleaved:null,lanes:0},effects:null}}function Fl(e,t){e=e.updateQueue,t.updateQueue===e&&(t.updateQueue={baseState:e.baseState,firstBaseUpdate:e.firstBaseUpdate,lastBaseUpdate:e.lastBaseUpdate,shared:e.shared,effects:e.effects})}function ea(e,t){return{eventTime:e,lane:t,tag:0,payload:null,callback:null,next:null}}function fa(e,t,n){var a=e.updateQueue;if(a===null)return null;if(a=a.shared,(Re&2)!==0){var r=a.pending;return r===null?t.next=t:(t.next=r.next,r.next=t),a.pending=t,Zn(e,n)}return r=a.interleaved,r===null?(t.next=t,zo(a)):(t.next=r.next,r.next=t),a.interleaved=t,Zn(e,n)}function Ks(e,t,n){if(t=t.updateQueue,t!==null&&(t=t.shared,(n&4194240)!==0)){var a=t.lanes;a&=e.pendingLanes,n|=a,t.lanes=n,Ze(e,n)}}function Kl(e,t){var n=e.updateQueue,a=e.alternate;if(a!==null&&(a=a.updateQueue,n===a)){var r=null,s=null;if(n=n.firstBaseUpdate,n!==null){do{var i={eventTime:n.eventTime,lane:n.lane,tag:n.tag,payload:n.payload,callback:n.callback,next:null};s===null?r=s=i:s=s.next=i,n=n.next}while(n!==null);s===null?r=s=t:s=s.next=t}else r=s=t;n={baseState:a.baseState,firstBaseUpdate:r,lastBaseUpdate:s,shared:a.shared,effects:a.effects},e.updateQueue=n;return}e=n.lastBaseUpdate,e===null?n.firstBaseUpdate=t:e.next=t,n.lastBaseUpdate=t}function js(e,t,n,a){var r=e.updateQueue;ya=!1;var s=r.firstBaseUpdate,i=r.lastBaseUpdate,l=r.shared.pending;if(l!==null){r.shared.pending=null;var d=l,f=d.next;d.next=null,i===null?s=f:i.next=f,i=d;var E=e.alternate;E!==null&&(E=E.updateQueue,l=E.lastBaseUpdate,l!==i&&(l===null?E.firstBaseUpdate=f:l.next=f,E.lastBaseUpdate=d))}if(s!==null){var I=r.baseState;i=0,E=f=d=null,l=s;do{var T=l.lane,W=l.eventTime;if((a&T)===T){E!==null&&(E=E.next={eventTime:W,lane:0,tag:l.tag,payload:l.payload,callback:l.callback,next:null});e:{var z=e,Y=l;switch(T=t,W=n,Y.tag){case 1:if(z=Y.payload,typeof z=="function"){I=z.call(W,I,T);break e}I=z;break e;case 3:z.flags=z.flags&-65537|128;case 0:if(z=Y.payload,T=typeof z=="function"?z.call(W,I,T):z,T==null)break e;I=x({},I,T);break e;case 2:ya=!0}}l.callback!==null&&l.lane!==0&&(e.flags|=64,T=r.effects,T===null?r.effects=[l]:T.push(l))}else W={eventTime:W,lane:T,tag:l.tag,payload:l.payload,callback:l.callback,next:null},E===null?(f=E=W,d=I):E=E.next=W,i|=T;if(l=l.next,l===null){if(l=r.shared.pending,l===null)break;T=l,l=T.next,T.next=null,r.lastBaseUpdate=T,r.shared.pending=null}}while(!0);if(E===null&&(d=I),r.baseState=d,r.firstBaseUpdate=f,r.lastBaseUpdate=E,t=r.shared.interleaved,t!==null){r=t;do i|=r.lane,r=r.next;while(r!==t)}else s===null&&(r.shared.lanes=0);za|=i,e.lanes=i,e.memoizedState=I}}function jl(e,t,n){if(e=t.effects,t.effects=null,e!==null)for(t=0;t<e.length;t++){var a=e[t],r=a.callback;if(r!==null){if(a.callback=null,a=n,typeof r!="function")throw Error(g(191,r));r.call(a)}}}var es={},Bn=ma(es),ts=ma(es),ns=ma(es);function qa(e){if(e===es)throw Error(g(174));return e}function Vo(e,t){switch(Ve(ns,t),Ve(ts,e),Ve(Bn,es),e=t.nodeType,e){case 9:case 11:t=(t=t.documentElement)?t.namespaceURI:nn(null,"");break;default:e=e===8?t.parentNode:t,t=e.namespaceURI||null,e=e.tagName,t=nn(t,e)}Je(Bn),Ve(Bn,t)}function Er(){Je(Bn),Je(ts),Je(ns)}function Hl(e){qa(ns.current);var t=qa(Bn.current),n=nn(t,e.type);t!==n&&(Ve(ts,e),Ve(Bn,n))}function $o(e){ts.current===e&&(Je(Bn),Je(ts))}var nt=ma(0);function Hs(e){for(var t=e;t!==null;){if(t.tag===13){var n=t.memoizedState;if(n!==null&&(n=n.dehydrated,n===null||n.data==="$?"||n.data==="$!"))return t}else if(t.tag===19&&t.memoizedProps.revealOrder!==void 0){if((t.flags&128)!==0)return t}else if(t.child!==null){t.child.return=t,t=t.child;continue}if(t===e)break;for(;t.sibling===null;){if(t.return===null||t.return===e)return null;t=t.return}t.sibling.return=t.return,t=t.sibling}return null}var Jo=[];function Xo(){for(var e=0;e<Jo.length;e++)Jo[e]._workInProgressVersionPrimary=null;Jo.length=0}var Ws=A.ReactCurrentDispatcher,Zo=A.ReactCurrentBatchConfig,Qa=0,at=null,ht=null,St=null,Gs=!1,as=!1,rs=0,cp=0;function Bt(){throw Error(g(321))}function ei(e,t){if(t===null)return!1;for(var n=0;n<t.length&&n<e.length;n++)if(!Cn(e[n],t[n]))return!1;return!0}function ti(e,t,n,a,r,s){if(Qa=s,at=t,t.memoizedState=null,t.updateQueue=null,t.lanes=0,Ws.current=e===null||e.memoizedState===null?mp:hp,e=n(a,r),as){s=0;do{if(as=!1,rs=0,25<=s)throw Error(g(301));s+=1,St=ht=null,t.updateQueue=null,Ws.current=gp,e=n(a,r)}while(as)}if(Ws.current=Qs,t=ht!==null&&ht.next!==null,Qa=0,St=ht=at=null,Gs=!1,t)throw Error(g(300));return e}function ni(){var e=rs!==0;return rs=0,e}function Fn(){var e={memoizedState:null,baseState:null,baseQueue:null,queue:null,next:null};return St===null?at.memoizedState=St=e:St=St.next=e,St}function hn(){if(ht===null){var e=at.alternate;e=e!==null?e.memoizedState:null}else e=ht.next;var t=St===null?at.memoizedState:St.next;if(t!==null)St=t,ht=e;else{if(e===null)throw Error(g(310));ht=e,e={memoizedState:ht.memoizedState,baseState:ht.baseState,baseQueue:ht.baseQueue,queue:ht.queue,next:null},St===null?at.memoizedState=St=e:St=St.next=e}return St}function ss(e,t){return typeof t=="function"?t(e):t}function ai(e){var t=hn(),n=t.queue;if(n===null)throw Error(g(311));n.lastRenderedReducer=e;var a=ht,r=a.baseQueue,s=n.pending;if(s!==null){if(r!==null){var i=r.next;r.next=s.next,s.next=i}a.baseQueue=r=s,n.pending=null}if(r!==null){s=r.next,a=a.baseState;var l=i=null,d=null,f=s;do{var E=f.lane;if((Qa&E)===E)d!==null&&(d=d.next={lane:0,action:f.action,hasEagerState:f.hasEagerState,eagerState:f.eagerState,next:null}),a=f.hasEagerState?f.eagerState:e(a,f.action);else{var I={lane:E,action:f.action,hasEagerState:f.hasEagerState,eagerState:f.eagerState,next:null};d===null?(l=d=I,i=a):d=d.next=I,at.lanes|=E,za|=E}f=f.next}while(f!==null&&f!==s);d===null?i=a:d.next=l,Cn(a,t.memoizedState)||(Jt=!0),t.memoizedState=a,t.baseState=i,t.baseQueue=d,n.lastRenderedState=a}if(e=n.interleaved,e!==null){r=e;do s=r.lane,at.lanes|=s,za|=s,r=r.next;while(r!==e)}else r===null&&(n.lanes=0);return[t.memoizedState,n.dispatch]}function ri(e){var t=hn(),n=t.queue;if(n===null)throw Error(g(311));n.lastRenderedReducer=e;var a=n.dispatch,r=n.pending,s=t.memoizedState;if(r!==null){n.pending=null;var i=r=r.next;do s=e(s,i.action),i=i.next;while(i!==r);Cn(s,t.memoizedState)||(Jt=!0),t.memoizedState=s,t.baseQueue===null&&(t.baseState=s),n.lastRenderedState=s}return[s,a]}function Wl(){}function Gl(e,t){var n=at,a=hn(),r=t(),s=!Cn(a.memoizedState,r);if(s&&(a.memoizedState=r,Jt=!0),a=a.queue,si(Ql.bind(null,n,a,e),[e]),a.getSnapshot!==t||s||St!==null&&St.memoizedState.tag&1){if(n.flags|=2048,os(9,ql.bind(null,n,a,r,t),void 0,null),Ct===null)throw Error(g(349));(Qa&30)!==0||Ul(n,t,r)}return r}function Ul(e,t,n){e.flags|=16384,e={getSnapshot:t,value:n},t=at.updateQueue,t===null?(t={lastEffect:null,stores:null},at.updateQueue=t,t.stores=[e]):(n=t.stores,n===null?t.stores=[e]:n.push(e))}function ql(e,t,n,a){t.value=n,t.getSnapshot=a,zl(t)&&Yl(e)}function Ql(e,t,n){return n(function(){zl(t)&&Yl(e)})}function zl(e){var t=e.getSnapshot;e=e.value;try{var n=t();return!Cn(e,n)}catch{return!0}}function Yl(e){var t=Zn(e,1);t!==null&&In(t,e,1,-1)}function Vl(e){var t=Fn();return typeof e=="function"&&(e=e()),t.memoizedState=t.baseState=e,e={pending:null,interleaved:null,lanes:0,dispatch:null,lastRenderedReducer:ss,lastRenderedState:e},t.queue=e,e=e.dispatch=up.bind(null,at,e),[t.memoizedState,e]}function os(e,t,n,a){return e={tag:e,create:t,destroy:n,deps:a,next:null},t=at.updateQueue,t===null?(t={lastEffect:null,stores:null},at.updateQueue=t,t.lastEffect=e.next=e):(n=t.lastEffect,n===null?t.lastEffect=e.next=e:(a=n.next,n.next=e,e.next=a,t.lastEffect=e)),e}function $l(){return hn().memoizedState}function Us(e,t,n,a){var r=Fn();at.flags|=e,r.memoizedState=os(1|t,n,void 0,a===void 0?null:a)}function qs(e,t,n,a){var r=hn();a=a===void 0?null:a;var s=void 0;if(ht!==null){var i=ht.memoizedState;if(s=i.destroy,a!==null&&ei(a,i.deps)){r.memoizedState=os(t,n,s,a);return}}at.flags|=e,r.memoizedState=os(1|t,n,s,a)}function Jl(e,t){return Us(8390656,8,e,t)}function si(e,t){return qs(2048,8,e,t)}function Xl(e,t){return qs(4,2,e,t)}function Zl(e,t){return qs(4,4,e,t)}function ec(e,t){if(typeof t=="function")return e=e(),t(e),function(){t(null)};if(t!=null)return e=e(),t.current=e,function(){t.current=null}}function tc(e,t,n){return n=n!=null?n.concat([e]):null,qs(4,4,ec.bind(null,t,e),n)}function oi(){}function nc(e,t){var n=hn();t=t===void 0?null:t;var a=n.memoizedState;return a!==null&&t!==null&&ei(t,a[1])?a[0]:(n.memoizedState=[e,t],e)}function ac(e,t){var n=hn();t=t===void 0?null:t;var a=n.memoizedState;return a!==null&&t!==null&&ei(t,a[1])?a[0]:(e=e(),n.memoizedState=[e,t],e)}function rc(e,t,n){return(Qa&21)===0?(e.baseState&&(e.baseState=!1,Jt=!0),e.memoizedState=n):(Cn(n,t)||(n=Oe(),at.lanes|=n,za|=n,e.baseState=!0),t)}function dp(e,t){var n=he;he=n!==0&&4>n?n:4,e(!0);var a=Zo.transition;Zo.transition={};try{e(!1),t()}finally{he=n,Zo.transition=a}}function sc(){return hn().memoizedState}function pp(e,t,n){var a=ka(e);if(n={lane:a,action:n,hasEagerState:!1,eagerState:null,next:null},oc(e))ic(t,n);else if(n=Bl(e,t,n,a),n!==null){var r=zt();In(n,e,a,r),lc(n,t,a)}}function up(e,t,n){var a=ka(e),r={lane:a,action:n,hasEagerState:!1,eagerState:null,next:null};if(oc(e))ic(t,r);else{var s=e.alternate;if(e.lanes===0&&(s===null||s.lanes===0)&&(s=t.lastRenderedReducer,s!==null))try{var i=t.lastRenderedState,l=s(i,n);if(r.hasEagerState=!0,r.eagerState=l,Cn(l,i)){var d=t.interleaved;d===null?(r.next=r,zo(t)):(r.next=d.next,d.next=r),t.interleaved=r;return}}catch{}finally{}n=Bl(e,t,r,a),n!==null&&(r=zt(),In(n,e,a,r),lc(n,t,a))}}function oc(e){var t=e.alternate;return e===at||t!==null&&t===at}function ic(e,t){as=Gs=!0;var n=e.pending;n===null?t.next=t:(t.next=n.next,n.next=t),e.pending=t}function lc(e,t,n){if((n&4194240)!==0){var a=t.lanes;a&=e.pendingLanes,n|=a,t.lanes=n,Ze(e,n)}}var Qs={readContext:mn,useCallback:Bt,useContext:Bt,useEffect:Bt,useImperativeHandle:Bt,useInsertionEffect:Bt,useLayoutEffect:Bt,useMemo:Bt,useReducer:Bt,useRef:Bt,useState:Bt,useDebugValue:Bt,useDeferredValue:Bt,useTransition:Bt,useMutableSource:Bt,useSyncExternalStore:Bt,useId:Bt,unstable_isNewReconciler:!1},mp={readContext:mn,useCallback:function(e,t){return Fn().memoizedState=[e,t===void 0?null:t],e},useContext:mn,useEffect:Jl,useImperativeHandle:function(e,t,n){return n=n!=null?n.concat([e]):null,Us(4194308,4,ec.bind(null,t,e),n)},useLayoutEffect:function(e,t){return Us(4194308,4,e,t)},useInsertionEffect:function(e,t){return Us(4,2,e,t)},useMemo:function(e,t){var n=Fn();return t=t===void 0?null:t,e=e(),n.memoizedState=[e,t],e},useReducer:function(e,t,n){var a=Fn();return t=n!==void 0?n(t):t,a.memoizedState=a.baseState=t,e={pending:null,interleaved:null,lanes:0,dispatch:null,lastRenderedReducer:e,lastRenderedState:t},a.queue=e,e=e.dispatch=pp.bind(null,at,e),[a.memoizedState,e]},useRef:function(e){var t=Fn();return e={current:e},t.memoizedState=e},useState:Vl,useDebugValue:oi,useDeferredValue:function(e){return Fn().memoizedState=e},useTransition:function(){var e=Vl(!1),t=e[0];return e=dp.bind(null,e[1]),Fn().memoizedState=e,[t,e]},useMutableSource:function(){},useSyncExternalStore:function(e,t,n){var a=at,r=Fn();if(et){if(n===void 0)throw Error(g(407));n=n()}else{if(n=t(),Ct===null)throw Error(g(349));(Qa&30)!==0||Ul(a,t,n)}r.memoizedState=n;var s={value:n,getSnapshot:t};return r.queue=s,Jl(Ql.bind(null,a,s,e),[e]),a.flags|=2048,os(9,ql.bind(null,a,s,n,t),void 0,null),n},useId:function(){var e=Fn(),t=Ct.identifierPrefix;if(et){var n=Xn,a=Jn;n=(a&~(1<<32-pe(a)-1)).toString(32)+n,t=":"+t+"R"+n,n=rs++,0<n&&(t+="H"+n.toString(32)),t+=":"}else n=cp++,t=":"+t+"r"+n.toString(32)+":";return e.memoizedState=t},unstable_isNewReconciler:!1},hp={readContext:mn,useCallback:nc,useContext:mn,useEffect:si,useImperativeHandle:tc,useInsertionEffect:Xl,useLayoutEffect:Zl,useMemo:ac,useReducer:ai,useRef:$l,useState:function(){return ai(ss)},useDebugValue:oi,useDeferredValue:function(e){var t=hn();return rc(t,ht.memoizedState,e)},useTransition:function(){var e=ai(ss)[0],t=hn().memoizedState;return[e,t]},useMutableSource:Wl,useSyncExternalStore:Gl,useId:sc,unstable_isNewReconciler:!1},gp={readContext:mn,useCallback:nc,useContext:mn,useEffect:si,useImperativeHandle:tc,useInsertionEffect:Xl,useLayoutEffect:Zl,useMemo:ac,useReducer:ri,useRef:$l,useState:function(){return ri(ss)},useDebugValue:oi,useDeferredValue:function(e){var t=hn();return ht===null?t.memoizedState=e:rc(t,ht.memoizedState,e)},useTransition:function(){var e=ri(ss)[0],t=hn().memoizedState;return[e,t]},useMutableSource:Wl,useSyncExternalStore:Gl,useId:sc,unstable_isNewReconciler:!1};function En(e,t){if(e&&e.defaultProps){t=x({},t),e=e.defaultProps;for(var n in e)t[n]===void 0&&(t[n]=e[n]);return t}return t}function ii(e,t,n,a){t=e.memoizedState,n=n(a,t),n=n==null?t:x({},t,n),e.memoizedState=n,e.lanes===0&&(e.updateQueue.baseState=n)}var zs={isMounted:function(e){return(e=e._reactInternals)?dn(e)===e:!1},enqueueSetState:function(e,t,n){e=e._reactInternals;var a=zt(),r=ka(e),s=ea(a,r);s.payload=t,n!=null&&(s.callback=n),t=fa(e,s,r),t!==null&&(In(t,e,r,a),Ks(t,e,r))},enqueueReplaceState:function(e,t,n){e=e._reactInternals;var a=zt(),r=ka(e),s=ea(a,r);s.tag=1,s.payload=t,n!=null&&(s.callback=n),t=fa(e,s,r),t!==null&&(In(t,e,r,a),Ks(t,e,r))},enqueueForceUpdate:function(e,t){e=e._reactInternals;var n=zt(),a=ka(e),r=ea(n,a);r.tag=2,t!=null&&(r.callback=t),t=fa(e,r,a),t!==null&&(In(t,e,a,n),Ks(t,e,a))}};function cc(e,t,n,a,r,s,i){return e=e.stateNode,typeof e.shouldComponentUpdate=="function"?e.shouldComponentUpdate(a,s,i):t.prototype&&t.prototype.isPureReactComponent?!Qr(n,a)||!Qr(r,s):!0}function dc(e,t,n){var a=!1,r=ha,s=t.contextType;return typeof s=="object"&&s!==null?s=mn(s):(r=$t(t)?Ha:Ot.current,a=t.contextTypes,s=(a=a!=null)?vr(e,r):ha),t=new t(n,s),e.memoizedState=t.state!==null&&t.state!==void 0?t.state:null,t.updater=zs,e.stateNode=t,t._reactInternals=e,a&&(e=e.stateNode,e.__reactInternalMemoizedUnmaskedChildContext=r,e.__reactInternalMemoizedMaskedChildContext=s),t}function pc(e,t,n,a){e=t.state,typeof t.componentWillReceiveProps=="function"&&t.componentWillReceiveProps(n,a),typeof t.UNSAFE_componentWillReceiveProps=="function"&&t.UNSAFE_componentWillReceiveProps(n,a),t.state!==e&&zs.enqueueReplaceState(t,t.state,null)}function li(e,t,n,a){var r=e.stateNode;r.props=n,r.state=e.memoizedState,r.refs={},Yo(e);var s=t.contextType;typeof s=="object"&&s!==null?r.context=mn(s):(s=$t(t)?Ha:Ot.current,r.context=vr(e,s)),r.state=e.memoizedState,s=t.getDerivedStateFromProps,typeof s=="function"&&(ii(e,t,s,n),r.state=e.memoizedState),typeof t.getDerivedStateFromProps=="function"||typeof r.getSnapshotBeforeUpdate=="function"||typeof r.UNSAFE_componentWillMount!="function"&&typeof r.componentWillMount!="function"||(t=r.state,typeof r.componentWillMount=="function"&&r.componentWillMount(),typeof r.UNSAFE_componentWillMount=="function"&&r.UNSAFE_componentWillMount(),t!==r.state&&zs.enqueueReplaceState(r,r.state,null),js(e,n,r,a),r.state=e.memoizedState),typeof r.componentDidMount=="function"&&(e.flags|=4194308)}function _r(e,t){try{var n="",a=t;do n+=te(a),a=a.return;while(a);var r=n}catch(s){r=`
Error generating stack: `+s.message+`
`+s.stack}return{value:e,source:t,stack:r,digest:null}}function ci(e,t,n){return{value:e,source:null,stack:n??null,digest:t??null}}function di(e,t){try{console.error(t.value)}catch(n){setTimeout(function(){throw n})}}var yp=typeof WeakMap=="function"?WeakMap:Map;function uc(e,t,n){n=ea(-1,n),n.tag=3,n.payload={element:null};var a=t.value;return n.callback=function(){eo||(eo=!0,Ei=a),di(e,t)},n}function mc(e,t,n){n=ea(-1,n),n.tag=3;var a=e.type.getDerivedStateFromError;if(typeof a=="function"){var r=t.value;n.payload=function(){return a(r)},n.callback=function(){di(e,t)}}var s=e.stateNode;return s!==null&&typeof s.componentDidCatch=="function"&&(n.callback=function(){di(e,t),typeof a!="function"&&(ba===null?ba=new Set([this]):ba.add(this));var i=t.stack;this.componentDidCatch(t.value,{componentStack:i!==null?i:""})}),n}function hc(e,t,n){var a=e.pingCache;if(a===null){a=e.pingCache=new yp;var r=new Set;a.set(t,r)}else r=a.get(t),r===void 0&&(r=new Set,a.set(t,r));r.has(n)||(r.add(n),e=Np.bind(null,e,t,n),t.then(e,e))}function gc(e){do{var t;if((t=e.tag===13)&&(t=e.memoizedState,t=t!==null?t.dehydrated!==null:!0),t)return e;e=e.return}while(e!==null);return null}function yc(e,t,n,a,r){return(e.mode&1)===0?(e===t?e.flags|=65536:(e.flags|=128,n.flags|=131072,n.flags&=-52805,n.tag===1&&(n.alternate===null?n.tag=17:(t=ea(-1,1),t.tag=2,fa(n,t,1))),n.lanes|=1),e):(e.flags|=65536,e.lanes=r,e)}var fp=A.ReactCurrentOwner,Jt=!1;function Qt(e,t,n,a){t.child=e===null?Ol(t,null,n,a):Sr(t,e.child,n,a)}function fc(e,t,n,a,r){n=n.render;var s=t.ref;return Tr(t,r),a=ti(e,t,n,a,s,r),n=ni(),e!==null&&!Jt?(t.updateQueue=e.updateQueue,t.flags&=-2053,e.lanes&=~r,ta(e,t,r)):(et&&n&&Fo(t),t.flags|=1,Qt(e,t,a,r),t.child)}function vc(e,t,n,a,r){if(e===null){var s=n.type;return typeof s=="function"&&!Pi(s)&&s.defaultProps===void 0&&n.compare===null&&n.defaultProps===void 0?(t.tag=15,t.type=s,bc(e,t,s,a,r)):(e=oo(n.type,null,a,t,t.mode,r),e.ref=t.ref,e.return=t,t.child=e)}if(s=e.child,(e.lanes&r)===0){var i=s.memoizedProps;if(n=n.compare,n=n!==null?n:Qr,n(i,a)&&e.ref===t.ref)return ta(e,t,r)}return t.flags|=1,e=Ca(s,a),e.ref=t.ref,e.return=t,t.child=e}function bc(e,t,n,a,r){if(e!==null){var s=e.memoizedProps;if(Qr(s,a)&&e.ref===t.ref)if(Jt=!1,t.pendingProps=a=s,(e.lanes&r)!==0)(e.flags&131072)!==0&&(Jt=!0);else return t.lanes=e.lanes,ta(e,t,r)}return pi(e,t,n,a,r)}function wc(e,t,n){var a=t.pendingProps,r=a.children,s=e!==null?e.memoizedState:null;if(a.mode==="hidden")if((t.mode&1)===0)t.memoizedState={baseLanes:0,cachePool:null,transitions:null},Ve(Ir,on),on|=n;else{if((n&1073741824)===0)return e=s!==null?s.baseLanes|n:n,t.lanes=t.childLanes=1073741824,t.memoizedState={baseLanes:e,cachePool:null,transitions:null},t.updateQueue=null,Ve(Ir,on),on|=e,null;t.memoizedState={baseLanes:0,cachePool:null,transitions:null},a=s!==null?s.baseLanes:n,Ve(Ir,on),on|=a}else s!==null?(a=s.baseLanes|n,t.memoizedState=null):a=n,Ve(Ir,on),on|=a;return Qt(e,t,r,n),t.child}function kc(e,t){var n=t.ref;(e===null&&n!==null||e!==null&&e.ref!==n)&&(t.flags|=512,t.flags|=2097152)}function pi(e,t,n,a,r){var s=$t(n)?Ha:Ot.current;return s=vr(t,s),Tr(t,r),n=ti(e,t,n,a,s,r),a=ni(),e!==null&&!Jt?(t.updateQueue=e.updateQueue,t.flags&=-2053,e.lanes&=~r,ta(e,t,r)):(et&&a&&Fo(t),t.flags|=1,Qt(e,t,n,r),t.child)}function Sc(e,t,n,a,r){if($t(n)){var s=!0;Rs(t)}else s=!1;if(Tr(t,r),t.stateNode===null)Vs(e,t),dc(t,n,a),li(t,n,a,r),a=!0;else if(e===null){var i=t.stateNode,l=t.memoizedProps;i.props=l;var d=i.context,f=n.contextType;typeof f=="object"&&f!==null?f=mn(f):(f=$t(n)?Ha:Ot.current,f=vr(t,f));var E=n.getDerivedStateFromProps,I=typeof E=="function"||typeof i.getSnapshotBeforeUpdate=="function";I||typeof i.UNSAFE_componentWillReceiveProps!="function"&&typeof i.componentWillReceiveProps!="function"||(l!==a||d!==f)&&pc(t,i,a,f),ya=!1;var T=t.memoizedState;i.state=T,js(t,a,i,r),d=t.memoizedState,l!==a||T!==d||Vt.current||ya?(typeof E=="function"&&(ii(t,n,E,a),d=t.memoizedState),(l=ya||cc(t,n,l,a,T,d,f))?(I||typeof i.UNSAFE_componentWillMount!="function"&&typeof i.componentWillMount!="function"||(typeof i.componentWillMount=="function"&&i.componentWillMount(),typeof i.UNSAFE_componentWillMount=="function"&&i.UNSAFE_componentWillMount()),typeof i.componentDidMount=="function"&&(t.flags|=4194308)):(typeof i.componentDidMount=="function"&&(t.flags|=4194308),t.memoizedProps=a,t.memoizedState=d),i.props=a,i.state=d,i.context=f,a=l):(typeof i.componentDidMount=="function"&&(t.flags|=4194308),a=!1)}else{i=t.stateNode,Fl(e,t),l=t.memoizedProps,f=t.type===t.elementType?l:En(t.type,l),i.props=f,I=t.pendingProps,T=i.context,d=n.contextType,typeof d=="object"&&d!==null?d=mn(d):(d=$t(n)?Ha:Ot.current,d=vr(t,d));var W=n.getDerivedStateFromProps;(E=typeof W=="function"||typeof i.getSnapshotBeforeUpdate=="function")||typeof i.UNSAFE_componentWillReceiveProps!="function"&&typeof i.componentWillReceiveProps!="function"||(l!==I||T!==d)&&pc(t,i,a,d),ya=!1,T=t.memoizedState,i.state=T,js(t,a,i,r);var z=t.memoizedState;l!==I||T!==z||Vt.current||ya?(typeof W=="function"&&(ii(t,n,W,a),z=t.memoizedState),(f=ya||cc(t,n,f,a,T,z,d)||!1)?(E||typeof i.UNSAFE_componentWillUpdate!="function"&&typeof i.componentWillUpdate!="function"||(typeof i.componentWillUpdate=="function"&&i.componentWillUpdate(a,z,d),typeof i.UNSAFE_componentWillUpdate=="function"&&i.UNSAFE_componentWillUpdate(a,z,d)),typeof i.componentDidUpdate=="function"&&(t.flags|=4),typeof i.getSnapshotBeforeUpdate=="function"&&(t.flags|=1024)):(typeof i.componentDidUpdate!="function"||l===e.memoizedProps&&T===e.memoizedState||(t.flags|=4),typeof i.getSnapshotBeforeUpdate!="function"||l===e.memoizedProps&&T===e.memoizedState||(t.flags|=1024),t.memoizedProps=a,t.memoizedState=z),i.props=a,i.state=z,i.context=d,a=f):(typeof i.componentDidUpdate!="function"||l===e.memoizedProps&&T===e.memoizedState||(t.flags|=4),typeof i.getSnapshotBeforeUpdate!="function"||l===e.memoizedProps&&T===e.memoizedState||(t.flags|=1024),a=!1)}return ui(e,t,n,a,s,r)}function ui(e,t,n,a,r,s){kc(e,t);var i=(t.flags&128)!==0;if(!a&&!i)return r&&Al(t,n,!1),ta(e,t,s);a=t.stateNode,fp.current=t;var l=i&&typeof n.getDerivedStateFromError!="function"?null:a.render();return t.flags|=1,e!==null&&i?(t.child=Sr(t,e.child,null,s),t.child=Sr(t,null,l,s)):Qt(e,t,l,s),t.memoizedState=a.state,r&&Al(t,n,!0),t.child}function Cc(e){var t=e.stateNode;t.pendingContext?El(e,t.pendingContext,t.pendingContext!==t.context):t.context&&El(e,t.context,!1),Vo(e,t.containerInfo)}function Tc(e,t,n,a,r){return kr(),Wo(r),t.flags|=256,Qt(e,t,n,a),t.child}var mi={dehydrated:null,treeContext:null,retryLane:0};function hi(e){return{baseLanes:e,cachePool:null,transitions:null}}function Ec(e,t,n){var a=t.pendingProps,r=nt.current,s=!1,i=(t.flags&128)!==0,l;if((l=i)||(l=e!==null&&e.memoizedState===null?!1:(r&2)!==0),l?(s=!0,t.flags&=-129):(e===null||e.memoizedState!==null)&&(r|=1),Ve(nt,r&1),e===null)return Ho(t),e=t.memoizedState,e!==null&&(e=e.dehydrated,e!==null)?((t.mode&1)===0?t.lanes=1:e.data==="$!"?t.lanes=8:t.lanes=1073741824,null):(i=a.children,e=a.fallback,s?(a=t.mode,s=t.child,i={mode:"hidden",children:i},(a&1)===0&&s!==null?(s.childLanes=0,s.pendingProps=i):s=io(i,a,0,null),e=Ja(e,a,n,null),s.return=t,e.return=t,s.sibling=e,t.child=s,t.child.memoizedState=hi(n),t.memoizedState=mi,e):gi(t,i));if(r=e.memoizedState,r!==null&&(l=r.dehydrated,l!==null))return vp(e,t,i,a,l,r,n);if(s){s=a.fallback,i=t.mode,r=e.child,l=r.sibling;var d={mode:"hidden",children:a.children};return(i&1)===0&&t.child!==r?(a=t.child,a.childLanes=0,a.pendingProps=d,t.deletions=null):(a=Ca(r,d),a.subtreeFlags=r.subtreeFlags&14680064),l!==null?s=Ca(l,s):(s=Ja(s,i,n,null),s.flags|=2),s.return=t,a.return=t,a.sibling=s,t.child=a,a=s,s=t.child,i=e.child.memoizedState,i=i===null?hi(n):{baseLanes:i.baseLanes|n,cachePool:null,transitions:i.transitions},s.memoizedState=i,s.childLanes=e.childLanes&~n,t.memoizedState=mi,a}return s=e.child,e=s.sibling,a=Ca(s,{mode:"visible",children:a.children}),(t.mode&1)===0&&(a.lanes=n),a.return=t,a.sibling=null,e!==null&&(n=t.deletions,n===null?(t.deletions=[e],t.flags|=16):n.push(e)),t.child=a,t.memoizedState=null,a}function gi(e,t){return t=io({mode:"visible",children:t},e.mode,0,null),t.return=e,e.child=t}function Ys(e,t,n,a){return a!==null&&Wo(a),Sr(t,e.child,null,n),e=gi(t,t.pendingProps.children),e.flags|=2,t.memoizedState=null,e}function vp(e,t,n,a,r,s,i){if(n)return t.flags&256?(t.flags&=-257,a=ci(Error(g(422))),Ys(e,t,i,a)):t.memoizedState!==null?(t.child=e.child,t.flags|=128,null):(s=a.fallback,r=t.mode,a=io({mode:"visible",children:a.children},r,0,null),s=Ja(s,r,i,null),s.flags|=2,a.return=t,s.return=t,a.sibling=s,t.child=a,(t.mode&1)!==0&&Sr(t,e.child,null,i),t.child.memoizedState=hi(i),t.memoizedState=mi,s);if((t.mode&1)===0)return Ys(e,t,i,null);if(r.data==="$!"){if(a=r.nextSibling&&r.nextSibling.dataset,a)var l=a.dgst;return a=l,s=Error(g(419)),a=ci(s,a,void 0),Ys(e,t,i,a)}if(l=(i&e.childLanes)!==0,Jt||l){if(a=Ct,a!==null){switch(i&-i){case 4:r=2;break;case 16:r=8;break;case 64:case 128:case 256:case 512:case 1024:case 2048:case 4096:case 8192:case 16384:case 32768:case 65536:case 131072:case 262144:case 524288:case 1048576:case 2097152:case 4194304:case 8388608:case 16777216:case 33554432:case 67108864:r=32;break;case 536870912:r=268435456;break;default:r=0}r=(r&(a.suspendedLanes|i))!==0?0:r,r!==0&&r!==s.retryLane&&(s.retryLane=r,Zn(e,r),In(a,e,r,-1))}return Ri(),a=ci(Error(g(421))),Ys(e,t,i,a)}return r.data==="$?"?(t.flags|=128,t.child=e.child,t=Rp.bind(null,e),r._reactRetry=t,null):(e=s.treeContext,sn=ua(r.nextSibling),rn=t,et=!0,Tn=null,e!==null&&(pn[un++]=Jn,pn[un++]=Xn,pn[un++]=Wa,Jn=e.id,Xn=e.overflow,Wa=t),t=gi(t,a.children),t.flags|=4096,t)}function _c(e,t,n){e.lanes|=t;var a=e.alternate;a!==null&&(a.lanes|=t),Qo(e.return,t,n)}function yi(e,t,n,a,r){var s=e.memoizedState;s===null?e.memoizedState={isBackwards:t,rendering:null,renderingStartTime:0,last:a,tail:n,tailMode:r}:(s.isBackwards=t,s.rendering=null,s.renderingStartTime=0,s.last=a,s.tail=n,s.tailMode=r)}function Ac(e,t,n){var a=t.pendingProps,r=a.revealOrder,s=a.tail;if(Qt(e,t,a.children,n),a=nt.current,(a&2)!==0)a=a&1|2,t.flags|=128;else{if(e!==null&&(e.flags&128)!==0)e:for(e=t.child;e!==null;){if(e.tag===13)e.memoizedState!==null&&_c(e,n,t);else if(e.tag===19)_c(e,n,t);else if(e.child!==null){e.child.return=e,e=e.child;continue}if(e===t)break e;for(;e.sibling===null;){if(e.return===null||e.return===t)break e;e=e.return}e.sibling.return=e.return,e=e.sibling}a&=1}if(Ve(nt,a),(t.mode&1)===0)t.memoizedState=null;else switch(r){case"forwards":for(n=t.child,r=null;n!==null;)e=n.alternate,e!==null&&Hs(e)===null&&(r=n),n=n.sibling;n=r,n===null?(r=t.child,t.child=null):(r=n.sibling,n.sibling=null),yi(t,!1,r,n,s);break;case"backwards":for(n=null,r=t.child,t.child=null;r!==null;){if(e=r.alternate,e!==null&&Hs(e)===null){t.child=r;break}e=r.sibling,r.sibling=n,n=r,r=e}yi(t,!0,n,null,s);break;case"together":yi(t,!1,null,null,void 0);break;default:t.memoizedState=null}return t.child}function Vs(e,t){(t.mode&1)===0&&e!==null&&(e.alternate=null,t.alternate=null,t.flags|=2)}function ta(e,t,n){if(e!==null&&(t.dependencies=e.dependencies),za|=t.lanes,(n&t.childLanes)===0)return null;if(e!==null&&t.child!==e.child)throw Error(g(153));if(t.child!==null){for(e=t.child,n=Ca(e,e.pendingProps),t.child=n,n.return=t;e.sibling!==null;)e=e.sibling,n=n.sibling=Ca(e,e.pendingProps),n.return=t;n.sibling=null}return t.child}function bp(e,t,n){switch(t.tag){case 3:Cc(t),kr();break;case 5:Hl(t);break;case 1:$t(t.type)&&Rs(t);break;case 4:Vo(t,t.stateNode.containerInfo);break;case 10:var a=t.type._context,r=t.memoizedProps.value;Ve(Bs,a._currentValue),a._currentValue=r;break;case 13:if(a=t.memoizedState,a!==null)return a.dehydrated!==null?(Ve(nt,nt.current&1),t.flags|=128,null):(n&t.child.childLanes)!==0?Ec(e,t,n):(Ve(nt,nt.current&1),e=ta(e,t,n),e!==null?e.sibling:null);Ve(nt,nt.current&1);break;case 19:if(a=(n&t.childLanes)!==0,(e.flags&128)!==0){if(a)return Ac(e,t,n);t.flags|=128}if(r=t.memoizedState,r!==null&&(r.rendering=null,r.tail=null,r.lastEffect=null),Ve(nt,nt.current),a)break;return null;case 22:case 23:return t.lanes=0,wc(e,t,n)}return ta(e,t,n)}var Ic,fi,Lc,Nc;Ic=function(e,t){for(var n=t.child;n!==null;){if(n.tag===5||n.tag===6)e.appendChild(n.stateNode);else if(n.tag!==4&&n.child!==null){n.child.return=n,n=n.child;continue}if(n===t)break;for(;n.sibling===null;){if(n.return===null||n.return===t)return;n=n.return}n.sibling.return=n.return,n=n.sibling}},fi=function(){},Lc=function(e,t,n,a){var r=e.memoizedProps;if(r!==a){e=t.stateNode,qa(Bn.current);var s=null;switch(n){case"input":r=Pr(e,r),a=Pr(e,a),s=[];break;case"select":r=x({},r,{value:void 0}),a=x({},a,{value:void 0}),s=[];break;case"textarea":r=Et(e,r),a=Et(e,a),s=[];break;default:typeof r.onClick!="function"&&typeof a.onClick=="function"&&(e.onclick=Is)}er(n,a);var i;n=null;for(f in r)if(!a.hasOwnProperty(f)&&r.hasOwnProperty(f)&&r[f]!=null)if(f==="style"){var l=r[f];for(i in l)l.hasOwnProperty(i)&&(n||(n={}),n[i]="")}else f!=="dangerouslySetInnerHTML"&&f!=="children"&&f!=="suppressContentEditableWarning"&&f!=="suppressHydrationWarning"&&f!=="autoFocus"&&(G.hasOwnProperty(f)?s||(s=[]):(s=s||[]).push(f,null));for(f in a){var d=a[f];if(l=r!=null?r[f]:void 0,a.hasOwnProperty(f)&&d!==l&&(d!=null||l!=null))if(f==="style")if(l){for(i in l)!l.hasOwnProperty(i)||d&&d.hasOwnProperty(i)||(n||(n={}),n[i]="");for(i in d)d.hasOwnProperty(i)&&l[i]!==d[i]&&(n||(n={}),n[i]=d[i])}else n||(s||(s=[]),s.push(f,n)),n=d;else f==="dangerouslySetInnerHTML"?(d=d?d.__html:void 0,l=l?l.__html:void 0,d!=null&&l!==d&&(s=s||[]).push(f,d)):f==="children"?typeof d!="string"&&typeof d!="number"||(s=s||[]).push(f,""+d):f!=="suppressContentEditableWarning"&&f!=="suppressHydrationWarning"&&(G.hasOwnProperty(f)?(d!=null&&f==="onScroll"&&$e("scroll",e),s||l===d||(s=[])):(s=s||[]).push(f,d))}n&&(s=s||[]).push("style",n);var f=s;(t.updateQueue=f)&&(t.flags|=4)}},Nc=function(e,t,n,a){n!==a&&(t.flags|=4)};function is(e,t){if(!et)switch(e.tailMode){case"hidden":t=e.tail;for(var n=null;t!==null;)t.alternate!==null&&(n=t),t=t.sibling;n===null?e.tail=null:n.sibling=null;break;case"collapsed":n=e.tail;for(var a=null;n!==null;)n.alternate!==null&&(a=n),n=n.sibling;a===null?t||e.tail===null?e.tail=null:e.tail.sibling=null:a.sibling=null}}function Ft(e){var t=e.alternate!==null&&e.alternate.child===e.child,n=0,a=0;if(t)for(var r=e.child;r!==null;)n|=r.lanes|r.childLanes,a|=r.subtreeFlags&14680064,a|=r.flags&14680064,r.return=e,r=r.sibling;else for(r=e.child;r!==null;)n|=r.lanes|r.childLanes,a|=r.subtreeFlags,a|=r.flags,r.return=e,r=r.sibling;return e.subtreeFlags|=a,e.childLanes=n,t}function wp(e,t,n){var a=t.pendingProps;switch(Ko(t),t.tag){case 2:case 16:case 15:case 0:case 11:case 7:case 8:case 12:case 9:case 14:return Ft(t),null;case 1:return $t(t.type)&&Ns(),Ft(t),null;case 3:return a=t.stateNode,Er(),Je(Vt),Je(Ot),Xo(),a.pendingContext&&(a.context=a.pendingContext,a.pendingContext=null),(e===null||e.child===null)&&(Ds(t)?t.flags|=4:e===null||e.memoizedState.isDehydrated&&(t.flags&256)===0||(t.flags|=1024,Tn!==null&&(Ii(Tn),Tn=null))),fi(e,t),Ft(t),null;case 5:$o(t);var r=qa(ns.current);if(n=t.type,e!==null&&t.stateNode!=null)Lc(e,t,n,a,r),e.ref!==t.ref&&(t.flags|=512,t.flags|=2097152);else{if(!a){if(t.stateNode===null)throw Error(g(166));return Ft(t),null}if(e=qa(Bn.current),Ds(t)){a=t.stateNode,n=t.type;var s=t.memoizedProps;switch(a[On]=t,a[Jr]=s,e=(t.mode&1)!==0,n){case"dialog":$e("cancel",a),$e("close",a);break;case"iframe":case"object":case"embed":$e("load",a);break;case"video":case"audio":for(r=0;r<Yr.length;r++)$e(Yr[r],a);break;case"source":$e("error",a);break;case"img":case"image":case"link":$e("error",a),$e("load",a);break;case"details":$e("toggle",a);break;case"input":ys(a,s),$e("invalid",a);break;case"select":a._wrapperState={wasMultiple:!!s.multiple},$e("invalid",a);break;case"textarea":cn(a,s),$e("invalid",a)}er(n,s),r=null;for(var i in s)if(s.hasOwnProperty(i)){var l=s[i];i==="children"?typeof l=="string"?a.textContent!==l&&(s.suppressHydrationWarning!==!0&&As(a.textContent,l,e),r=["children",l]):typeof l=="number"&&a.textContent!==""+l&&(s.suppressHydrationWarning!==!0&&As(a.textContent,l,e),r=["children",""+l]):G.hasOwnProperty(i)&&l!=null&&i==="onScroll"&&$e("scroll",a)}switch(n){case"input":Hn(a),Aa(a,s,!0);break;case"textarea":Hn(a),ce(a);break;case"select":case"option":break;default:typeof s.onClick=="function"&&(a.onclick=Is)}a=r,t.updateQueue=a,a!==null&&(t.flags|=4)}else{i=r.nodeType===9?r:r.ownerDocument,e==="http://www.w3.org/1999/xhtml"&&(e=Za(n)),e==="http://www.w3.org/1999/xhtml"?n==="script"?(e=i.createElement("div"),e.innerHTML="<script><\/script>",e=e.removeChild(e.firstChild)):typeof a.is=="string"?e=i.createElement(n,{is:a.is}):(e=i.createElement(n),n==="select"&&(i=e,a.multiple?i.multiple=!0:a.size&&(i.size=a.size))):e=i.createElementNS(e,n),e[On]=t,e[Jr]=a,Ic(e,t,!1,!1),t.stateNode=e;e:{switch(i=tr(n,a),n){case"dialog":$e("cancel",e),$e("close",e),r=a;break;case"iframe":case"object":case"embed":$e("load",e),r=a;break;case"video":case"audio":for(r=0;r<Yr.length;r++)$e(Yr[r],e);r=a;break;case"source":$e("error",e),r=a;break;case"img":case"image":case"link":$e("error",e),$e("load",e),r=a;break;case"details":$e("toggle",e),r=a;break;case"input":ys(e,a),r=Pr(e,a),$e("invalid",e);break;case"option":r=a;break;case"select":e._wrapperState={wasMultiple:!!a.multiple},r=x({},a,{value:void 0}),$e("invalid",e);break;case"textarea":cn(e,a),r=Et(e,a),$e("invalid",e);break;default:r=a}er(n,r),l=r;for(s in l)if(l.hasOwnProperty(s)){var d=l[s];s==="style"?fs(e,d):s==="dangerouslySetInnerHTML"?(d=d?d.__html:void 0,d!=null&&Mr(e,d)):s==="children"?typeof d=="string"?(n!=="textarea"||d!=="")&&Fe(e,d):typeof d=="number"&&Fe(e,""+d):s!=="suppressContentEditableWarning"&&s!=="suppressHydrationWarning"&&s!=="autoFocus"&&(G.hasOwnProperty(s)?d!=null&&s==="onScroll"&&$e("scroll",e):d!=null&&ft(e,s,d,i))}switch(n){case"input":Hn(e),Aa(e,a,!1);break;case"textarea":Hn(e),ce(e);break;case"option":a.value!=null&&e.setAttribute("value",""+_e(a.value));break;case"select":e.multiple=!!a.multiple,s=a.value,s!=null?aa(e,!!a.multiple,s,!1):a.defaultValue!=null&&aa(e,!!a.multiple,a.defaultValue,!0);break;default:typeof r.onClick=="function"&&(e.onclick=Is)}switch(n){case"button":case"input":case"select":case"textarea":a=!!a.autoFocus;break e;case"img":a=!0;break e;default:a=!1}}a&&(t.flags|=4)}t.ref!==null&&(t.flags|=512,t.flags|=2097152)}return Ft(t),null;case 6:if(e&&t.stateNode!=null)Nc(e,t,e.memoizedProps,a);else{if(typeof a!="string"&&t.stateNode===null)throw Error(g(166));if(n=qa(ns.current),qa(Bn.current),Ds(t)){if(a=t.stateNode,n=t.memoizedProps,a[On]=t,(s=a.nodeValue!==n)&&(e=rn,e!==null))switch(e.tag){case 3:As(a.nodeValue,n,(e.mode&1)!==0);break;case 5:e.memoizedProps.suppressHydrationWarning!==!0&&As(a.nodeValue,n,(e.mode&1)!==0)}s&&(t.flags|=4)}else a=(n.nodeType===9?n:n.ownerDocument).createTextNode(a),a[On]=t,t.stateNode=a}return Ft(t),null;case 13:if(Je(nt),a=t.memoizedState,e===null||e.memoizedState!==null&&e.memoizedState.dehydrated!==null){if(et&&sn!==null&&(t.mode&1)!==0&&(t.flags&128)===0)xl(),kr(),t.flags|=98560,s=!1;else if(s=Ds(t),a!==null&&a.dehydrated!==null){if(e===null){if(!s)throw Error(g(318));if(s=t.memoizedState,s=s!==null?s.dehydrated:null,!s)throw Error(g(317));s[On]=t}else kr(),(t.flags&128)===0&&(t.memoizedState=null),t.flags|=4;Ft(t),s=!1}else Tn!==null&&(Ii(Tn),Tn=null),s=!0;if(!s)return t.flags&65536?t:null}return(t.flags&128)!==0?(t.lanes=n,t):(a=a!==null,a!==(e!==null&&e.memoizedState!==null)&&a&&(t.child.flags|=8192,(t.mode&1)!==0&&(e===null||(nt.current&1)!==0?gt===0&&(gt=3):Ri())),t.updateQueue!==null&&(t.flags|=4),Ft(t),null);case 4:return Er(),fi(e,t),e===null&&Vr(t.stateNode.containerInfo),Ft(t),null;case 10:return qo(t.type._context),Ft(t),null;case 17:return $t(t.type)&&Ns(),Ft(t),null;case 19:if(Je(nt),s=t.memoizedState,s===null)return Ft(t),null;if(a=(t.flags&128)!==0,i=s.rendering,i===null)if(a)is(s,!1);else{if(gt!==0||e!==null&&(e.flags&128)!==0)for(e=t.child;e!==null;){if(i=Hs(e),i!==null){for(t.flags|=128,is(s,!1),a=i.updateQueue,a!==null&&(t.updateQueue=a,t.flags|=4),t.subtreeFlags=0,a=n,n=t.child;n!==null;)s=n,e=a,s.flags&=14680066,i=s.alternate,i===null?(s.childLanes=0,s.lanes=e,s.child=null,s.subtreeFlags=0,s.memoizedProps=null,s.memoizedState=null,s.updateQueue=null,s.dependencies=null,s.stateNode=null):(s.childLanes=i.childLanes,s.lanes=i.lanes,s.child=i.child,s.subtreeFlags=0,s.deletions=null,s.memoizedProps=i.memoizedProps,s.memoizedState=i.memoizedState,s.updateQueue=i.updateQueue,s.type=i.type,e=i.dependencies,s.dependencies=e===null?null:{lanes:e.lanes,firstContext:e.firstContext}),n=n.sibling;return Ve(nt,nt.current&1|2),t.child}e=e.sibling}s.tail!==null&&c()>Lr&&(t.flags|=128,a=!0,is(s,!1),t.lanes=4194304)}else{if(!a)if(e=Hs(i),e!==null){if(t.flags|=128,a=!0,n=e.updateQueue,n!==null&&(t.updateQueue=n,t.flags|=4),is(s,!0),s.tail===null&&s.tailMode==="hidden"&&!i.alternate&&!et)return Ft(t),null}else 2*c()-s.renderingStartTime>Lr&&n!==1073741824&&(t.flags|=128,a=!0,is(s,!1),t.lanes=4194304);s.isBackwards?(i.sibling=t.child,t.child=i):(n=s.last,n!==null?n.sibling=i:t.child=i,s.last=i)}return s.tail!==null?(t=s.tail,s.rendering=t,s.tail=t.sibling,s.renderingStartTime=c(),t.sibling=null,n=nt.current,Ve(nt,a?n&1|2:n&1),t):(Ft(t),null);case 22:case 23:return Ni(),a=t.memoizedState!==null,e!==null&&e.memoizedState!==null!==a&&(t.flags|=8192),a&&(t.mode&1)!==0?(on&1073741824)!==0&&(Ft(t),t.subtreeFlags&6&&(t.flags|=8192)):Ft(t),null;case 24:return null;case 25:return null}throw Error(g(156,t.tag))}function kp(e,t){switch(Ko(t),t.tag){case 1:return $t(t.type)&&Ns(),e=t.flags,e&65536?(t.flags=e&-65537|128,t):null;case 3:return Er(),Je(Vt),Je(Ot),Xo(),e=t.flags,(e&65536)!==0&&(e&128)===0?(t.flags=e&-65537|128,t):null;case 5:return $o(t),null;case 13:if(Je(nt),e=t.memoizedState,e!==null&&e.dehydrated!==null){if(t.alternate===null)throw Error(g(340));kr()}return e=t.flags,e&65536?(t.flags=e&-65537|128,t):null;case 19:return Je(nt),null;case 4:return Er(),null;case 10:return qo(t.type._context),null;case 22:case 23:return Ni(),null;case 24:return null;default:return null}}var $s=!1,Kt=!1,Sp=typeof WeakSet=="function"?WeakSet:Set,q=null;function Ar(e,t){var n=e.ref;if(n!==null)if(typeof n=="function")try{n(null)}catch(a){rt(e,t,a)}else n.current=null}function vi(e,t,n){try{n()}catch(a){rt(e,t,a)}}var Rc=!1;function Cp(e,t){if(No=qn,e=cl(),So(e)){if("selectionStart"in e)var n={start:e.selectionStart,end:e.selectionEnd};else e:{n=(n=e.ownerDocument)&&n.defaultView||window;var a=n.getSelection&&n.getSelection();if(a&&a.rangeCount!==0){n=a.anchorNode;var r=a.anchorOffset,s=a.focusNode;a=a.focusOffset;try{n.nodeType,s.nodeType}catch{n=null;break e}var i=0,l=-1,d=-1,f=0,E=0,I=e,T=null;t:for(;;){for(var W;I!==n||r!==0&&I.nodeType!==3||(l=i+r),I!==s||a!==0&&I.nodeType!==3||(d=i+a),I.nodeType===3&&(i+=I.nodeValue.length),(W=I.firstChild)!==null;)T=I,I=W;for(;;){if(I===e)break t;if(T===n&&++f===r&&(l=i),T===s&&++E===a&&(d=i),(W=I.nextSibling)!==null)break;I=T,T=I.parentNode}I=W}n=l===-1||d===-1?null:{start:l,end:d}}else n=null}n=n||{start:0,end:0}}else n=null;for(Ro={focusedElem:e,selectionRange:n},qn=!1,q=t;q!==null;)if(t=q,e=t.child,(t.subtreeFlags&1028)!==0&&e!==null)e.return=t,q=e;else for(;q!==null;){t=q;try{var z=t.alternate;if((t.flags&1024)!==0)switch(t.tag){case 0:case 11:case 15:break;case 1:if(z!==null){var Y=z.memoizedProps,ot=z.memoizedState,h=t.stateNode,p=h.getSnapshotBeforeUpdate(t.elementType===t.type?Y:En(t.type,Y),ot);h.__reactInternalSnapshotBeforeUpdate=p}break;case 3:var y=t.stateNode.containerInfo;y.nodeType===1?y.textContent="":y.nodeType===9&&y.documentElement&&y.removeChild(y.documentElement);break;case 5:case 6:case 4:case 17:break;default:throw Error(g(163))}}catch(P){rt(t,t.return,P)}if(e=t.sibling,e!==null){e.return=t.return,q=e;break}q=t.return}return z=Rc,Rc=!1,z}function ls(e,t,n){var a=t.updateQueue;if(a=a!==null?a.lastEffect:null,a!==null){var r=a=a.next;do{if((r.tag&e)===e){var s=r.destroy;r.destroy=void 0,s!==void 0&&vi(t,n,s)}r=r.next}while(r!==a)}}function Js(e,t){if(t=t.updateQueue,t=t!==null?t.lastEffect:null,t!==null){var n=t=t.next;do{if((n.tag&e)===e){var a=n.create;n.destroy=a()}n=n.next}while(n!==t)}}function bi(e){var t=e.ref;if(t!==null){var n=e.stateNode;switch(e.tag){case 5:e=n;break;default:e=n}typeof t=="function"?t(e):t.current=e}}function Pc(e){var t=e.alternate;t!==null&&(e.alternate=null,Pc(t)),e.child=null,e.deletions=null,e.sibling=null,e.tag===5&&(t=e.stateNode,t!==null&&(delete t[On],delete t[Jr],delete t[Do],delete t[sp],delete t[op])),e.stateNode=null,e.return=null,e.dependencies=null,e.memoizedProps=null,e.memoizedState=null,e.pendingProps=null,e.stateNode=null,e.updateQueue=null}function xc(e){return e.tag===5||e.tag===3||e.tag===4}function Mc(e){e:for(;;){for(;e.sibling===null;){if(e.return===null||xc(e.return))return null;e=e.return}for(e.sibling.return=e.return,e=e.sibling;e.tag!==5&&e.tag!==6&&e.tag!==18;){if(e.flags&2||e.child===null||e.tag===4)continue e;e.child.return=e,e=e.child}if(!(e.flags&2))return e.stateNode}}function wi(e,t,n){var a=e.tag;if(a===5||a===6)e=e.stateNode,t?n.nodeType===8?n.parentNode.insertBefore(e,t):n.insertBefore(e,t):(n.nodeType===8?(t=n.parentNode,t.insertBefore(e,n)):(t=n,t.appendChild(e)),n=n._reactRootContainer,n!=null||t.onclick!==null||(t.onclick=Is));else if(a!==4&&(e=e.child,e!==null))for(wi(e,t,n),e=e.sibling;e!==null;)wi(e,t,n),e=e.sibling}function ki(e,t,n){var a=e.tag;if(a===5||a===6)e=e.stateNode,t?n.insertBefore(e,t):n.appendChild(e);else if(a!==4&&(e=e.child,e!==null))for(ki(e,t,n),e=e.sibling;e!==null;)ki(e,t,n),e=e.sibling}var At=null,_n=!1;function va(e,t,n){for(n=n.child;n!==null;)Dc(e,t,n),n=n.sibling}function Dc(e,t,n){if(ae&&typeof ae.onCommitFiberUnmount=="function")try{ae.onCommitFiberUnmount(K,n)}catch{}switch(n.tag){case 5:Kt||Ar(n,t);case 6:var a=At,r=_n;At=null,va(e,t,n),At=a,_n=r,At!==null&&(_n?(e=At,n=n.stateNode,e.nodeType===8?e.parentNode.removeChild(n):e.removeChild(n)):At.removeChild(n.stateNode));break;case 18:At!==null&&(_n?(e=At,n=n.stateNode,e.nodeType===8?Mo(e.parentNode,n):e.nodeType===1&&Mo(e,n),wn(e)):Mo(At,n.stateNode));break;case 4:a=At,r=_n,At=n.stateNode.containerInfo,_n=!0,va(e,t,n),At=a,_n=r;break;case 0:case 11:case 14:case 15:if(!Kt&&(a=n.updateQueue,a!==null&&(a=a.lastEffect,a!==null))){r=a=a.next;do{var s=r,i=s.destroy;s=s.tag,i!==void 0&&((s&2)!==0||(s&4)!==0)&&vi(n,t,i),r=r.next}while(r!==a)}va(e,t,n);break;case 1:if(!Kt&&(Ar(n,t),a=n.stateNode,typeof a.componentWillUnmount=="function"))try{a.props=n.memoizedProps,a.state=n.memoizedState,a.componentWillUnmount()}catch(l){rt(n,t,l)}va(e,t,n);break;case 21:va(e,t,n);break;case 22:n.mode&1?(Kt=(a=Kt)||n.memoizedState!==null,va(e,t,n),Kt=a):va(e,t,n);break;default:va(e,t,n)}}function Oc(e){var t=e.updateQueue;if(t!==null){e.updateQueue=null;var n=e.stateNode;n===null&&(n=e.stateNode=new Sp),t.forEach(function(a){var r=Pp.bind(null,e,a);n.has(a)||(n.add(a),a.then(r,r))})}}function An(e,t){var n=t.deletions;if(n!==null)for(var a=0;a<n.length;a++){var r=n[a];try{var s=e,i=t,l=i;e:for(;l!==null;){switch(l.tag){case 5:At=l.stateNode,_n=!1;break e;case 3:At=l.stateNode.containerInfo,_n=!0;break e;case 4:At=l.stateNode.containerInfo,_n=!0;break e}l=l.return}if(At===null)throw Error(g(160));Dc(s,i,r),At=null,_n=!1;var d=r.alternate;d!==null&&(d.return=null),r.return=null}catch(f){rt(r,t,f)}}if(t.subtreeFlags&12854)for(t=t.child;t!==null;)Bc(t,e),t=t.sibling}function Bc(e,t){var n=e.alternate,a=e.flags;switch(e.tag){case 0:case 11:case 14:case 15:if(An(t,e),Kn(e),a&4){try{ls(3,e,e.return),Js(3,e)}catch(Y){rt(e,e.return,Y)}try{ls(5,e,e.return)}catch(Y){rt(e,e.return,Y)}}break;case 1:An(t,e),Kn(e),a&512&&n!==null&&Ar(n,n.return);break;case 5:if(An(t,e),Kn(e),a&512&&n!==null&&Ar(n,n.return),e.flags&32){var r=e.stateNode;try{Fe(r,"")}catch(Y){rt(e,e.return,Y)}}if(a&4&&(r=e.stateNode,r!=null)){var s=e.memoizedProps,i=n!==null?n.memoizedProps:s,l=e.type,d=e.updateQueue;if(e.updateQueue=null,d!==null)try{l==="input"&&s.type==="radio"&&s.name!=null&&Xa(r,s),tr(l,i);var f=tr(l,s);for(i=0;i<d.length;i+=2){var E=d[i],I=d[i+1];E==="style"?fs(r,I):E==="dangerouslySetInnerHTML"?Mr(r,I):E==="children"?Fe(r,I):ft(r,E,I,f)}switch(l){case"input":_a(r,s);break;case"textarea":fn(r,s);break;case"select":var T=r._wrapperState.wasMultiple;r._wrapperState.wasMultiple=!!s.multiple;var W=s.value;W!=null?aa(r,!!s.multiple,W,!1):T!==!!s.multiple&&(s.defaultValue!=null?aa(r,!!s.multiple,s.defaultValue,!0):aa(r,!!s.multiple,s.multiple?[]:"",!1))}r[Jr]=s}catch(Y){rt(e,e.return,Y)}}break;case 6:if(An(t,e),Kn(e),a&4){if(e.stateNode===null)throw Error(g(162));r=e.stateNode,s=e.memoizedProps;try{r.nodeValue=s}catch(Y){rt(e,e.return,Y)}}break;case 3:if(An(t,e),Kn(e),a&4&&n!==null&&n.memoizedState.isDehydrated)try{wn(t.containerInfo)}catch(Y){rt(e,e.return,Y)}break;case 4:An(t,e),Kn(e);break;case 13:An(t,e),Kn(e),r=e.child,r.flags&8192&&(s=r.memoizedState!==null,r.stateNode.isHidden=s,!s||r.alternate!==null&&r.alternate.memoizedState!==null||(Ti=c())),a&4&&Oc(e);break;case 22:if(E=n!==null&&n.memoizedState!==null,e.mode&1?(Kt=(f=Kt)||E,An(t,e),Kt=f):An(t,e),Kn(e),a&8192){if(f=e.memoizedState!==null,(e.stateNode.isHidden=f)&&!E&&(e.mode&1)!==0)for(q=e,E=e.child;E!==null;){for(I=q=E;q!==null;){switch(T=q,W=T.child,T.tag){case 0:case 11:case 14:case 15:ls(4,T,T.return);break;case 1:Ar(T,T.return);var z=T.stateNode;if(typeof z.componentWillUnmount=="function"){a=T,n=T.return;try{t=a,z.props=t.memoizedProps,z.state=t.memoizedState,z.componentWillUnmount()}catch(Y){rt(a,n,Y)}}break;case 5:Ar(T,T.return);break;case 22:if(T.memoizedState!==null){jc(I);continue}}W!==null?(W.return=T,q=W):jc(I)}E=E.sibling}e:for(E=null,I=e;;){if(I.tag===5){if(E===null){E=I;try{r=I.stateNode,f?(s=r.style,typeof s.setProperty=="function"?s.setProperty("display","none","important"):s.display="none"):(l=I.stateNode,d=I.memoizedProps.style,i=d!=null&&d.hasOwnProperty("display")?d.display:null,l.style.display=Dr("display",i))}catch(Y){rt(e,e.return,Y)}}}else if(I.tag===6){if(E===null)try{I.stateNode.nodeValue=f?"":I.memoizedProps}catch(Y){rt(e,e.return,Y)}}else if((I.tag!==22&&I.tag!==23||I.memoizedState===null||I===e)&&I.child!==null){I.child.return=I,I=I.child;continue}if(I===e)break e;for(;I.sibling===null;){if(I.return===null||I.return===e)break e;E===I&&(E=null),I=I.return}E===I&&(E=null),I.sibling.return=I.return,I=I.sibling}}break;case 19:An(t,e),Kn(e),a&4&&Oc(e);break;case 21:break;default:An(t,e),Kn(e)}}function Kn(e){var t=e.flags;if(t&2){try{e:{for(var n=e.return;n!==null;){if(xc(n)){var a=n;break e}n=n.return}throw Error(g(160))}switch(a.tag){case 5:var r=a.stateNode;a.flags&32&&(Fe(r,""),a.flags&=-33);var s=Mc(e);ki(e,s,r);break;case 3:case 4:var i=a.stateNode.containerInfo,l=Mc(e);wi(e,l,i);break;default:throw Error(g(161))}}catch(d){rt(e,e.return,d)}e.flags&=-3}t&4096&&(e.flags&=-4097)}function Tp(e,t,n){q=e,Fc(e)}function Fc(e,t,n){for(var a=(e.mode&1)!==0;q!==null;){var r=q,s=r.child;if(r.tag===22&&a){var i=r.memoizedState!==null||$s;if(!i){var l=r.alternate,d=l!==null&&l.memoizedState!==null||Kt;l=$s;var f=Kt;if($s=i,(Kt=d)&&!f)for(q=r;q!==null;)i=q,d=i.child,i.tag===22&&i.memoizedState!==null?Hc(r):d!==null?(d.return=i,q=d):Hc(r);for(;s!==null;)q=s,Fc(s),s=s.sibling;q=r,$s=l,Kt=f}Kc(e)}else(r.subtreeFlags&8772)!==0&&s!==null?(s.return=r,q=s):Kc(e)}}function Kc(e){for(;q!==null;){var t=q;if((t.flags&8772)!==0){var n=t.alternate;try{if((t.flags&8772)!==0)switch(t.tag){case 0:case 11:case 15:Kt||Js(5,t);break;case 1:var a=t.stateNode;if(t.flags&4&&!Kt)if(n===null)a.componentDidMount();else{var r=t.elementType===t.type?n.memoizedProps:En(t.type,n.memoizedProps);a.componentDidUpdate(r,n.memoizedState,a.__reactInternalSnapshotBeforeUpdate)}var s=t.updateQueue;s!==null&&jl(t,s,a);break;case 3:var i=t.updateQueue;if(i!==null){if(n=null,t.child!==null)switch(t.child.tag){case 5:n=t.child.stateNode;break;case 1:n=t.child.stateNode}jl(t,i,n)}break;case 5:var l=t.stateNode;if(n===null&&t.flags&4){n=l;var d=t.memoizedProps;switch(t.type){case"button":case"input":case"select":case"textarea":d.autoFocus&&n.focus();break;case"img":d.src&&(n.src=d.src)}}break;case 6:break;case 4:break;case 12:break;case 13:if(t.memoizedState===null){var f=t.alternate;if(f!==null){var E=f.memoizedState;if(E!==null){var I=E.dehydrated;I!==null&&wn(I)}}}break;case 19:case 17:case 21:case 22:case 23:case 25:break;default:throw Error(g(163))}Kt||t.flags&512&&bi(t)}catch(T){rt(t,t.return,T)}}if(t===e){q=null;break}if(n=t.sibling,n!==null){n.return=t.return,q=n;break}q=t.return}}function jc(e){for(;q!==null;){var t=q;if(t===e){q=null;break}var n=t.sibling;if(n!==null){n.return=t.return,q=n;break}q=t.return}}function Hc(e){for(;q!==null;){var t=q;try{switch(t.tag){case 0:case 11:case 15:var n=t.return;try{Js(4,t)}catch(d){rt(t,n,d)}break;case 1:var a=t.stateNode;if(typeof a.componentDidMount=="function"){var r=t.return;try{a.componentDidMount()}catch(d){rt(t,r,d)}}var s=t.return;try{bi(t)}catch(d){rt(t,s,d)}break;case 5:var i=t.return;try{bi(t)}catch(d){rt(t,i,d)}}}catch(d){rt(t,t.return,d)}if(t===e){q=null;break}var l=t.sibling;if(l!==null){l.return=t.return,q=l;break}q=t.return}}var Ep=Math.ceil,Xs=A.ReactCurrentDispatcher,Si=A.ReactCurrentOwner,gn=A.ReactCurrentBatchConfig,Re=0,Ct=null,pt=null,It=0,on=0,Ir=ma(0),gt=0,cs=null,za=0,Zs=0,Ci=0,ds=null,Xt=null,Ti=0,Lr=1/0,na=null,eo=!1,Ei=null,ba=null,to=!1,wa=null,no=0,ps=0,_i=null,ao=-1,ro=0;function zt(){return(Re&6)!==0?c():ao!==-1?ao:ao=c()}function ka(e){return(e.mode&1)===0?1:(Re&2)!==0&&It!==0?It&-It:lp.transition!==null?(ro===0&&(ro=Oe()),ro):(e=he,e!==0||(e=window.event,e=e===void 0?16:ws(e.type)),e)}function In(e,t,n,a){if(50<ps)throw ps=0,_i=null,Error(g(185));We(e,n,a),((Re&2)===0||e!==Ct)&&(e===Ct&&((Re&2)===0&&(Zs|=n),gt===4&&Sa(e,It)),Zt(e,a),n===1&&Re===0&&(t.mode&1)===0&&(Lr=c()+500,Ps&&ga()))}function Zt(e,t){var n=e.callbackNode;Qe(e,t);var a=X(e,e===Ct?It:0);if(a===0)n!==null&&v(n),e.callbackNode=null,e.callbackPriority=0;else if(t=a&-a,e.callbackPriority!==t){if(n!=null&&v(n),t===1)e.tag===0?ip(Gc.bind(null,e)):Il(Gc.bind(null,e)),ap(function(){(Re&6)===0&&ga()}),n=null;else{switch(Ke(a)){case 1:n=N;break;case 4:n=H;break;case 16:n=O;break;case 536870912:n=F;break;default:n=O}n=Jc(n,Wc.bind(null,e))}e.callbackPriority=t,e.callbackNode=n}}function Wc(e,t){if(ao=-1,ro=0,(Re&6)!==0)throw Error(g(327));var n=e.callbackNode;if(Nr()&&e.callbackNode!==n)return null;var a=X(e,e===Ct?It:0);if(a===0)return null;if((a&30)!==0||(a&e.expiredLanes)!==0||t)t=so(e,a);else{t=a;var r=Re;Re|=2;var s=qc();(Ct!==e||It!==t)&&(na=null,Lr=c()+500,Va(e,t));do try{Ip();break}catch(l){Uc(e,l)}while(!0);Uo(),Xs.current=s,Re=r,pt!==null?t=0:(Ct=null,It=0,t=gt)}if(t!==0){if(t===2&&(r=ze(e),r!==0&&(a=r,t=Ai(e,r))),t===1)throw n=cs,Va(e,0),Sa(e,a),Zt(e,c()),n;if(t===6)Sa(e,a);else{if(r=e.current.alternate,(a&30)===0&&!_p(r)&&(t=so(e,a),t===2&&(s=ze(e),s!==0&&(a=s,t=Ai(e,s))),t===1))throw n=cs,Va(e,0),Sa(e,a),Zt(e,c()),n;switch(e.finishedWork=r,e.finishedLanes=a,t){case 0:case 1:throw Error(g(345));case 2:$a(e,Xt,na);break;case 3:if(Sa(e,a),(a&130023424)===a&&(t=Ti+500-c(),10<t)){if(X(e,0)!==0)break;if(r=e.suspendedLanes,(r&a)!==a){zt(),e.pingedLanes|=e.suspendedLanes&r;break}e.timeoutHandle=xo($a.bind(null,e,Xt,na),t);break}$a(e,Xt,na);break;case 4:if(Sa(e,a),(a&4194240)===a)break;for(t=e.eventTimes,r=-1;0<a;){var i=31-pe(a);s=1<<i,i=t[i],i>r&&(r=i),a&=~s}if(a=r,a=c()-a,a=(120>a?120:480>a?480:1080>a?1080:1920>a?1920:3e3>a?3e3:4320>a?4320:1960*Ep(a/1960))-a,10<a){e.timeoutHandle=xo($a.bind(null,e,Xt,na),a);break}$a(e,Xt,na);break;case 5:$a(e,Xt,na);break;default:throw Error(g(329))}}}return Zt(e,c()),e.callbackNode===n?Wc.bind(null,e):null}function Ai(e,t){var n=ds;return e.current.memoizedState.isDehydrated&&(Va(e,t).flags|=256),e=so(e,t),e!==2&&(t=Xt,Xt=n,t!==null&&Ii(t)),e}function Ii(e){Xt===null?Xt=e:Xt.push.apply(Xt,e)}function _p(e){for(var t=e;;){if(t.flags&16384){var n=t.updateQueue;if(n!==null&&(n=n.stores,n!==null))for(var a=0;a<n.length;a++){var r=n[a],s=r.getSnapshot;r=r.value;try{if(!Cn(s(),r))return!1}catch{return!1}}}if(n=t.child,t.subtreeFlags&16384&&n!==null)n.return=t,t=n;else{if(t===e)break;for(;t.sibling===null;){if(t.return===null||t.return===e)return!0;t=t.return}t.sibling.return=t.return,t=t.sibling}}return!0}function Sa(e,t){for(t&=~Ci,t&=~Zs,e.suspendedLanes|=t,e.pingedLanes&=~t,e=e.expirationTimes;0<t;){var n=31-pe(t),a=1<<n;e[n]=-1,t&=~a}}function Gc(e){if((Re&6)!==0)throw Error(g(327));Nr();var t=X(e,0);if((t&1)===0)return Zt(e,c()),null;var n=so(e,t);if(e.tag!==0&&n===2){var a=ze(e);a!==0&&(t=a,n=Ai(e,a))}if(n===1)throw n=cs,Va(e,0),Sa(e,t),Zt(e,c()),n;if(n===6)throw Error(g(345));return e.finishedWork=e.current.alternate,e.finishedLanes=t,$a(e,Xt,na),Zt(e,c()),null}function Li(e,t){var n=Re;Re|=1;try{return e(t)}finally{Re=n,Re===0&&(Lr=c()+500,Ps&&ga())}}function Ya(e){wa!==null&&wa.tag===0&&(Re&6)===0&&Nr();var t=Re;Re|=1;var n=gn.transition,a=he;try{if(gn.transition=null,he=1,e)return e()}finally{he=a,gn.transition=n,Re=t,(Re&6)===0&&ga()}}function Ni(){on=Ir.current,Je(Ir)}function Va(e,t){e.finishedWork=null,e.finishedLanes=0;var n=e.timeoutHandle;if(n!==-1&&(e.timeoutHandle=-1,np(n)),pt!==null)for(n=pt.return;n!==null;){var a=n;switch(Ko(a),a.tag){case 1:a=a.type.childContextTypes,a!=null&&Ns();break;case 3:Er(),Je(Vt),Je(Ot),Xo();break;case 5:$o(a);break;case 4:Er();break;case 13:Je(nt);break;case 19:Je(nt);break;case 10:qo(a.type._context);break;case 22:case 23:Ni()}n=n.return}if(Ct=e,pt=e=Ca(e.current,null),It=on=t,gt=0,cs=null,Ci=Zs=za=0,Xt=ds=null,Ua!==null){for(t=0;t<Ua.length;t++)if(n=Ua[t],a=n.interleaved,a!==null){n.interleaved=null;var r=a.next,s=n.pending;if(s!==null){var i=s.next;s.next=r,a.next=i}n.pending=a}Ua=null}return e}function Uc(e,t){do{var n=pt;try{if(Uo(),Ws.current=Qs,Gs){for(var a=at.memoizedState;a!==null;){var r=a.queue;r!==null&&(r.pending=null),a=a.next}Gs=!1}if(Qa=0,St=ht=at=null,as=!1,rs=0,Si.current=null,n===null||n.return===null){gt=1,cs=t,pt=null;break}e:{var s=e,i=n.return,l=n,d=t;if(t=It,l.flags|=32768,d!==null&&typeof d=="object"&&typeof d.then=="function"){var f=d,E=l,I=E.tag;if((E.mode&1)===0&&(I===0||I===11||I===15)){var T=E.alternate;T?(E.updateQueue=T.updateQueue,E.memoizedState=T.memoizedState,E.lanes=T.lanes):(E.updateQueue=null,E.memoizedState=null)}var W=gc(i);if(W!==null){W.flags&=-257,yc(W,i,l,s,t),W.mode&1&&hc(s,f,t),t=W,d=f;var z=t.updateQueue;if(z===null){var Y=new Set;Y.add(d),t.updateQueue=Y}else z.add(d);break e}else{if((t&1)===0){hc(s,f,t),Ri();break e}d=Error(g(426))}}else if(et&&l.mode&1){var ot=gc(i);if(ot!==null){(ot.flags&65536)===0&&(ot.flags|=256),yc(ot,i,l,s,t),Wo(_r(d,l));break e}}s=d=_r(d,l),gt!==4&&(gt=2),ds===null?ds=[s]:ds.push(s),s=i;do{switch(s.tag){case 3:s.flags|=65536,t&=-t,s.lanes|=t;var h=uc(s,d,t);Kl(s,h);break e;case 1:l=d;var p=s.type,y=s.stateNode;if((s.flags&128)===0&&(typeof p.getDerivedStateFromError=="function"||y!==null&&typeof y.componentDidCatch=="function"&&(ba===null||!ba.has(y)))){s.flags|=65536,t&=-t,s.lanes|=t;var P=mc(s,l,t);Kl(s,P);break e}}s=s.return}while(s!==null)}zc(n)}catch(J){t=J,pt===n&&n!==null&&(pt=n=n.return);continue}break}while(!0)}function qc(){var e=Xs.current;return Xs.current=Qs,e===null?Qs:e}function Ri(){(gt===0||gt===3||gt===2)&&(gt=4),Ct===null||(za&268435455)===0&&(Zs&268435455)===0||Sa(Ct,It)}function so(e,t){var n=Re;Re|=2;var a=qc();(Ct!==e||It!==t)&&(na=null,Va(e,t));do try{Ap();break}catch(r){Uc(e,r)}while(!0);if(Uo(),Re=n,Xs.current=a,pt!==null)throw Error(g(261));return Ct=null,It=0,gt}function Ap(){for(;pt!==null;)Qc(pt)}function Ip(){for(;pt!==null&&!k();)Qc(pt)}function Qc(e){var t=$c(e.alternate,e,on);e.memoizedProps=e.pendingProps,t===null?zc(e):pt=t,Si.current=null}function zc(e){var t=e;do{var n=t.alternate;if(e=t.return,(t.flags&32768)===0){if(n=wp(n,t,on),n!==null){pt=n;return}}else{if(n=kp(n,t),n!==null){n.flags&=32767,pt=n;return}if(e!==null)e.flags|=32768,e.subtreeFlags=0,e.deletions=null;else{gt=6,pt=null;return}}if(t=t.sibling,t!==null){pt=t;return}pt=t=e}while(t!==null);gt===0&&(gt=5)}function $a(e,t,n){var a=he,r=gn.transition;try{gn.transition=null,he=1,Lp(e,t,n,a)}finally{gn.transition=r,he=a}return null}function Lp(e,t,n,a){do Nr();while(wa!==null);if((Re&6)!==0)throw Error(g(327));n=e.finishedWork;var r=e.finishedLanes;if(n===null)return null;if(e.finishedWork=null,e.finishedLanes=0,n===e.current)throw Error(g(177));e.callbackNode=null,e.callbackPriority=0;var s=n.lanes|n.childLanes;if(Ye(e,s),e===Ct&&(pt=Ct=null,It=0),(n.subtreeFlags&2064)===0&&(n.flags&2064)===0||to||(to=!0,Jc(O,function(){return Nr(),null})),s=(n.flags&15990)!==0,(n.subtreeFlags&15990)!==0||s){s=gn.transition,gn.transition=null;var i=he;he=1;var l=Re;Re|=4,Si.current=null,Cp(e,n),Bc(n,e),Vd(Ro),qn=!!No,Ro=No=null,e.current=n,Tp(n),S(),Re=l,he=i,gn.transition=s}else e.current=n;if(to&&(to=!1,wa=e,no=r),s=e.pendingLanes,s===0&&(ba=null),Le(n.stateNode),Zt(e,c()),t!==null)for(a=e.onRecoverableError,n=0;n<t.length;n++)r=t[n],a(r.value,{componentStack:r.stack,digest:r.digest});if(eo)throw eo=!1,e=Ei,Ei=null,e;return(no&1)!==0&&e.tag!==0&&Nr(),s=e.pendingLanes,(s&1)!==0?e===_i?ps++:(ps=0,_i=e):ps=0,ga(),null}function Nr(){if(wa!==null){var e=Ke(no),t=gn.transition,n=he;try{if(gn.transition=null,he=16>e?16:e,wa===null)var a=!1;else{if(e=wa,wa=null,no=0,(Re&6)!==0)throw Error(g(331));var r=Re;for(Re|=4,q=e.current;q!==null;){var s=q,i=s.child;if((q.flags&16)!==0){var l=s.deletions;if(l!==null){for(var d=0;d<l.length;d++){var f=l[d];for(q=f;q!==null;){var E=q;switch(E.tag){case 0:case 11:case 15:ls(8,E,s)}var I=E.child;if(I!==null)I.return=E,q=I;else for(;q!==null;){E=q;var T=E.sibling,W=E.return;if(Pc(E),E===f){q=null;break}if(T!==null){T.return=W,q=T;break}q=W}}}var z=s.alternate;if(z!==null){var Y=z.child;if(Y!==null){z.child=null;do{var ot=Y.sibling;Y.sibling=null,Y=ot}while(Y!==null)}}q=s}}if((s.subtreeFlags&2064)!==0&&i!==null)i.return=s,q=i;else e:for(;q!==null;){if(s=q,(s.flags&2048)!==0)switch(s.tag){case 0:case 11:case 15:ls(9,s,s.return)}var h=s.sibling;if(h!==null){h.return=s.return,q=h;break e}q=s.return}}var p=e.current;for(q=p;q!==null;){i=q;var y=i.child;if((i.subtreeFlags&2064)!==0&&y!==null)y.return=i,q=y;else e:for(i=p;q!==null;){if(l=q,(l.flags&2048)!==0)try{switch(l.tag){case 0:case 11:case 15:Js(9,l)}}catch(J){rt(l,l.return,J)}if(l===i){q=null;break e}var P=l.sibling;if(P!==null){P.return=l.return,q=P;break e}q=l.return}}if(Re=r,ga(),ae&&typeof ae.onPostCommitFiberRoot=="function")try{ae.onPostCommitFiberRoot(K,e)}catch{}a=!0}return a}finally{he=n,gn.transition=t}}return!1}function Yc(e,t,n){t=_r(n,t),t=uc(e,t,1),e=fa(e,t,1),t=zt(),e!==null&&(We(e,1,t),Zt(e,t))}function rt(e,t,n){if(e.tag===3)Yc(e,e,n);else for(;t!==null;){if(t.tag===3){Yc(t,e,n);break}else if(t.tag===1){var a=t.stateNode;if(typeof t.type.getDerivedStateFromError=="function"||typeof a.componentDidCatch=="function"&&(ba===null||!ba.has(a))){e=_r(n,e),e=mc(t,e,1),t=fa(t,e,1),e=zt(),t!==null&&(We(t,1,e),Zt(t,e));break}}t=t.return}}function Np(e,t,n){var a=e.pingCache;a!==null&&a.delete(t),t=zt(),e.pingedLanes|=e.suspendedLanes&n,Ct===e&&(It&n)===n&&(gt===4||gt===3&&(It&130023424)===It&&500>c()-Ti?Va(e,0):Ci|=n),Zt(e,t)}function Vc(e,t){t===0&&((e.mode&1)===0?t=1:(t=oe,oe<<=1,(oe&130023424)===0&&(oe=4194304)));var n=zt();e=Zn(e,t),e!==null&&(We(e,t,n),Zt(e,n))}function Rp(e){var t=e.memoizedState,n=0;t!==null&&(n=t.retryLane),Vc(e,n)}function Pp(e,t){var n=0;switch(e.tag){case 13:var a=e.stateNode,r=e.memoizedState;r!==null&&(n=r.retryLane);break;case 19:a=e.stateNode;break;default:throw Error(g(314))}a!==null&&a.delete(t),Vc(e,n)}var $c;$c=function(e,t,n){if(e!==null)if(e.memoizedProps!==t.pendingProps||Vt.current)Jt=!0;else{if((e.lanes&n)===0&&(t.flags&128)===0)return Jt=!1,bp(e,t,n);Jt=(e.flags&131072)!==0}else Jt=!1,et&&(t.flags&1048576)!==0&&Ll(t,Ms,t.index);switch(t.lanes=0,t.tag){case 2:var a=t.type;Vs(e,t),e=t.pendingProps;var r=vr(t,Ot.current);Tr(t,n),r=ti(null,t,a,e,r,n);var s=ni();return t.flags|=1,typeof r=="object"&&r!==null&&typeof r.render=="function"&&r.$$typeof===void 0?(t.tag=1,t.memoizedState=null,t.updateQueue=null,$t(a)?(s=!0,Rs(t)):s=!1,t.memoizedState=r.state!==null&&r.state!==void 0?r.state:null,Yo(t),r.updater=zs,t.stateNode=r,r._reactInternals=t,li(t,a,e,n),t=ui(null,t,a,!0,s,n)):(t.tag=0,et&&s&&Fo(t),Qt(null,t,r,n),t=t.child),t;case 16:a=t.elementType;e:{switch(Vs(e,t),e=t.pendingProps,r=a._init,a=r(a._payload),t.type=a,r=t.tag=Mp(a),e=En(a,e),r){case 0:t=pi(null,t,a,e,n);break e;case 1:t=Sc(null,t,a,e,n);break e;case 11:t=fc(null,t,a,e,n);break e;case 14:t=vc(null,t,a,En(a.type,e),n);break e}throw Error(g(306,a,""))}return t;case 0:return a=t.type,r=t.pendingProps,r=t.elementType===a?r:En(a,r),pi(e,t,a,r,n);case 1:return a=t.type,r=t.pendingProps,r=t.elementType===a?r:En(a,r),Sc(e,t,a,r,n);case 3:e:{if(Cc(t),e===null)throw Error(g(387));a=t.pendingProps,s=t.memoizedState,r=s.element,Fl(e,t),js(t,a,null,n);var i=t.memoizedState;if(a=i.element,s.isDehydrated)if(s={element:a,isDehydrated:!1,cache:i.cache,pendingSuspenseBoundaries:i.pendingSuspenseBoundaries,transitions:i.transitions},t.updateQueue.baseState=s,t.memoizedState=s,t.flags&256){r=_r(Error(g(423)),t),t=Tc(e,t,a,n,r);break e}else if(a!==r){r=_r(Error(g(424)),t),t=Tc(e,t,a,n,r);break e}else for(sn=ua(t.stateNode.containerInfo.firstChild),rn=t,et=!0,Tn=null,n=Ol(t,null,a,n),t.child=n;n;)n.flags=n.flags&-3|4096,n=n.sibling;else{if(kr(),a===r){t=ta(e,t,n);break e}Qt(e,t,a,n)}t=t.child}return t;case 5:return Hl(t),e===null&&Ho(t),a=t.type,r=t.pendingProps,s=e!==null?e.memoizedProps:null,i=r.children,Po(a,r)?i=null:s!==null&&Po(a,s)&&(t.flags|=32),kc(e,t),Qt(e,t,i,n),t.child;case 6:return e===null&&Ho(t),null;case 13:return Ec(e,t,n);case 4:return Vo(t,t.stateNode.containerInfo),a=t.pendingProps,e===null?t.child=Sr(t,null,a,n):Qt(e,t,a,n),t.child;case 11:return a=t.type,r=t.pendingProps,r=t.elementType===a?r:En(a,r),fc(e,t,a,r,n);case 7:return Qt(e,t,t.pendingProps,n),t.child;case 8:return Qt(e,t,t.pendingProps.children,n),t.child;case 12:return Qt(e,t,t.pendingProps.children,n),t.child;case 10:e:{if(a=t.type._context,r=t.pendingProps,s=t.memoizedProps,i=r.value,Ve(Bs,a._currentValue),a._currentValue=i,s!==null)if(Cn(s.value,i)){if(s.children===r.children&&!Vt.current){t=ta(e,t,n);break e}}else for(s=t.child,s!==null&&(s.return=t);s!==null;){var l=s.dependencies;if(l!==null){i=s.child;for(var d=l.firstContext;d!==null;){if(d.context===a){if(s.tag===1){d=ea(-1,n&-n),d.tag=2;var f=s.updateQueue;if(f!==null){f=f.shared;var E=f.pending;E===null?d.next=d:(d.next=E.next,E.next=d),f.pending=d}}s.lanes|=n,d=s.alternate,d!==null&&(d.lanes|=n),Qo(s.return,n,t),l.lanes|=n;break}d=d.next}}else if(s.tag===10)i=s.type===t.type?null:s.child;else if(s.tag===18){if(i=s.return,i===null)throw Error(g(341));i.lanes|=n,l=i.alternate,l!==null&&(l.lanes|=n),Qo(i,n,t),i=s.sibling}else i=s.child;if(i!==null)i.return=s;else for(i=s;i!==null;){if(i===t){i=null;break}if(s=i.sibling,s!==null){s.return=i.return,i=s;break}i=i.return}s=i}Qt(e,t,r.children,n),t=t.child}return t;case 9:return r=t.type,a=t.pendingProps.children,Tr(t,n),r=mn(r),a=a(r),t.flags|=1,Qt(e,t,a,n),t.child;case 14:return a=t.type,r=En(a,t.pendingProps),r=En(a.type,r),vc(e,t,a,r,n);case 15:return bc(e,t,t.type,t.pendingProps,n);case 17:return a=t.type,r=t.pendingProps,r=t.elementType===a?r:En(a,r),Vs(e,t),t.tag=1,$t(a)?(e=!0,Rs(t)):e=!1,Tr(t,n),dc(t,a,r),li(t,a,r,n),ui(null,t,a,!0,e,n);case 19:return Ac(e,t,n);case 22:return wc(e,t,n)}throw Error(g(156,t.tag))};function Jc(e,t){return u(e,t)}function xp(e,t,n,a){this.tag=e,this.key=n,this.sibling=this.child=this.return=this.stateNode=this.type=this.elementType=null,this.index=0,this.ref=null,this.pendingProps=t,this.dependencies=this.memoizedState=this.updateQueue=this.memoizedProps=null,this.mode=a,this.subtreeFlags=this.flags=0,this.deletions=null,this.childLanes=this.lanes=0,this.alternate=null}function yn(e,t,n,a){return new xp(e,t,n,a)}function Pi(e){return e=e.prototype,!(!e||!e.isReactComponent)}function Mp(e){if(typeof e=="function")return Pi(e)?1:0;if(e!=null){if(e=e.$$typeof,e===ln)return 11;if(e===ut)return 14}return 2}function Ca(e,t){var n=e.alternate;return n===null?(n=yn(e.tag,t,e.key,e.mode),n.elementType=e.elementType,n.type=e.type,n.stateNode=e.stateNode,n.alternate=e,e.alternate=n):(n.pendingProps=t,n.type=e.type,n.flags=0,n.subtreeFlags=0,n.deletions=null),n.flags=e.flags&14680064,n.childLanes=e.childLanes,n.lanes=e.lanes,n.child=e.child,n.memoizedProps=e.memoizedProps,n.memoizedState=e.memoizedState,n.updateQueue=e.updateQueue,t=e.dependencies,n.dependencies=t===null?null:{lanes:t.lanes,firstContext:t.firstContext},n.sibling=e.sibling,n.index=e.index,n.ref=e.ref,n}function oo(e,t,n,a,r,s){var i=2;if(a=e,typeof e=="function")Pi(e)&&(i=1);else if(typeof e=="string")i=5;else e:switch(e){case Ee:return Ja(n.children,r,s,t);case Xe:i=8,r|=8;break;case Me:return e=yn(12,n,t,r|2),e.elementType=Me,e.lanes=s,e;case ue:return e=yn(13,n,t,r),e.elementType=ue,e.lanes=s,e;case tn:return e=yn(19,n,t,r),e.elementType=tn,e.lanes=s,e;case qe:return io(n,r,s,t);default:if(typeof e=="object"&&e!==null)switch(e.$$typeof){case vt:i=10;break e;case Lt:i=9;break e;case ln:i=11;break e;case ut:i=14;break e;case tt:i=16,a=null;break e}throw Error(g(130,e==null?e:typeof e,""))}return t=yn(i,n,t,r),t.elementType=e,t.type=a,t.lanes=s,t}function Ja(e,t,n,a){return e=yn(7,e,a,t),e.lanes=n,e}function io(e,t,n,a){return e=yn(22,e,a,t),e.elementType=qe,e.lanes=n,e.stateNode={isHidden:!1},e}function xi(e,t,n){return e=yn(6,e,null,t),e.lanes=n,e}function Mi(e,t,n){return t=yn(4,e.children!==null?e.children:[],e.key,t),t.lanes=n,t.stateNode={containerInfo:e.containerInfo,pendingChildren:null,implementation:e.implementation},t}function Dp(e,t,n,a,r){this.tag=t,this.containerInfo=e,this.finishedWork=this.pingCache=this.current=this.pendingChildren=null,this.timeoutHandle=-1,this.callbackNode=this.pendingContext=this.context=null,this.callbackPriority=0,this.eventTimes=Be(0),this.expirationTimes=Be(-1),this.entangledLanes=this.finishedLanes=this.mutableReadLanes=this.expiredLanes=this.pingedLanes=this.suspendedLanes=this.pendingLanes=0,this.entanglements=Be(0),this.identifierPrefix=a,this.onRecoverableError=r,this.mutableSourceEagerHydrationData=null}function Di(e,t,n,a,r,s,i,l,d){return e=new Dp(e,t,n,l,d),t===1?(t=1,s===!0&&(t|=8)):t=0,s=yn(3,null,null,t),e.current=s,s.stateNode=e,s.memoizedState={element:a,isDehydrated:n,cache:null,transitions:null,pendingSuspenseBoundaries:null},Yo(s),e}function Op(e,t,n){var a=3<arguments.length&&arguments[3]!==void 0?arguments[3]:null;return{$$typeof:$,key:a==null?null:""+a,children:e,containerInfo:t,implementation:n}}function Xc(e){if(!e)return ha;e=e._reactInternals;e:{if(dn(e)!==e||e.tag!==1)throw Error(g(170));var t=e;do{switch(t.tag){case 3:t=t.stateNode.context;break e;case 1:if($t(t.type)){t=t.stateNode.__reactInternalMemoizedMergedChildContext;break e}}t=t.return}while(t!==null);throw Error(g(171))}if(e.tag===1){var n=e.type;if($t(n))return _l(e,n,t)}return t}function Zc(e,t,n,a,r,s,i,l,d){return e=Di(n,a,!0,e,r,s,i,l,d),e.context=Xc(null),n=e.current,a=zt(),r=ka(n),s=ea(a,r),s.callback=t??null,fa(n,s,r),e.current.lanes=r,We(e,r,a),Zt(e,a),e}function lo(e,t,n,a){var r=t.current,s=zt(),i=ka(r);return n=Xc(n),t.context===null?t.context=n:t.pendingContext=n,t=ea(s,i),t.payload={element:e},a=a===void 0?null:a,a!==null&&(t.callback=a),e=fa(r,t,i),e!==null&&(In(e,r,i,s),Ks(e,r,i)),i}function co(e){if(e=e.current,!e.child)return null;switch(e.child.tag){case 5:return e.child.stateNode;default:return e.child.stateNode}}function ed(e,t){if(e=e.memoizedState,e!==null&&e.dehydrated!==null){var n=e.retryLane;e.retryLane=n!==0&&n<t?n:t}}function Oi(e,t){ed(e,t),(e=e.alternate)&&ed(e,t)}function Bp(){return null}var td=typeof reportError=="function"?reportError:function(e){console.error(e)};function Bi(e){this._internalRoot=e}po.prototype.render=Bi.prototype.render=function(e){var t=this._internalRoot;if(t===null)throw Error(g(409));lo(e,t,null,null)},po.prototype.unmount=Bi.prototype.unmount=function(){var e=this._internalRoot;if(e!==null){this._internalRoot=null;var t=e.containerInfo;Ya(function(){lo(null,e,null,null)}),t[Vn]=null}};function po(e){this._internalRoot=e}po.prototype.unstable_scheduleHydration=function(e){if(e){var t=Pt();e={blockedOn:null,target:e,priority:t};for(var n=0;n<dt.length&&t!==0&&t<dt[n].priority;n++);dt.splice(n,0,e),n===0&&Gt(e)}};function Fi(e){return!(!e||e.nodeType!==1&&e.nodeType!==9&&e.nodeType!==11)}function uo(e){return!(!e||e.nodeType!==1&&e.nodeType!==9&&e.nodeType!==11&&(e.nodeType!==8||e.nodeValue!==" react-mount-point-unstable "))}function nd(){}function Fp(e,t,n,a,r){if(r){if(typeof a=="function"){var s=a;a=function(){var f=co(i);s.call(f)}}var i=Zc(t,a,e,0,null,!1,!1,"",nd);return e._reactRootContainer=i,e[Vn]=i.current,Vr(e.nodeType===8?e.parentNode:e),Ya(),i}for(;r=e.lastChild;)e.removeChild(r);if(typeof a=="function"){var l=a;a=function(){var f=co(d);l.call(f)}}var d=Di(e,0,!1,null,null,!1,!1,"",nd);return e._reactRootContainer=d,e[Vn]=d.current,Vr(e.nodeType===8?e.parentNode:e),Ya(function(){lo(t,d,n,a)}),d}function mo(e,t,n,a,r){var s=n._reactRootContainer;if(s){var i=s;if(typeof r=="function"){var l=r;r=function(){var d=co(i);l.call(d)}}lo(t,i,e,r)}else i=Fp(n,t,e,r,a);return co(i)}mt=function(e){switch(e.tag){case 3:var t=e.stateNode;if(t.current.memoizedState.isDehydrated){var n=U(t.pendingLanes);n!==0&&(Ze(t,n|1),Zt(t,c()),(Re&6)===0&&(Lr=c()+500,ga()))}break;case 13:Ya(function(){var a=Zn(e,1);if(a!==null){var r=zt();In(a,e,1,r)}}),Oi(e,1)}},Rt=function(e){if(e.tag===13){var t=Zn(e,134217728);if(t!==null){var n=zt();In(t,e,134217728,n)}Oi(e,134217728)}},an=function(e){if(e.tag===13){var t=ka(e),n=Zn(e,t);if(n!==null){var a=zt();In(n,e,t,a)}Oi(e,t)}},Pt=function(){return he},ct=function(e,t){var n=he;try{return he=e,t()}finally{he=n}},Br=function(e,t,n){switch(t){case"input":if(_a(e,n),t=n.name,n.type==="radio"&&t!=null){for(n=e;n.parentNode;)n=n.parentNode;for(n=n.querySelectorAll("input[name="+JSON.stringify(""+t)+'][type="radio"]'),t=0;t<n.length;t++){var a=n[t];if(a!==e&&a.form===e.form){var r=Ls(a);if(!r)throw Error(g(90));Rr(a),_a(a,r)}}}break;case"textarea":fn(e,n);break;case"select":t=n.value,t!=null&&aa(e,!!n.multiple,t,!1)}},Fr=Li,lt=Ya;var Kp={usingClientEntryPoint:!1,Events:[Xr,yr,Ls,vs,wt,Li]},us={findFiberByHostInstance:ja,bundleType:0,version:"18.3.1",rendererPackageName:"react-dom"},jp={bundleType:us.bundleType,version:us.version,rendererPackageName:us.rendererPackageName,rendererConfig:us.rendererConfig,overrideHookState:null,overrideHookStateDeletePath:null,overrideHookStateRenamePath:null,overrideProps:null,overridePropsDeletePath:null,overridePropsRenamePath:null,setErrorHandler:null,setSuspenseHandler:null,scheduleUpdate:null,currentDispatcherRef:A.ReactCurrentDispatcher,findHostInstanceByFiber:function(e){return e=ia(e),e===null?null:e.stateNode},findFiberByHostInstance:us.findFiberByHostInstance||Bp,findHostInstancesForRefresh:null,scheduleRefresh:null,scheduleRoot:null,setRefreshHandler:null,getCurrentFiber:null,reconcilerVersion:"18.3.1-next-f1338f8080-20240426"};if(typeof __REACT_DEVTOOLS_GLOBAL_HOOK__<"u"){var ho=__REACT_DEVTOOLS_GLOBAL_HOOK__;if(!ho.isDisabled&&ho.supportsFiber)try{K=ho.inject(jp),ae=ho}catch{}}return en.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED=Kp,en.createPortal=function(e,t){var n=2<arguments.length&&arguments[2]!==void 0?arguments[2]:null;if(!Fi(t))throw Error(g(200));return Op(e,t,null,n)},en.createRoot=function(e,t){if(!Fi(e))throw Error(g(299));var n=!1,a="",r=td;return t!=null&&(t.unstable_strictMode===!0&&(n=!0),t.identifierPrefix!==void 0&&(a=t.identifierPrefix),t.onRecoverableError!==void 0&&(r=t.onRecoverableError)),t=Di(e,1,!1,null,null,n,!1,a,r),e[Vn]=t.current,Vr(e.nodeType===8?e.parentNode:e),new Bi(t)},en.findDOMNode=function(e){if(e==null)return null;if(e.nodeType===1)return e;var t=e._reactInternals;if(t===void 0)throw typeof e.render=="function"?Error(g(188)):(e=Object.keys(e).join(","),Error(g(268,e)));return e=ia(t),e=e===null?null:e.stateNode,e},en.flushSync=function(e){return Ya(e)},en.hydrate=function(e,t,n){if(!uo(t))throw Error(g(200));return mo(null,e,t,!0,n)},en.hydrateRoot=function(e,t,n){if(!Fi(e))throw Error(g(405));var a=n!=null&&n.hydratedSources||null,r=!1,s="",i=td;if(n!=null&&(n.unstable_strictMode===!0&&(r=!0),n.identifierPrefix!==void 0&&(s=n.identifierPrefix),n.onRecoverableError!==void 0&&(i=n.onRecoverableError)),t=Zc(t,null,e,1,n??null,r,!1,s,i),e[Vn]=t.current,Vr(e),a)for(e=0;e<a.length;e++)n=a[e],r=n._getVersion,r=r(n._source),t.mutableSourceEagerHydrationData==null?t.mutableSourceEagerHydrationData=[n,r]:t.mutableSourceEagerHydrationData.push(n,r);return new po(t)},en.render=function(e,t,n){if(!uo(t))throw Error(g(200));return mo(null,e,t,!1,n)},en.unmountComponentAtNode=function(e){if(!uo(e))throw Error(g(40));return e._reactRootContainer?(Ya(function(){mo(null,null,e,!1,function(){e._reactRootContainer=null,e[Vn]=null})}),!0):!1},en.unstable_batchedUpdates=Li,en.unstable_renderSubtreeIntoContainer=function(e,t,n,a){if(!uo(n))throw Error(g(200));if(e==null||e._reactInternals===void 0)throw Error(g(38));return mo(e,t,n,!1,a)},en.version="18.3.1-next-f1338f8080-20240426",en}var dd;function Yp(){if(dd)return Hi.exports;dd=1;function w(){if(!(typeof __REACT_DEVTOOLS_GLOBAL_HOOK__>"u"||typeof __REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE!="function"))try{__REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE(w)}catch(D){console.error(D)}}return w(),Hi.exports=zp(),Hi.exports}var pd;function Vp(){if(pd)return go;pd=1;var w=Yp();return go.createRoot=w.createRoot,go.hydrateRoot=w.hydrateRoot,go}var ud=Vp(),Ea=Qi();const $p="/",Jp="/architectures",Xp="/ontology",Zp="/benchmarks",eu="/benchmarks/",tu="v1.89.5",nu="/version.json",md="v",au="footerCeVersion",ru={cache:"no-cache"},su=({currentRoute:w})=>{const[D,g]=Ea.useState(tu);Ea.useEffect(()=>{fetch(nu,ru).then(Z=>Z.ok?Z.json():null).then(Z=>{const de=Z&&(Z.tag_name||Z.version);if(de){const we=de.startsWith(md)?de:`${md}${de}`;g(we);const b=document.getElementById(au);b&&(b.textContent=`CodeExplorer CLI & Extension ${we}`)}}).catch(()=>{})},[w]);const V=w===$p,G=w===Jp,re=w===Xp,be=w===Zp||w.startsWith(eu),ye=V?"#install":"/#install";return R.jsx("header",{className:"slab-header",children:R.jsxs("nav",{className:"wrap slab-bar","aria-label":"Primary",children:[R.jsxs("a",{href:"/",className:"brand-lockup","aria-label":"CodeExplorer home",children:[R.jsx("span",{className:"brand-mark",children:"ce"}),R.jsx("span",{children:"CodeExplorer"})]}),R.jsxs("ul",{className:"slab-links",children:[R.jsx("li",{children:R.jsx("a",{href:"/architectures","aria-current":G?"page":void 0,children:"Architectures"})}),R.jsx("li",{children:R.jsx("a",{href:"/ontology","aria-current":re?"page":void 0,children:"Ontology"})}),R.jsx("li",{children:R.jsx("a",{href:"/benchmarks","aria-current":be?"page":void 0,children:"Benchmarks"})}),R.jsx("li",{className:"slab-anchor-item",children:R.jsx("a",{href:ye,children:"Install"})})]}),R.jsxs("div",{className:"slab-actions",children:[R.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/releases/latest",target:"_blank",rel:"noopener",className:"btn",id:"heroReleaseBadge","aria-label":"Latest CodeExplorer release",children:D}),R.jsx("a",{href:"https://github.com/vmikhailov/code-explorer",target:"_blank",rel:"noopener",className:"btn",children:"GitHub"})]})]})})},ou="CodeExplorer CLI & Extension v1.89.5",iu=({versionLabel:w=ou})=>R.jsx("footer",{className:"mast-footer",children:R.jsxs("div",{className:"wrap",children:[R.jsxs("div",{className:"mast-top",children:[R.jsxs("div",{className:"mast-col",children:[R.jsxs("a",{href:"/",className:"brand-lockup",children:[R.jsx("span",{className:"brand-mark",children:"ce"}),R.jsx("span",{children:"CodeExplorer"})]}),R.jsx("p",{className:"panel-copy",children:"Live architecture maps in VS Code, graph memory for AI agents via MCP, and deterministic CI gates."})]}),R.jsxs("div",{className:"mast-col",children:[R.jsx("span",{className:"mast-heading",children:"Distribution"}),R.jsxs("ul",{className:"mast-list",children:[R.jsx("li",{children:R.jsx("a",{href:"https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode",target:"_blank",rel:"noopener",children:"VS Code Marketplace"})}),R.jsx("li",{children:R.jsx("a",{href:"https://open-vsx.org/extension/vmikhailov/code-explorer-vscode",target:"_blank",rel:"noopener",children:"Open VSX Registry"})}),R.jsx("li",{children:R.jsx("a",{href:"https://github.com/vmikhailov/homebrew-tap",target:"_blank",rel:"noopener",children:"Homebrew Tap"})}),R.jsx("li",{children:R.jsx("a",{href:"https://www.nuget.org/packages/codeexplorer.cli",target:"_blank",rel:"noopener",children:"NuGet Global Tool"})})]})]}),R.jsxs("div",{className:"mast-col",children:[R.jsx("span",{className:"mast-heading",children:"Architecture & Docs"}),R.jsxs("ul",{className:"mast-list",children:[R.jsx("li",{children:R.jsx("a",{href:"/architectures",children:"Architecture Encyclopedia"})}),R.jsx("li",{children:R.jsx("a",{href:"/ontology",children:"Graph Ontology Map"})}),R.jsx("li",{children:R.jsx("a",{href:"/benchmarks",children:"Competitor Benchmarks"})}),R.jsx("li",{children:R.jsx("a",{href:"https://passport.code-explorer.app",target:"_blank",rel:"noopener",className:"tone-accent",children:"CodePassport"})}),R.jsx("li",{children:R.jsx("a",{href:"/presentation.html",target:"_blank",rel:"noopener",children:"Interactive Presentation"})}),R.jsx("li",{children:R.jsx("a",{href:"/llms.txt",target:"_blank",rel:"noopener",children:"llms.txt for Agents"})})]})]}),R.jsxs("div",{className:"mast-col",children:[R.jsx("span",{className:"mast-heading",children:"Project & Legal"}),R.jsxs("ul",{className:"mast-list",children:[R.jsx("li",{children:R.jsx("a",{href:"https://github.com/vmikhailov/code-explorer",target:"_blank",rel:"noopener",children:"GitHub Repository"})}),R.jsx("li",{children:R.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/releases",target:"_blank",rel:"noopener",children:"Release Notes"})}),R.jsx("li",{children:R.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/issues",target:"_blank",rel:"noopener",children:"Issue Tracker"})}),R.jsx("li",{children:R.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/blob/main/LICENSE",target:"_blank",rel:"noopener",children:"MIT License"})}),R.jsx("li",{children:R.jsx("a",{href:"/privacy",children:"Privacy Policy"})})]})]})]}),R.jsxs("div",{className:"mast-bottom",children:[R.jsx("span",{children:"Built by Viacheslav Mikhailov, 100% local execution"}),R.jsx("span",{className:"measured",id:"footerCeVersion",children:w})]})]})}),lu=({currentRoute:w,children:D,afterFooter:g})=>R.jsxs(R.Fragment,{children:[R.jsx(su,{currentRoute:w}),D,R.jsx(iu,{}),g]}),jn={"/":{routePath:"/",pageKind:"home",title:"CodeExplorer (ce): Live Architecture Maps, Agent MCP & CI Gates",description:"Map services and SQL tables straight from code in VS Code, give AI coding agents instant architectural memory via MCP, and block architectural drift in CI.",canonical:"https://code-explorer.app/",ogUrl:"https://code-explorer.app/",ogTitle:"CodeExplorer (ce): Live Architecture Maps, Agent MCP & CI Gates",ogDescription:"Live architecture maps in VS Code, instant MCP graph memory for AI coding agents, and deterministic CI guardrails.",mainHtml:`<!-- Stage Hero: Value Prop + Primary Install + Live Cockpit -->
    <section class="hero-stage" id="cockpit">
      <div class="wrap">
        <div class="stage-grid">
          <div class="stage-copy">
            <h1 class="stage-title">Live architecture for you and <span class="tone-accent">your agent.</span></h1>
            <p class="stage-lede">
              Map services from code, guide AI agents via MCP, and block drift.
            </p>
            <div class="stage-actions">
              <div class="stage-controls">
                <a href="https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode" target="_blank" rel="noopener" class="btn btn--primary">Install in VS Code</a>
                <a href="/benchmarks" class="btn">Benchmarks</a>
              </div>
              <button type="button" class="cmd-strip" onclick="copyInstallCmd('brew install vmikhailov/tap/ce')" aria-label="Copy Homebrew install command">
                <span class="cmd-prompt">$</span>
                <span>brew install vmikhailov/tap/ce</span>
                <span class="cmd-copy-tag">Copy</span>
              </button>
              <ul class="lang-tags" aria-label="Supported languages">
                <li>C#</li>
                <li>Java</li>
                <li>Kotlin</li>
                <li>TypeScript</li>
                <li>Go</li>
                <li>Python</li>
                <li>Rust</li>
                <li>PHP</li>
                <li>Perl</li>
                <li>SQL</li>
                <li>Terraform</li>
              </ul>
            </div>
          </div>

          <div class="workbench" data-slot="cockpit" data-kind="product">
            <div class="workbench-bar">
              <div class="workbench-tabs" role="tablist" aria-label="Architecture Cockpit views">
                <button type="button" class="tab-btn active" onclick="switchShowcase(0, this)">1. System Map</button>
                <button type="button" class="tab-btn" onclick="switchShowcase(1, this)">2. Blast Radius</button>
                <button type="button" class="tab-btn" onclick="switchShowcase(2, this)">3. Call Flows</button>
              </div>
              <span class="workbench-readout"><code id="showcasePathLabel">all_services.png</code></span>
            </div>

            <div class="cockpit-viewport" onclick="openLightbox()" role="button" tabindex="0" aria-label="Enlarge Architecture Cockpit screenshot" onkeydown="if(event.key==='Enter'||event.key===' '){event.preventDefault();openLightbox();}">
              <img id="showcaseImage" src="all_services.png" alt="CodeExplorer Interactive Architecture Cockpit in VS Code" fetchpriority="high" width="1920" height="1080">
            </div>

            <div class="cockpit-footer">
              <p class="cockpit-caption" id="showcaseDescription">
                <strong>System Map:</strong> Services, HTTP/gRPC routes, queues, and SQL tables built straight from source code.
              </p>
              <div class="cockpit-metrics" id="showcasePills">
                <span class="metric-chip metric-chip--accent">Synced with code</span>
                <span class="metric-chip metric-chip--ok">Local SQLite</span>
                <span class="metric-chip">Click to zoom</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Section 1: Before vs After Agent Proof -->
    <section class="band band--deep" id="benchmark">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">Stop paying agents to grep in the dark.</h2>
          <p class="band-lede">
            Paired agent runs on production monorepos and multi-service codebases.
          </p>
        </div>

        <div class="diff-layout">
          <div class="diff-stack" data-slot="benchmark-diff">
            <div class="diff-surface">
              <div class="diff-head">
                <span data-bind-html="index.cs01_head_html">Case 01: <code>twentyhq/twenty</code> (24,527 files)</span>
                <a href="/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty">Report</a>
              </div>
              <div class="diff-row diff-row--minus">
                <span class="diff-sign">&minus;</span>
                <span><code>codebase-memory-mcp</code> + AST scripts (<span data-bind="index.cs01_cbm_rec">3.4% (13/381) from CBM graph</span>)</span>
                <span class="diff-val" data-bind="index.cs01_cbm_val">9m 15s, 28 turns, ~1.7M tokens</span>
              </div>
              <div class="diff-row diff-row--plus">
                <span class="diff-sign">+</span>
                <span><code>CodeExplorer MCP</code> (<span data-bind="index.cs01_ce_rec">100% (381/381) directly from graph.db</span>)</span>
                <span class="diff-val tone-ok" data-bind="index.cs01_ce_val">1m 33s (~6.0× faster), 7 turns, ~242K tokens</span>
              </div>
            </div>

            <div class="diff-surface">
              <div class="diff-head">
                <span data-bind-html="index.cs02_head_html">Case 02: 324-service workspace (13,782 files)</span>
                <a href="/benchmarks/02-enterprise-microservices-deep-audit">Report</a>
              </div>
              <div class="diff-row diff-row--minus">
                <span class="diff-sign">&minus;</span>
                <span data-bind-html="index.cs02_rg_label_html"><code>ripgrep</code> + Python scripts (missed 9 services)</span>
                <span class="diff-val" data-bind="index.cs02_rg_val">29m 52s, 285 turns, ~17.2M tokens ($1.15)</span>
              </div>
              <div class="diff-row diff-row--plus">
                <span class="diff-sign">+</span>
                <span data-bind-html="index.cs02_ce_label"><code>CodeExplorer</code> (10 CLI commands + 16 file reads, 0 scripts, 0 compactions)</span>
                <span class="diff-val tone-ok" data-bind="index.cs02_ce_val">4m 16s (~7.0× faster), 18 turns, ~686K tokens ($0.10)</span>
              </div>
            </div>
          </div>

          <div class="proof-ledger" data-slot="benchmark-ledger">
            <div class="proof-stat">
              <span class="reading tone-accent" data-bind="index.card_token_comp">~7.0×</span>
              <h3 class="proof-label">Fewer input tokens</h3>
              <p class="panel-copy" data-bind="index.card_token_copy">Reduced cumulative input tokens from ~1.68M to ~242K on twentyhq/twenty (and ~25× on 324 services).</p>
            </div>

            <div class="proof-stat">
              <span class="reading tone-ok" data-bind="index.card_arch_recall">~98%</span>
              <h3 class="proof-label">SQL and queue lineage recall</h3>
              <p class="panel-copy">Captures ORM queries, raw SQL tables, and message broker topics missed by AST-only indexers.</p>
            </div>

            <div>
              <a href="/benchmarks" class="btn">Explore All Benchmarks</a>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Section 2: Interactive Cypher Query & Lineage Playground -->
    <section class="band" id="playground">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">Trace callers, routes, and SQL tables.</h2>
          <p class="band-lede">
            Query cross-service dependencies in Cypher and get deterministic answers in milliseconds.
          </p>
        </div>

        <div class="workbench" data-slot="cypher-workbench">
          <div class="workbench-bar">
            <div class="workbench-tabs" role="tablist" aria-label="Cypher query presets">
              <button type="button" class="preset-btn active" onclick="loadQueryPreset('blast_radius', this)">Blast Radius (*1..4)</button>
              <button type="button" class="preset-btn" onclick="loadQueryPreset('api_to_db', this)">SQL Table Lineage</button>
              <button type="button" class="preset-btn" onclick="loadQueryPreset('cqrs_pipeline', this)">Endpoint to Handler</button>
              <button type="button" class="preset-btn" onclick="loadQueryPreset('cyclic_deps', this)">Module Cycles</button>
            </div>
            <button type="button" class="btn" id="runQueryBtn" onclick="runQueryAnimation()">Run Query</button>
          </div>

          <div class="split-bench">
            <div class="bench-pane">
              <div class="bench-pane-head">
                <span>Cypher Query</span>
                <span class="measured" id="queryPresetTag">blast_radius.cypher</span>
              </div>
              <pre class="code-view" id="queryCodeArea"><span class="syn-kw">MATCH</span> path = (caller:Method)-[:CALLS*<span class="syn-num">1..4</span>]-&gt;(target:Method {name: <span class="syn-str">"ChargeAsync"</span>})
<span class="syn-kw">RETURN</span> caller.fullName <span class="syn-kw">AS</span> UpstreamCaller,
       <span class="syn-fn">length</span>(path) <span class="syn-kw">AS</span> Hops
<span class="syn-kw">ORDER BY</span> Hops <span class="syn-kw">ASC</span>
<span class="syn-kw">LIMIT</span> <span class="syn-num">10</span></pre>
              <div class="bench-meta-row">
                <span id="queryExplanation">Finds all upstream callers within 4 hops that can impact PaymentProcessor.ChargeAsync.</span>
                <button type="button" class="btn" onclick="copyCurrentQuery()">Copy CLI Command</button>
              </div>
            </div>

            <div class="bench-pane">
              <div class="bench-pane-head">
                <span>SQLite Graph Output</span>
                <span class="measured tone-ok" id="queryTimingBadge">4 rows in 3.8 ms</span>
              </div>
              <div class="data-table-wrap" id="queryResultsArea"><table class="data-table"><thead><tr><th>UpstreamCaller</th><th>Hops</th></tr></thead><tbody><tr><td>Billing.Application.Commands.CaptureOrderHandler.Handle</td><td>1</td></tr><tr><td>Orders.Api.Controllers.CheckoutController.SubmitOrder</td><td>2</td></tr><tr><td>Subscriptions.Worker.Consumers.RenewalConsumer.Consume</td><td>2</td></tr><tr><td>Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute</td><td>3</td></tr></tbody></table></div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Section 3: CI Guardrails & Baseline Ratchet -->
    <section class="band band--gates" id="gates">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">Freeze legacy debt. Block new drift in CI.</h2>
          <p class="band-lede">
            Baseline existing violations once so pull requests fail only on new drift.
          </p>
        </div>

        <div class="workbench" data-slot="gate-workbench">
          <div class="workbench-bar">
            <div class="workbench-tabs" role="tablist" aria-label="Architecture gate presets">
              <button type="button" class="preset-btn active" onclick="loadGatePreset('boundaries', this)">1. Layer Boundary</button>
              <button type="button" class="preset-btn" onclick="loadGatePreset('topology', this)">2. Shared SQL Writes</button>
              <button type="button" class="preset-btn" onclick="loadGatePreset('visitor', this)">3. Async Deadlock</button>
              <button type="button" class="preset-btn" onclick="loadGatePreset('literal', this)">4. Hardcoded URLs</button>
              <button type="button" class="preset-btn" onclick="loadGatePreset('ratchet', this)">5. Baseline Ratchet</button>
            </div>
            <a href="/architectures" class="btn">Architecture Gate Packs</a>
          </div>

          <div class="split-bench">
            <div class="bench-pane">
              <div class="bench-pane-head">
                <span id="gateFileLabel">.codeexplorer/gates/domain-purity.yaml</span>
                <span class="measured" id="gateDimensionBadge">Layer boundary</span>
              </div>
              <pre class="code-view" id="gateYamlArea"><span class="syn-kw">id:</span> <span class="syn-str">ARCH-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">Domain Layer Must Not Reference Infrastructure</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (d:Class)-[:DEPENDS_ON|CALLS]-&gt;(inf:Class)
  <span class="syn-kw">WHERE</span> d.namespace <span class="syn-kw">CONTAINS</span> <span class="syn-str">".Domain"</span>
    <span class="syn-kw">AND</span> (inf.namespace <span class="syn-kw">CONTAINS</span> <span class="syn-str">".Infrastructure"</span>
      <span class="syn-kw">OR</span> inf.namespace <span class="syn-kw">CONTAINS</span> <span class="syn-str">"Microsoft.EntityFrameworkCore"</span>)
  <span class="syn-kw">RETURN</span> d.fullName <span class="syn-kw">AS</span> Violator, inf.fullName <span class="syn-kw">AS</span> ForbiddenTarget</pre>
              <div class="bench-meta-row">
                <span id="gateDescriptionText">Blocks any type inside a Domain module from depending on Infrastructure or EF Core types.</span>
                <button type="button" class="btn" onclick="copyCurrentGateYaml()">Copy Gate YAML</button>
              </div>
            </div>

            <div class="bench-pane">
              <div class="bench-pane-head">
                <span><code>ce gate check --ratchet</code></span>
                <span class="measured tone-bad" id="gateStatusBadge">Exit 1 (2 violations)</span>
              </div>
              <div class="violation-list" id="gateViolationsArea">
                <div class="violation-item">
                  <strong class="tone-bad">ARCH-001 (error)</strong>
                  <span>Orders.Domain.Aggregates.Order -&gt; Orders.Infrastructure.Persistence.OrderDbContext</span>
                  <span class="tone-muted">src/Orders.Domain/Aggregates/Order.cs:84</span>
                </div>
                <div class="violation-item">
                  <strong class="tone-bad">ARCH-001 (error)</strong>
                  <span>Billing.Domain.Services.InvoiceCalculator -&gt; Billing.Infrastructure.Http.TaxRateClient</span>
                  <span class="tone-muted">src/Billing.Domain/Services/InvoiceCalculator.cs:41</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Section 4: Install CLI / Extension & Connect MCP -->
    <section class="band band--closing" id="install">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">Install in 30 seconds.</h2>
          <p class="band-lede">
            Run 100% locally in VS Code, your terminal, or any MCP coding agent.
          </p>
        </div>

        <div class="setup-grid">
          <div class="workbench" data-slot="install-workbench">
            <div class="workbench-bar">
              <div class="workbench-tabs" role="tablist" aria-label="Installation methods">
                <button type="button" class="tab-btn active" onclick="switchInstallTab('brew', this)">Homebrew</button>
                <button type="button" class="tab-btn" onclick="switchInstallTab('vscode', this)">VS Code</button>
                <button type="button" class="tab-btn" onclick="switchInstallTab('win', this)">Windows</button>
                <button type="button" class="tab-btn" onclick="switchInstallTab('curl', this)">Linux curl</button>
                <button type="button" class="tab-btn" onclick="switchInstallTab('dotnet', this)">.NET Tool</button>
              </div>
              <span class="workbench-readout">MIT License</span>
            </div>

            <div class="install-pane active" id="tab-brew">
              <p class="panel-copy">Install the native <code>ce</code> binary on macOS or Linux via Homebrew:</p>
              <div class="install-cmd-row">
                <code>brew install vmikhailov/tap/ce</code>
                <button type="button" class="btn" onclick="copyInstallCmd('brew install vmikhailov/tap/ce')">Copy</button>
              </div>
            </div>

            <div class="install-pane" id="tab-vscode">
              <p class="panel-copy">Install the extension from VS Code Marketplace or Open VSX (manages <code>ce</code> automatically):</p>
              <div class="install-cmd-row">
                <code>code --install-extension vmikhailov.code-explorer-vscode</code>
                <div class="slab-actions">
                  <button type="button" class="btn" onclick="copyInstallCmd('code --install-extension vmikhailov.code-explorer-vscode')">Copy</button>
                  <a href="https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode" target="_blank" rel="noopener" class="btn">Marketplace</a>
                </div>
              </div>
            </div>

            <div class="install-pane" id="tab-win">
              <p class="panel-copy">Install the standalone Windows binary in PowerShell:</p>
              <div class="install-cmd-row">
                <code>Invoke-WebRequest -Uri https://code-explorer.app/install.ps1 -OutFile install.ps1; .\\install.ps1</code>
                <button type="button" class="btn" onclick="copyInstallCmd('Invoke-WebRequest -Uri https://code-explorer.app/install.ps1 -OutFile install.ps1; .\\\\install.ps1')">Copy</button>
              </div>
            </div>

            <div class="install-pane" id="tab-curl">
              <p class="panel-copy">Install the standalone binary directly to <code>~/.local/bin/ce</code>:</p>
              <div class="install-cmd-row">
                <code>curl -fsSL -O https://code-explorer.app/install.sh &amp;&amp; sh install.sh</code>
                <button type="button" class="btn" onclick="copyInstallCmd('curl -fsSL -O https://code-explorer.app/install.sh && sh install.sh')">Copy</button>
              </div>
            </div>

            <div class="install-pane" id="tab-dotnet">
              <p class="panel-copy">Install cross-platform via NuGet global tool:</p>
              <div class="install-cmd-row">
                <code>dotnet tool install -g codeexplorer.cli</code>
                <button type="button" class="btn" onclick="copyInstallCmd('dotnet tool install -g codeexplorer.cli')">Copy</button>
              </div>
            </div>

            <div class="quickstart-strip">
              <span>Index any repository locally:</span>
              <code>ce init &amp;&amp; ce scan</code>
            </div>
          </div>

          <div class="workbench" id="ai-mcp" data-slot="mcp-workbench">
            <div class="workbench-bar">
              <span>Connect Claude Code, Cursor, or Windsurf</span>
              <button type="button" class="btn" onclick="copyMCPConfig()">Copy MCP JSON</button>
            </div>
            <div class="bench-pane">
              <p class="panel-copy">
                Add <code>ce mcp</code> to your agent config for instant call-chain, impact, and SQL lineage tools:
              </p>
              <pre class="code-view" id="mcpConfigSnippet" style="min-height: 0;">{
  <span class="syn-kw">"mcpServers"</span>: {
    <span class="syn-fn">"code-explorer"</span>: {
      <span class="syn-kw">"command"</span>: <span class="syn-str">"ce"</span>,
      <span class="syn-kw">"args"</span>: [<span class="syn-str">"mcp"</span>]
    }
  }
}</pre>
            </div>
          </div>
        </div>
      </div>
    </section>`},"/architectures":{routePath:"/architectures",pageKind:"architectures",title:"CodeExplorer (ce) Architecture Encyclopedia & Gate Packs",description:"Interactive encyclopedia of software architectures with downloadable OpenCypher Semantic Invariant Gate (SIG) packs for Clean Architecture, Hexagonal, Modular Monolith, Vertical Slice, Feature-Sliced Design, and Microservices.",canonical:"https://code-explorer.app/architectures",ogUrl:"https://code-explorer.app/architectures",ogTitle:"CodeExplorer Architecture Encyclopedia & Gate Packs",ogDescription:"Pick an architectural style, inspect its dependency rules, and download executable OpenCypher gate packs.",mainHtml:`<section id="workbench" class="hero-stage">
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
              <code id="cli-cmd" class="cli-install-cmd">ce gate install clean-architecture --stack agnostic</code>
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
            Every rule below is a zero-match OpenCypher query evaluated locally in milliseconds via <code>ce gate check</code> or MCP <code>check_semantic_gates</code> with zero LLM token cost.
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
            <h3 class="prompt-stack-title">Inspect executable YAML &amp; OpenCypher definitions</h3>
            <span class="prompt-stack-note">Drop directly into <code>.codeexplorer/gates/</code> or adapt paths with your AI agent</span>
          </div>

          <details id="drawer-0" class="prompt-drawer" open>
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge badge--bad">SIG-CA-B-001</code>
                <span>Domain Core Purity (OpenCypher YAML)</span>
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
query: &gt;-
  MATCH (src:Function)-[:CALLS]-&gt;(dst:Function)
  WHERE (src.file_path CONTAINS '/Domain/' OR src.file_path CONTAINS '/Core/')
    AND (dst.file_path CONTAINS '/Infrastructure/' OR dst.file_path CONTAINS '/Application/' OR dst.file_path CONTAINS '/Api/' OR dst.file_path CONTAINS '/Web/')
  RETURN src.file_path AS file, src.start_line AS line, src.name AS symbol, dst.name AS target,
         'Domain symbol ' + src.name + ' calls outer-layer symbol ' + dst.name AS message</pre>
            </div>
          </details>

          <details id="drawer-1" class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge badge--bad">SIG-CA-B-002</code>
                <span>Application Layer Isolation (OpenCypher YAML)</span>
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
query: &gt;-
  MATCH (src:Function)-[:CALLS]-&gt;(dst:Function)
  WHERE (src.file_path CONTAINS '/Application/' OR src.file_path CONTAINS '/UseCases/')
    AND (dst.file_path CONTAINS '/Infrastructure/' OR dst.file_path CONTAINS '/Persistence/' OR dst.file_path CONTAINS '/Api/')
  RETURN src.file_path AS file, src.start_line AS line, src.name AS symbol, dst.name AS target,
         'Application use case ' + src.name + ' depends directly on adapter ' + dst.name AS message</pre>
            </div>
          </details>

          <details id="drawer-2" class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge badge--bad">SIG-CA-B-003</code>
                <span>No Delivery-to-Database Bypass (OpenCypher YAML)</span>
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
query: &gt;-
  MATCH (q:Query)-[:ACCESSED_BY]-&gt;(fn:Function)
  WHERE fn.file_path CONTAINS '/Controllers/' OR fn.file_path CONTAINS '/Api/' OR fn.file_path CONTAINS '/Resolvers/'
  RETURN fn.file_path AS file, fn.start_line AS line, fn.name AS symbol, q.name AS target,
         'Delivery handler ' + fn.name + ' executes database query directly, bypassing Application layer' AS message</pre>
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
            <code class="panel-code">ce gate install modular-monolith</code>
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
    </section>`},"/ontology":{routePath:"/ontology",pageKind:"ontology",title:"CodeExplorer (ce) 5-Layer Graph Ontology & Schema Reference",description:"Interactive 5-layer ontology map of all 34 entity kinds, 257 extracted properties, and 45 typed relationships collected by CodeExplorer across polyglot codebases.",canonical:"https://code-explorer.app/ontology",ogUrl:"https://code-explorer.app/ontology",ogTitle:"CodeExplorer 5-Layer Graph Ontology Reference",ogDescription:"Explore every AST, dataflow, SQL, messaging, service, and domain entity defined in the CodeExplorer OpenCypher graph.",mainHtml:`<section id="explorer" class="ont-hero-stage">
      <div class="wrap">
        <div class="stage-intro">
          <div>
            <h1 class="hero-title">Every fact CodeExplorer extracts.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Inspect 34 entity kinds, 267 properties, and 45 relationships across 5 layers.
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
                    <span id="ont-focus-count" class="ont-layer-count">15 active links</span>
                  </div>
                  <div id="ont-focus-links" class="ont-focus-links">
                <button type="button" class="ont-focus-chip ont-focus-chip--control_flow" data-select-kind="Function"><code>out: CALLS :Function</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--control_flow" data-select-kind="Function"><code>out: EXPOSED_BY :Function</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--control_flow" data-select-kind="Type"><code>out: EXPOSED_BY :Type</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--control_flow" data-select-kind="Function"><code>out: TRIGGERS :Function</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="Database"><code>out: ATTRIBUTED_TO :Database</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="ExternalService"><code>out: ATTRIBUTED_TO :ExternalService</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="Query"><code>out: ATTRIBUTED_TO :Query</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="CallLiteral"><code>out: PASSES_LITERAL :CallLiteral</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="Topic"><code>out: SUBSCRIBES_TO :Topic</code></button>
                <button type="button" class="ont-focus-chip ont-focus-chip--data_flow" data-select-kind="CallArgument"><code>out: TARGETED_BY :CallArgument</code></button>
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
                    <span class="ont-node-stats">6p / 7e</span>
                  </button>
                </div>
              </div>

              <div class="ont-layer" data-layer-id="4">
                <div class="ont-layer-bar">
                  <h3 class="ont-layer-title">Layer 4: Semantic Architecture</h3>
                  <code class="ont-layer-pill ont-layer-pill--l4">19 kinds</code>
                </div>
                <p class="ont-layer-sub">Runtime workloads, API &amp; broker contracts, databases, SQL queries, tables, and external or cloud dependencies.</p>
                <div class="ont-node-grid">
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Module" title="Software Modules: Abstract base software module encompassing executable workloads, shared libraries, and automated test suites." aria-selected="false">
                    <code class="ont-node-kind">:Module</code>
                    <span class="ont-node-stats">5p / 5e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Workload" title="Runtime Workloads: Abstract executable runtime workload encompassing backend services, client applications, background workers, and CLI tools." aria-selected="false">
                    <code class="ont-node-kind">:Workload</code>
                    <span class="ont-node-stats">5p / 12e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="App" title="Applications: Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular, Electron)." aria-selected="false">
                    <code class="ont-node-kind">:App</code>
                    <span class="ont-node-stats">5p / 12e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--control_flow" data-kind="Service" title="Services: Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server)." aria-selected="false">
                    <code class="ont-node-kind">:Service</code>
                    <span class="ont-node-stats">5p / 16e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Worker" title="Workers: Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service)." aria-selected="false">
                    <code class="ont-node-kind">:Worker</code>
                    <span class="ont-node-stats">5p / 13e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="CliTool" title="CLI Tools: Represents a command-line tool, developer script, or administrative CLI utility." aria-selected="false">
                    <code class="ont-node-kind">:CliTool</code>
                    <span class="ont-node-stats">5p / 12e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Library" title="Libraries &amp; SDKs: Represents a shared library, utility module, DTO package, or domain contract reused across services." aria-selected="false">
                    <code class="ont-node-kind">:Library</code>
                    <span class="ont-node-stats">5p / 6e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="TestSuite" title="Test Suites: Represents an automated test project, runner suite, or test module (e.g. NUnit/xUnit test project, Jest test suite, pytest suite)." aria-selected="false">
                    <code class="ont-node-kind">:TestSuite</code>
                    <span class="ont-node-stats">5p / 6e</span>
                  </button>
                  <button type="button" class="ont-node-btn" data-kind="Endpoint" title="API &amp; Message Endpoints (REST, gRPC, WS, MSG): Represents an exposed external API or message contract endpoint (REST, gRPC, GraphQL, WebSocket, WCF, or Broker Consumer)." aria-selected="true">
                    <code class="ont-node-kind">:Endpoint</code>
                    <span class="ont-node-stats">20p / 15e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="Topic" title="Message Topics &amp; Queues: Represents a message queue, event exchange, or topic boundary." aria-selected="false">
                    <code class="ont-node-kind">:Topic</code>
                    <span class="ont-node-stats">10p / 11e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="ExternalService" title="External Services &amp; Resources: Represents an external API, managed cloud resource, or remote service dependency outside the workspace (REST, gRPC, GraphQL, S3, Stripe, Auth0)." aria-selected="false">
                    <code class="ont-node-kind">:ExternalService</code>
                    <span class="ont-node-stats">15p / 13e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Cloud" title="Cloud Platforms &amp; Vendors: Represents a cloud platform, managed infrastructure provider, or SaaS vendor hosting external services, databases, brokers, or workloads (e.g. AWS, GCP, Azure, Cloudflare, Stripe, Supabase)." aria-selected="false">
                    <code class="ont-node-kind">:Cloud</code>
                    <span class="ont-node-stats">5p / 6e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="CloudResource" title="Cloud Resources: Represents a provisioned or referenced cloud infrastructure resource (e.g. RDS instance, SQS queue, S3 bucket, VPC, IAM role, KMS key, Kubernetes workload) hosted in a Cloud platform." aria-selected="false">
                    <code class="ont-node-kind">:CloudResource</code>
                    <span class="ont-node-stats">11p / 13e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="PeerMesh" title="Peer Meshes: Represents a symmetric peer-to-peer cluster or collective topology." aria-selected="false">
                    <code class="ont-node-kind">:PeerMesh</code>
                    <span class="ont-node-stats">4p / 1e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="Database" title="Databases &amp; Storage: Represents a database server instance or catalog configured by a workload and containing one or more DbSchema namespaces." aria-selected="false">
                    <code class="ont-node-kind">:Database</code>
                    <span class="ont-node-stats">15p / 9e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="DbSchema" title="Database Schemas: Represents a logical or physical schema namespace within a database catalog owning tables, views, and stored procedures (e.g. public, dbo, billing, main)." aria-selected="false">
                    <code class="ont-node-kind">:DbSchema</code>
                    <span class="ont-node-stats">5p / 4e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Table" title="Tables &amp; Views: Represents a physical database table." aria-selected="false">
                    <code class="ont-node-kind">:Table</code>
                    <span class="ont-node-stats">5p / 8e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Procedure" title="Stored Procedures: Represents a stored procedure in a database." aria-selected="false">
                    <code class="ont-node-kind">:Procedure</code>
                    <span class="ont-node-stats">0p / 4e</span>
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
                    <span class="ont-node-stats">14p / 25e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--control_flow" data-kind="Function" title="Functions &amp; Methods: Represents a parsed method, function, subroutine, or procedure." aria-selected="false">
                    <code class="ont-node-kind">:Function</code>
                    <span class="ont-node-stats">19p / 34e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Member" title="Members &amp; Fields: Represents a declared field, property, parameter, or local variable." aria-selected="false">
                    <code class="ont-node-kind">:Member</code>
                    <span class="ont-node-stats">12p / 6e</span>
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
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--structural" data-kind="CatchClause" title="Catch Clauses: Represents a catch or except block in structured exception handling." aria-selected="false">
                    <code class="ont-node-kind">:CatchClause</code>
                    <span class="ont-node-stats">10p / 3e</span>
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
                    <span class="ont-node-stats">21p / 17e</span>
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
                    <span class="ont-node-stats">8p / 19e</span>
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
                    <span id="insp-compact-props-label" class="ont-block-label">Properties (20 + 4 base)</span>
                    <div id="insp-compact-props" class="ont-compact-pills"><code class="ont-edge-prop">name: string</code><code class="ont-edge-prop">path: string</code><code class="ont-edge-prop">http_method: string</code><code class="ont-edge-prop">route_template: string</code><code class="ont-edge-prop">protocol: string</code><code class="ont-edge-prop">is_anonymous: boolean</code><code class="ont-edge-prop">+14 more</code></div>
                  </div>
                  <div class="ont-compact-group">
                    <span id="insp-compact-rels-label" class="ont-block-label">Connected entities (13 out, 2 in)</span>
                    <div id="insp-compact-rels" class="ont-compact-pills"><button type="button" class="ont-jump-btn" data-select-kind="Function">:Function</button><button type="button" class="ont-jump-btn" data-select-kind="Type">:Type</button><button type="button" class="ont-jump-btn" data-select-kind="Database">:Database</button><button type="button" class="ont-jump-btn" data-select-kind="ExternalService">:ExternalService</button><button type="button" class="ont-jump-btn" data-select-kind="Query">:Query</button><button type="button" class="ont-jump-btn" data-select-kind="CallLiteral">:CallLiteral</button><code class="ont-edge-prop">+6 more</code></div>
                  </div>
                </div>

                <div id="insp-full-body" class="ont-insp-full-body">
                  <div class="ont-block">
                    <div class="ont-block-bar">
                      <span id="insp-props-heading" class="ont-block-label">Collected properties (20 + 4 base)</span>
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
                    </div>
                  </div>

                  <div class="ont-block">
                    <div class="ont-block-bar">
                      <span id="insp-rels-heading" class="ont-block-label">Typed relationships (13 out, 2 in)</span>
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
                  <button type="button" class="ont-jump-btn" data-select-kind="CatchClause">:CatchClause</button>
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
                      <span class="ont-block-label">OpenCypher query</span>
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
              <span class="ont-layer-count">11 relationship types</span>
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
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; Function</code><code class="ont-pair-pill">Function -&gt; Function</code><code class="ont-pair-pill">Function -&gt; Type</code><code class="ont-pair-pill">Type -&gt; Function</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--control_flow">CALLS_ENDPOINT</code></td>
                    <td>Links an external API call to the target HTTP endpoint it invokes.</td>
                    <td><code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">ExternalService -&gt; Endpoint</code></div></td>
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
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Service -&gt; Endpoint</code></div></td>
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
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Workload -&gt; ExternalService</code><code class="ont-pair-pill">Workload -&gt; Service</code></div></td>
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
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; Function</code><code class="ont-pair-pill">EntryPoint -&gt; File</code></div></td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div class="ont-rel-cat-block">
            <div class="ont-rel-cat-head">
              <h3>Data, SQL &amp; Messaging</h3>
              <span class="ont-layer-count">14 relationship types</span>
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
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">ExternalService -&gt; Function</code><code class="ont-pair-pill">ExternalService -&gt; Type</code><code class="ont-pair-pill">Member -&gt; Function</code><code class="ont-pair-pill">Procedure -&gt; Function</code><code class="ont-pair-pill">Query -&gt; Function</code><code class="ont-pair-pill">Query -&gt; Type</code> <code class="ont-pair-pill">+3 more</code></div></td>
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
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">READS_FROM</code></td>
                    <td>Links a query or workload to the table or cloud resource it reads from.</td>
                    <td><code>operation</code>, <code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Query -&gt; Table</code></div></td>
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
                    <td><code class="ont-edge-tag ont-edge-tag--data_flow">WRITES_TO</code></td>
                    <td>Links a query or workload to the table or cloud resource it writes to.</td>
                    <td><code>operation</code>, <code>evidence_kind</code>, <code>evidence_ids</code>, <code>hops</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Query -&gt; Table</code></div></td>
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
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">COORDINATES_WITH</code></td>
                    <td>Links a workload to the symmetric peer mesh it coordinates with.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Service -&gt; PeerMesh</code></div></td>
                  </tr>
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
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Database -&gt; CloudResource</code><code class="ont-pair-pill">ExternalService -&gt; CloudResource</code><code class="ont-pair-pill">Module -&gt; Project</code><code class="ont-pair-pill">Package -&gt; Project</code><code class="ont-pair-pill">Topic -&gt; CloudResource</code><code class="ont-pair-pill">Workload -&gt; CloudResource</code></div></td>
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
              <span class="ont-layer-count">8 relationship types</span>
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
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">BoundedContext -&gt; Database</code><code class="ont-pair-pill">BoundedContext -&gt; Module</code><code class="ont-pair-pill">BoundedContext -&gt; Topic</code><code class="ont-pair-pill">Database -&gt; DbSchema</code><code class="ont-pair-pill">DbSchema -&gt; Procedure</code><code class="ont-pair-pill">DbSchema -&gt; Table</code> <code class="ont-pair-pill">+8 more</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">DECLARED_IN</code></td>
                    <td>Links a syntactic type, function, or member declaration to its physical declaring source file.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">CallArgument -&gt; File</code><code class="ont-pair-pill">CallLiteral -&gt; File</code><code class="ont-pair-pill">CatchClause -&gt; File</code><code class="ont-pair-pill">Cloud -&gt; File</code><code class="ont-pair-pill">CloudResource -&gt; File</code><code class="ont-pair-pill">Database -&gt; File</code> <code class="ont-pair-pill">+12 more</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--structural">DECLARES</code></td>
                    <td>Indicates that a container entity declares a sub-entity.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Endpoint -&gt; CatchClause</code><code class="ont-pair-pill">Function -&gt; CatchClause</code><code class="ont-pair-pill">Function -&gt; Function</code><code class="ont-pair-pill">Function -&gt; Member</code><code class="ont-pair-pill">Function -&gt; Type</code><code class="ont-pair-pill">Type -&gt; Type</code></div></td>
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
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </section>`},"/benchmarks":{routePath:"/benchmarks",pageKind:"benchmarks",title:"CodeExplorer (ce) Benchmarks Catalog",description:"Catalog of 8 reproducible benchmarks comparing CodeExplorer (ce) against codebase-memory-mcp (CBM), Graphify, and ripgrep across multi-task brownfield feature implementation, architectural guardrails, multi-hop audits, and deterministic graph indexing.",canonical:"https://code-explorer.app/benchmarks",ogUrl:"https://code-explorer.app/benchmarks",ogTitle:"CodeExplorer (ce) Benchmarks Catalog",ogDescription:"Catalog of 8 reproducible benchmarks comparing CodeExplorer (ce) against codebase-memory-mcp (CBM), Graphify, and ripgrep across multi-task brownfield feature implementation, architectural guardrails, multi-hop audits, and deterministic graph indexing.",mainHtml:`<section class="hero-stage">
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
            <span>Track 1: Real-Life Feature Implementation &amp; Pre-Commit Guardrails (Cases 09, 08, 07 &amp; 06)</span>
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
                <td><strong><a href="/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent">Case Study 09</a></strong><br><span class="tone-muted">twentyhq/twenty (12 Runs)</span></td>
                <td>Zero-spoiler constructive coding benchmark across 3 multi-module tickets (API key revocation, app public-domain DNS teardown, subdomain collision guard) with 100% symmetric architectural rules, comparing Bare Agent (<code>rg</code> + <code>read_file</code>) against CodeExplorer 1-Shot Context Pack (Iter 1: without <code>@Module</code> graph; Iter 2: with <code>@Module</code> cycle safety).</td>
                <td>CodeExplorer<br>vs<br>ripgrep</td>
                <td class="cell-win"><strong class="tone-ok">~2.2× fewer input tokens</strong> (~3.1M vs ~6.9M vs Bare; ~3.1× fewer than Iter 1)<br><strong class="tone-ok">~1.9× fewer file reads &amp; ~1.4× faster</strong> (55 vs 104 reads, 19m 38s vs 28m 02s)<br><strong>15/15 Jest specs &amp; 21/21 SIG gates pass</strong> (0 rg calls needed in Task 2)</td>
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
                <td>Automated pre-commit architectural guard &amp; 4-phase SHA-256 ratchet showcase: enforce 6 invariant rules across 18 subsystems (4,216 files), freeze legacy debt with line-shift immunity, block single-line PR leaks, and monotonically tighten baselines.</td>
                <td>CodeExplorer<br><span class="tone-muted">(Native SIG)</span></td>
                <td class="cell-win" data-bind-html="catalog.cs06_outcome"><strong class="tone-ok">422 ms native build check</strong><br><strong class="tone-ok">0 tokens ($0.00)</strong> · 4-phase SHA-256 ratchet<br><strong>6 of 6 gates evaluated</strong> (2 PASS, 1 WARN, 3 FAIL)</td>
                <td><a href="/benchmarks/06-precommit-architectural-guard" class="btn">Read Showcase</a></td>
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
          1. CodeExplorer (ce): v1.75.0 to v1.87.2 (.NET 10 &amp; .NET 11, SQLite + OpenCypher, 36 MCP tools)<br>
          2. codebase-memory-mcp (CBM): v0.12.1 (win32-x64 Go + SQLite binary, 17 MCP tools)<br>
          3. Graphify (graphifyy): v0.9.104 (Python 3.10, Tree-sitter + NetworkX --code-only, 10 MCP tools)<br>
          4. Lexical CLI Baseline: ripgrep (rg v14.1.1) + Python 3.10 (Case 02) + view_file<br>
          5. LLM Model (Tracks 1 &amp; 2): Gemini 3.8 Flash (symmetric system prompts across paired runs)<br>
          6. Pinned Target Commits: twentyhq/twenty @ b9ef7107 (Case 01), twentyhq/twenty @ 12e8fae4 (Cases 03, 04, 07 &amp; 08), immich-app/immich @ 2153d22f (Cases 03 &amp; 04), OrchardCMS/OrchardCore @ 05ad2d9a (Case 05), argoproj/argo-cd @ 6d689fa3 (Case 06)</div>
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
          1. CodeExplorer (ce): v1.75.0 (.NET 10, SQLite + OpenCypher, 36 MCP tools)<br>
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
# Q1 — Topics with producers but no consumers (or vice versa):
MATCH (t:Topic) WHERE NOT (t)-[:SUBSCRIBED_BY]-&gt;() OR NOT (t)-[:PUBLISHED_BY]-&gt;() RETURN t.name
# Q2 — Pairs of projects sharing a database:
MATCH (p1:Project)-[:USES_DB]-&gt;(d:Database)&lt;-[:USES_DB]-(p2:Project) WHERE p1.name &lt; p2.name RETURN p1.name, p2.name, d.name
# Q3 — Top written database tables:
MATCH (q:Query)-[:WRITES_TO]-&gt;(t:Table) RETURN t.name, count(q) AS writes ORDER BY writes DESC LIMIT 10
# Q4 — Endpoints transitively writing to a database table within 1..4 call hops:
MATCH (e:Endpoint)-[:EXPOSED_BY]-&gt;(f:Function)-[:CALLS*1..4]-&gt;(w:Function)&lt;-[:DECLARED_IN]-(q:Query)-[:WRITES_TO]-&gt;(t:Table) RETURN e.name, t.name LIMIT 20
# Q5 — Per-project endpoint and published topic counts:
MATCH (p:Project) OPTIONAL MATCH (p)-[:EXPOSES]-&gt;(e:Endpoint) OPTIONAL MATCH (p)-[:PUBLISHES_TO]-&gt;(tp:Topic) RETURN p.name, count(DISTINCT e), count(DISTINCT tp)
# Q6 — End-to-end chain: Endpoint -&gt; Call Chain (0..3 hops) -&gt; Queue Publish -&gt; Queue Subscriber:
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
          1. CodeExplorer (ce): v1.75.0 (.NET 10, SQLite + OpenCypher, 36 MCP tools)<br>
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
              Continuous architectural governance showcase on argoproj/argo-cd (Kubernetes continuous delivery platform with 18 subsystems and 4,216 files). Demonstrates how CodeExplorer Semantic Invariant Gates (SIG) evaluate 6 production rules in 422 ms with zero LLM tokens ($0.00), freeze legacy technical debt into line-shift-immune SHA-256 baselines, block single-line PR regressions, and monotonically tighten architectural ceilings.
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
            Mature codebases cannot halt feature delivery for weeks to refactor 70-method structs on Day 1. CodeExplorer hashes structural AST tuples instead of volatile line numbers, enabling zero-friction CI adoption and one-way debt reduction.
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
    </section>`},"/privacy":{routePath:"/privacy",pageKind:"privacy",title:"Privacy Policy | CodeExplorer",description:"CodeExplorer is a 100% local, privacy-first developer tool. Zero telemetry, zero cloud dependencies, zero source code transmission.",canonical:"https://code-explorer.app/privacy",ogUrl:"https://code-explorer.app/privacy",ogTitle:"Privacy Policy | CodeExplorer",ogDescription:"CodeExplorer is a 100% local, privacy-first developer tool. Zero telemetry, zero cloud dependencies, zero source code transmission.",mainHtml:`<h1>Privacy Policy</h1>\r
      <div class="meta">Effective Date: October 29, 2026 · Standalone Open Source Software</div>\r
\r
      <div class="card">\r
        <h2>1. Summary &amp; Core Principles</h2>\r
        <p>\r
          CodeExplorer (CLI binary <code>ce</code> and the VS Code Extension <code>code-explorer-vscode</code>) is designed from the ground up as a <strong>100% local-first, privacy-by-design</strong> architecture intelligence engine.\r
        </p>\r
        <ul>\r
          <li><strong>Zero Telemetry:</strong> CodeExplorer does not collect, record, or transmit usage statistics, crash dumps, or telemetry.</li>\r
          <li><strong>Zero Code Transmission:</strong> Your source code, AST, tokens, git history, and architecture graphs never leave your local machine or your CI/CD runner.</li>\r
          <li><strong>Local Database:</strong> All parsed architecture data is stored in a local SQLite database (<code>.codeexplorer/codeexplorer.db</code>) within your project repository.</li>\r
          <li><strong>No User Accounts:</strong> We do not require registration, accounts, or authentication to use CodeExplorer.</li>\r
        </ul>\r
      </div>\r
\r
      <div class="card">\r
        <h2>2. Website (code-explorer.app)</h2>\r
        <p>\r
          The website <code>code-explorer.app</code> is a static documentation and download portal hosted on an isolated server.\r
        </p>\r
        <p>\r
          Standard web server access logs (client IP address, user-agent, requested URL, and timestamp) are processed strictly for operational security, DDOS mitigation, and anonymous aggregate visitor counts. No advertising cookies, third-party trackers, or personal tracking scripts are loaded on this website.\r
        </p>\r
      </div>\r
\r
      <div class="card">\r
        <h2>3. Local Architecture Engine Execution</h2>\r
        <p>\r
          When you execute <code>ce scan</code>, <code>ce map</code>, or <code>ce gate</code>, or when you interact with the VS Code extension or MCP server:\r
        </p>\r
        <ul>\r
          <li>The analysis executes exclusively on your CPU using local Roslyn/Tree-sitter parsers.</li>\r
          <li>No external network requests are made during scanning or invariant checking.</li>\r
          <li>MCP agent interactions (Claude Code, Cursor, Copilot) are mediated locally via standard input/output (stdio) with your local client.</li>\r
        </ul>\r
      </div>\r
\r
      <div class="card">\r
        <h2>4. Open Source Transparency &amp; License</h2>\r
        <p>\r
          CodeExplorer is free and open-source software released under the <a href="https://github.com/vmikhailov/code-explorer/blob/main/LICENSE" target="_blank" rel="noopener" style="color: var(--accent);">MIT License</a>. The entire source code is publicly inspectable and auditable on <a href="https://github.com/vmikhailov/code-explorer" target="_blank" rel="noopener" style="color: var(--accent);">GitHub</a>.\r
        </p>\r
      </div>\r
\r
      <div class="card">\r
        <h2>5. Contact</h2>\r
        <p>\r
          If you have questions regarding this privacy policy or security practices, you can open an issue on the official GitHub repository at <a href="https://github.com/vmikhailov/code-explorer/issues" target="_blank" rel="noopener" style="color: var(--accent);">github.com/vmikhailov/code-explorer/issues</a>.\r
        </p>\r
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
    </section>`},"/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent":{routePath:"/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent",pageKind:"benchmark-case",title:"Case Study 09: Zero-Spoiler Coding Benchmark (One-Shot Context Pack vs Bare Agent) | CodeExplorer",description:"Zero-spoiler constructive coding benchmark on twentyhq/twenty (36,543 files, 12 runs) comparing CodeExplorer One-Shot Task Context Pack + NestJS @Module cycle safety against a bare ripgrep agent under identical architectural rules: ~2.2× fewer input tokens, ~1.9× fewer file reads, ~1.4× faster, 15/15 Jest specs and 21/21 gates passed.",canonical:"https://code-explorer.app/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent",ogUrl:"https://code-explorer.app/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent",ogTitle:"Case Study 09: Zero-Spoiler Coding Benchmark (One-Shot Context Pack vs Bare Agent)",ogDescription:"Why chatty MCP loses, why 1-shot task context compilation wins (~2.2× fewer tokens vs Bare Agent), and why NestJS @Module cycle verification cuts graph-agent token usage by ~3.1×.",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="crumb-bar">
          <div class="crumb-trail">
            <a href="/benchmarks">Benchmarks Catalog</a>
            <span>/</span>
            <span class="tone-ink">Case Study 09: Zero-Spoiler One-Shot Context Pack vs Bare Agent</span>
          </div>
          <span class="measured">46 packages, 36,543 indexed files · 3 zero-spoiler tasks (12 runs)</span>
        </div>

        <div class="hero-top">
          <div>
            <h1 class="hero-title">Case Study 09: zero-spoiler coding benchmark (One-Shot Context Pack vs Bare Agent).</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              When an AI coding agent receives a realistic, zero-spoiler bug or feature ticket and both agents share the exact same architectural rules in their system prompt, does a code graph actually beat a bare ripgrep + read_file agent? This standalone study tests 3 multi-module tasks across 12 runs on twentyhq/twenty, showing why chatty multi-call MCP loses, why a 1-shot task context compiler wins (~2.2× fewer tokens, ~1.9× fewer file reads), and why indexing NestJS @Module dependency cycles cuts graph-agent token usage by ~3.1×.
            </p>
            <div class="hero-controls">
              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/09-zero-spoiler-context-pack-vs-bare-agent" target="_blank" rel="noopener" class="btn btn--primary">Inspect All 12 Diffs &amp; Harness</a>
              <a href="#prompts" class="btn">Inspect Prompts &amp; Context Pack</a>
            </div>
          </div>
        </div>

        <div class="panels panels--4" style="margin-top: var(--space-xl);">
          <div class="panel panel--sunken">
            <span class="reading tone-accent">~2.2×</span>
            <h2 class="panel-title">Fewer Input Tokens</h2>
            <p class="panel-copy">Across all 3 zero-spoiler tasks in Iteration 2, CodeExplorer consumed ~3.1M cumulative input tokens vs ~6.9M for the Bare Agent (~1.6× fewer uncached input tokens).</p>
            <code class="panel-code">3,080,276 vs 6,897,615 total input tokens</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-ok">~1.9×</span>
            <h2 class="panel-title">Fewer File Reads</h2>
            <p class="panel-copy">One-shot context compilation + pre-verified @Module import safety cut source file reads from 104 down to 55 and shell commands from 41 down to 20 (0 rg calls in Task 2).</p>
            <code class="panel-code">55 vs 104 file reads · 59 vs 84 LLM turns</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool">~3.1×</span>
            <h2 class="panel-title">@Module Graph Gain</h2>
            <p class="panel-copy">Adding NestJS @Module cycle verification (moduleWiringAndCycleSafety) reduced CodeExplorer token usage from ~9.5M (Iteration 1) to ~3.1M (Iteration 2) and time from ~42m to ~20m.</p>
            <code class="panel-code">1,178 s (19m 38s) vs 1,682 s (28m 02s) Bare</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading">15/15 Pass</span>
            <h2 class="panel-title">Behavioral &amp; Gate Parity</h2>
            <p class="panel-copy">Every run is verified against an external held-out suite of 15 Jest behavioral assertions (5 per task), tsgo typechecking, and 7 Semantic Invariant Gates (21/21 passed).</p>
            <code class="panel-code">15/15 Jest assertions · 21/21 SIG gates (0 new)</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);">Tested participants (3 zero-spoiler constructive coding tasks across 2 iterations = 12 runs on twentyhq/twenty @ 12e8fae4):<br>
          1. CodeExplorer Context-Pack Compiler (ce): v1.89.4 + exp/context-pack (.NET 11, SQLite + OpenCypher, 1-shot /api/context + /api/gates/check warm daemon with FileWatcher, 465,090 nodes, 1,068,128 edges across 460 NestJS modules)<br>
          2. Bare Agent Baseline: ripgrep (rg v14.1.1) + read_file + edit_file + write_file (identical sandbox and identical 7 architectural rules in system prompt)<br>
          3. LLM Models: Iteration 1 executed on Gemini 4 Carbon (High); Iteration 2 executed on Gemini 4 Argon (Medium) with 100% paired same-model runs<br>
          4. Target Repository: twentyhq/twenty @ 12e8fae4 (46 packages, 36,543 indexed files, TypeScript, NestJS, GraphQL, TypeORM)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Why most code-graph benchmarks fail (and why this study is structured differently).</h2>
          <p class="band-lede">
            When we audited an earlier constructive benchmark where a graph-equipped agent used 2.2× more tokens and 2.5× more time than ripgrep, we found three methodological bugs that distort almost all AI coding benchmarks. We designed this benchmark from scratch to eliminate all three.
          </p>
        </div>

        <div class="panels panels--3">
          <div class="panel">
            <span class="reading tone-bad">Flaw 01</span>
            <h3 class="panel-title">The Spoiler-Prompt Trap</h3>
            <p class="panel-copy">If a benchmark prompt names the target file (<code>api-key.service.ts</code>), method signature (<code>revokeApiKey</code>), or cache key (<code>usageLimits</code>), a 36,543-file monorepo collapses into a 2-file read task. A bare agent needs zero search, so any indexing tool is pure overhead. Here, prompts contain <strong>zero file paths, class names, or method names</strong>.</p>
            <code class="panel-code">100% symptom-only bug &amp; feature tickets</code>
          </div>
          <div class="panel">
            <span class="reading tone-bad">Flaw 02</span>
            <h3 class="panel-title">The Asymmetric-Rules Trap</h3>
            <p class="panel-copy">Hiding architectural rules from the baseline agent while enforcing them on the graph agent tests prompt secrecy rather than code intelligence. In this benchmark, <strong>both agents receive the exact same 7 architectural invariants verbatim</strong> in their system prompt.</p>
            <code class="panel-code">100% symmetric architectural instructions</code>
          </div>
          <div class="panel">
            <span class="reading tone-bad">Flaw 03</span>
            <h3 class="panel-title">The Chatty Multi-Call MCP Trap</h3>
            <p class="panel-copy">Calling 15 to 20 granular MCP tools (<code>find_symbol</code>, <code>get_file_outline</code>, <code>get_callers</code>) resends the growing 40K-token conversation history on every turn, costing 500K+ extra input tokens. We replaced chatty navigation with a <strong>single Turn-1 context compiler</strong> (<code>ce context</code>).</p>
            <code class="panel-code">1 call on Turn 1 + 1 gate check on final turn</code>
          </div>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Where agent tokens actually go (and the @Module circular-dependency bottleneck).</h2>
          <p class="band-lede">
            Inspecting the turn-by-turn transcripts from Iteration 1 revealed a surprising truth about brownfield coding: finding the entry point takes only 1 to 2 turns even with ripgrep. Over 70% of agent turns and file reads are spent on three structural verification problems.
          </p>
        </div>

        <div class="panels panels--3">
          <div class="panel">
            <span class="reading tone-accent">Problem A</span>
            <h3 class="panel-title">Hidden Cross-Service Side Effects</h3>
            <p class="panel-copy">Deleting a database row is never just <code>repo.delete()</code>. Revoking an API key must recompute <code>WorkspaceCacheService</code> and drop active Redis quota counters; deleting an application must tear down external Cloudflare hostnames via <code>DnsManagerService</code>. <code>outboundSideEffects</code> surfaces these edges on Turn 1.</p>
            <code class="panel-code">outboundSideEffects + tablesAndCanonicalOwners</code>
          </div>
          <div class="panel">
            <span class="reading tone-cool">Problem B</span>
            <h3 class="panel-title">Ratcheted Legacy Debt Traps</h3>
            <p class="panel-copy">In brownfield files like <code>application.service.ts</code> and <code>auth.resolver.ts</code>, older methods directly query foreign TypeORM repositories. Without warning, LLMs copy the style of adjacent methods and break module boundaries. <code>legacyTrapsInSubgraphFiles</code> flags tolerated debt before the agent reads the file.</p>
            <code class="panel-code">guardrails.legacyTrapsInSubgraphFiles</code>
          </div>
          <div class="panel">
            <span class="reading tone-ok">Problem C</span>
            <h3 class="panel-title">NestJS @Module Cycle Paranoia</h3>
            <p class="panel-copy">In Iteration 1, CodeExplorer found the exact service files by Turn 2, but then spent <strong>20 to 24 turns reading unrelated *.module.ts files</strong> to check if importing <code>UsageLimitModule</code> or <code>PublicDomainModule</code> would create a circular dependency. Adding <code>moduleWiringAndCycleSafety</code> eliminated that entire search loop in Iteration 2.</p>
            <code class="panel-code">SAFE_DIRECT_IMPORT (0 reverse paths in &lt;1 ms)</code>
          </div>
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">3. Suite-wide ablation scorecard (Iteration 1 vs Iteration 2 across all 3 tasks).</h2>
          <p class="band-lede">
            All 12 runs pass 100% of the held-out Jest behavioral specs (15/15) and all 7 Semantic Invariant Gates (21/21 across 3 tasks). Comparing Iteration 1 (Context Pack without @Module graph) against Iteration 2 (Context Pack with @Module cycle verdicts) isolates the exact impact of pre-computing DI module graph safety.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Suite-wide ablation scorecard across 3 tasks">
          <div class="table-caption-bar">
            <span>Table 1: 3-Task Combined Totals Across Iteration 1 and Iteration 2 (12 Runs)</span>
            <span class="measured">15 Jest behavioral assertions · 7 SIG gates per task</span>
          </div>
          <table class="bench-table">
            <thead>
              <tr>
                <th>Metric (Sum of Tasks 1, 2 &amp; 3)</th>
                <th>Iter 1: CE v1<br><span class="tone-muted">(No @Module Graph)</span></th>
                <th>Iter 1: Bare<br><span class="tone-muted">(rg + read_file)</span></th>
                <th>Iter 2: CE v2<br><span class="tone-muted">(With @Module Safety)</span></th>
                <th>Iter 2: Bare<br><span class="tone-muted">(rg + read_file)</span></th>
                <th>CE v2 Advantage<br><span class="tone-muted">(vs Iter 2 Bare)</span></th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Held-Out Jest Behavioral Specs</strong></td>
                <td class="cell-num">15 / 15</td>
                <td class="cell-num">15 / 15</td>
                <td class="cell-win">15 / 15 (100%)</td>
                <td class="cell-num">15 / 15 (100%)</td>
                <td class="cell-win">Parity (15/15 pass)</td>
              </tr>
              <tr>
                <td><strong>Architectural Gates (SIG-TWENTY-001..007)</strong></td>
                <td class="cell-num">21 / 21 (0&nbsp;new)</td>
                <td class="cell-num">21 / 21 (0&nbsp;new)</td>
                <td class="cell-win">21 / 21 (0&nbsp;new)</td>
                <td class="cell-num">21 / 21 (0&nbsp;new)</td>
                <td class="cell-win">0 new violations</td>
              </tr>
              <tr>
                <td><strong>LLM Turns</strong></td>
                <td class="cell-num">100&nbsp;turns</td>
                <td class="cell-num">140&nbsp;turns</td>
                <td class="cell-win">59&nbsp;turns</td>
                <td class="cell-num">84&nbsp;turns</td>
                <td class="cell-win">1.42× fewer turns (-30%)</td>
              </tr>
              <tr>
                <td><strong>Total Tool Calls</strong></td>
                <td class="cell-num">189&nbsp;calls</td>
                <td class="cell-num">283&nbsp;calls</td>
                <td class="cell-win">95&nbsp;calls</td>
                <td class="cell-num">165&nbsp;calls</td>
                <td class="cell-win">1.74× fewer calls (-42%)</td>
              </tr>
              <tr>
                <td><strong>Source File Reads (read_file)</strong></td>
                <td class="cell-num">121&nbsp;reads</td>
                <td class="cell-num">175&nbsp;reads</td>
                <td class="cell-win">55&nbsp;reads</td>
                <td class="cell-num">104&nbsp;reads</td>
                <td class="cell-win">1.89× fewer reads (-47%)</td>
              </tr>
              <tr>
                <td><strong>Shell Commands (run_command)</strong></td>
                <td class="cell-num">45&nbsp;commands</td>
                <td class="cell-num">82&nbsp;commands</td>
                <td class="cell-win">20&nbsp;commands</td>
                <td class="cell-num">41&nbsp;commands</td>
                <td class="cell-win">2.05× fewer cmds (-51%)</td>
              </tr>
              <tr>
                <td><strong>Wall-Clock Time</strong></td>
                <td class="cell-num">2,516&nbsp;s (41m&nbsp;56s)</td>
                <td class="cell-num">2,946&nbsp;s (49m&nbsp;06s)</td>
                <td class="cell-win">1,178&nbsp;s (19m&nbsp;38s)</td>
                <td class="cell-num">1,682&nbsp;s (28m&nbsp;02s)</td>
                <td class="cell-win">1.43× faster (-8m&nbsp;24s)</td>
              </tr>
              <tr>
                <td><strong>Uncached Input Tokens</strong></td>
                <td class="cell-num">488,327&nbsp;tokens</td>
                <td class="cell-num">773,756&nbsp;tokens</td>
                <td class="cell-win">316,255&nbsp;tokens</td>
                <td class="cell-num">491,682&nbsp;tokens</td>
                <td class="cell-win">1.55× fewer uncached</td>
              </tr>
              <tr>
                <td><strong>Cumulative Input Tokens (Total)</strong></td>
                <td class="cell-num">9,457,637&nbsp;tokens</td>
                <td class="cell-num">17,502,629&nbsp;tokens</td>
                <td class="cell-win">3,080,276&nbsp;tokens</td>
                <td class="cell-num">6,897,615&nbsp;tokens</td>
                <td class="cell-win">2.24× fewer vs Bare (3.07× vs CE v1)</td>
              </tr>
              <tr>
                <td><strong>Output Tokens</strong></td>
                <td class="cell-num">98,913&nbsp;tokens</td>
                <td class="cell-num">127,093&nbsp;tokens</td>
                <td class="cell-win">41,472&nbsp;tokens</td>
                <td class="cell-num">61,150&nbsp;tokens</td>
                <td class="cell-win">1.47× fewer output tokens</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">4. Per-task breakdown: Iteration 2 (Context-Pack v2 + @Module Graph) and Iteration 1 archive.</h2>
          <p class="band-lede">
            Detailed telemetry for each of the 3 zero-spoiler tasks. Notice Task 2 in Iteration 2: CodeExplorer executed only 2 shell commands in the entire session (1 call to /api/context on Turn 1 and 1 call to tsgo + /api/gates/check on the final turn, with 0 ripgrep invocations).
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Iteration 2 per-task breakdown">
          <div class="table-caption-bar">
            <span>Table 2A: Iteration 2 Per-Task Scorecard (With @Module Cycle Safety)</span>
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
            <tbody>
              <tr>
                <td><strong>Task 1: API Key Revocation</strong><br><span class="tone-muted">Delete scoped usageLimit rows, recompute cache &amp; drop quota counters</span></td>
                <td><strong>CE v2</strong></td>
                <td class="cell-win">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-win">16&nbsp;turns<br><span class="tone-muted">25&nbsp;tools (7/12/5)</span></td>
                <td class="cell-win">322&nbsp;s (5m&nbsp;22s)</td>
                <td class="cell-win">77,390&nbsp;tokens</td>
                <td class="cell-win">772,814&nbsp;tokens<br><span class="tone-muted">2.85× fewer</span></td>
                <td class="cell-win">11,782&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 1: API Key Revocation</strong><br><span class="tone-muted">Baseline ripgrep + read_file</span></td>
                <td>Bare</td>
                <td class="cell-num">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-num">26&nbsp;turns<br><span class="tone-muted">55&nbsp;tools (11/39/4)</span></td>
                <td class="cell-num">587&nbsp;s (9m&nbsp;47s)</td>
                <td class="cell-num">142,325&nbsp;tokens</td>
                <td class="cell-num">2,203,949&nbsp;tokens</td>
                <td class="cell-num">23,368&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 2: Application Deletion</strong><br><span class="tone-muted">Cascade publicDomain teardown + external DNS custom hostname removal</span></td>
                <td><strong>CE v2</strong></td>
                <td class="cell-win">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-win">16&nbsp;turns<br><span class="tone-muted">28&nbsp;tools (2/19/6)</span></td>
                <td class="cell-win">252&nbsp;s (4m&nbsp;12s)</td>
                <td class="cell-win">85,483&nbsp;tokens</td>
                <td class="cell-win">881,006&nbsp;tokens<br><span class="tone-muted">2.34× fewer</span></td>
                <td class="cell-win">8,811&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 2: Application Deletion</strong><br><span class="tone-muted">Baseline ripgrep + read_file</span></td>
                <td>Bare</td>
                <td class="cell-num">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-num">26&nbsp;turns<br><span class="tone-muted">48&nbsp;tools (15/26/6)</span></td>
                <td class="cell-num">425&nbsp;s (7m&nbsp;05s)</td>
                <td class="cell-num">127,830&nbsp;tokens</td>
                <td class="cell-num">2,062,682&nbsp;tokens</td>
                <td class="cell-num">13,997&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 3: Subdomain Collisions</strong><br><span class="tone-muted">Prevent subdomain &amp; suggestion collisions with registered PublicDomain</span></td>
                <td><strong>CE v2</strong></td>
                <td class="cell-win">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-win">27&nbsp;turns<br><span class="tone-muted">42&nbsp;tools (11/24/6)</span></td>
                <td class="cell-win">604&nbsp;s (10m&nbsp;04s)</td>
                <td class="cell-win">153,382&nbsp;tokens</td>
                <td class="cell-win">1,426,456&nbsp;tokens<br><span class="tone-muted">1.84× fewer</span></td>
                <td class="cell-win">20,879&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 3: Subdomain Collisions</strong><br><span class="tone-muted">Baseline ripgrep + read_file</span></td>
                <td>Bare</td>
                <td class="cell-num">5/5 Jest<br>7/7 SIG</td>
                <td class="cell-num">32&nbsp;turns<br><span class="tone-muted">62&nbsp;tools (15/39/7)</span></td>
                <td class="cell-num">670&nbsp;s (11m&nbsp;10s)</td>
                <td class="cell-num">221,527&nbsp;tokens</td>
                <td class="cell-num">2,630,984&nbsp;tokens</td>
                <td class="cell-num">23,785&nbsp;tokens</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="table-surface" role="region" aria-label="Iteration 1 per-task archive" style="margin-top: var(--space-xl);">
          <div class="table-caption-bar">
            <span>Table 2B: Iteration 1 Per-Task Archive (Before @Module Dependency Graph)</span>
            <span class="measured">Preserved baseline run archive</span>
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
            <tbody>
              <tr>
                <td><strong>Task 1: API Key Revocation</strong></td>
                <td><strong>CE v1</strong></td>
                <td class="cell-win">5/5 Jest · 7/7 SIG</td>
                <td class="cell-win">31&nbsp;turns · 60&nbsp;tools (12/43/4)</td>
                <td class="cell-win">509&nbsp;s (8m&nbsp;29s)</td>
                <td class="cell-win">151,694&nbsp;tokens</td>
                <td class="cell-win">2,858,456&nbsp;tokens (1.76×)</td>
                <td class="cell-win">26,197&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 1: API Key Revocation</strong></td>
                <td>Bare</td>
                <td class="cell-num">5/5 Jest · 7/7 SIG</td>
                <td class="cell-num">40&nbsp;turns · 92&nbsp;tools (19/67/5)</td>
                <td class="cell-num">676&nbsp;s (11m&nbsp;16s)</td>
                <td class="cell-num">212,272&nbsp;tokens</td>
                <td class="cell-num">5,033,482&nbsp;tokens</td>
                <td class="cell-num">31,381&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 2: Application Deletion</strong></td>
                <td><strong>CE v1</strong></td>
                <td class="cell-win">5/5 Jest · 7/7 SIG</td>
                <td class="cell-win">38&nbsp;turns · 69&nbsp;tools (20/40/6)</td>
                <td class="cell-win">1,039&nbsp;s (17m&nbsp;19s)</td>
                <td class="cell-win">188,875&nbsp;tokens</td>
                <td class="cell-win">4,166,906&nbsp;tokens (1.97×)</td>
                <td class="cell-win">37,142&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 2: Application Deletion</strong></td>
                <td>Bare</td>
                <td class="cell-num">5/5 Jest · 7/7 SIG</td>
                <td class="cell-num">55&nbsp;turns · 109&nbsp;tools (39/60/9)</td>
                <td class="cell-num">1,216&nbsp;s (20m&nbsp;16s)</td>
                <td class="cell-num">273,548&nbsp;tokens</td>
                <td class="cell-num">8,204,360&nbsp;tokens</td>
                <td class="cell-num">51,125&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 3: Subdomain Collisions</strong></td>
                <td><strong>CE v1</strong></td>
                <td class="cell-win">5/5 Jest · 7/7 SIG</td>
                <td class="cell-win">31&nbsp;turns · 60&nbsp;tools (13/38/6)</td>
                <td class="cell-win">968&nbsp;s (16m&nbsp;08s)</td>
                <td class="cell-win">147,758&nbsp;tokens</td>
                <td class="cell-win">2,432,275&nbsp;tokens (1.75×)</td>
                <td class="cell-win">35,574&nbsp;tokens</td>
              </tr>
              <tr>
                <td><strong>Task 3: Subdomain Collisions</strong></td>
                <td>Bare</td>
                <td class="cell-num">5/5 Jest · 7/7 SIG</td>
                <td class="cell-num">45&nbsp;turns · 82&nbsp;tools (24/48/9)</td>
                <td class="cell-num">1,054&nbsp;s (17m&nbsp;34s)</td>
                <td class="cell-num">287,936&nbsp;tokens</td>
                <td class="cell-num">4,264,787&nbsp;tokens</td>
                <td class="cell-num">44,587&nbsp;tokens</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band" id="prompts" style="border-bottom: none;">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">5. Verbatim zero-spoiler prompts &amp; compiled Turn-1 context pack.</h2>
          <p class="band-lede">
            Inspect the exact prompts given to both agents and the compiled Turn-1 output of <code>/api/context</code> (including <code>moduleWiringAndCycleSafety</code> and <code>legacyTrapsInSubgraphFiles</code>).
          </p>
        </div>

        <div class="prompt-stack">
          <details class="prompt-drawer" open>
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge badge--accent">Turn 1 Output</code>
                <span>Compiled Context Pack for Task 1 (/api/context?q=api+key+revoke+usage+limit)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block">query: api key revoke usage limit
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
                <code class="badge badge--ok">System Prompt</code>
                <span>CodeExplorer Context-Pack v2 Agent System Prompt (ce-agent-v2.md)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block"># Agent System Instructions (CodeExplorer Context-Pack v2 + @Module Cycle Safety)

You are a Senior Backend Engineer working inside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (\`packages/twenty-server\`).

## Strict Sandbox Rules
- Only read, search, edit, or create files inside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (excluding \`.codeexplorer/\`).
- Never read or access any path outside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (no access to parent benchmark folders, \`code-explorer\`, or \`antigravity/brain\`).
- Do not create \`.spec.ts\` or \`.test.ts\` files.

## Workspace Architectural Invariants (Strictly Enforced)
1. **Thin Transport Handlers (\`&lt;= 45 LOC\`, Zero Direct DB Access)**: GraphQL \`@Resolver\` and REST \`@Controller\` classes must keep handler methods \`&lt;= 45 LOC\` and must NEVER inject TypeORM \`@InjectRepository\` / \`Repository\` / \`DataSource\` directly  -  delegate persistence and domain logic to domain services.
2. **Strict Module Table Ownership**: Never inject another module's TypeORM Entity repository or query foreign domain/metadata tables directly across module boundaries (even if older legacy methods in the same file do so!). Import the owning NestJS module (avoiding circular NestJS module dependencies  -  extract a focused sub-module/service inside the owning domain if needed) and call its exported domain service so caches, counters, and external side-effects stay consistent.
3. **Async Queue &amp; Exception Hygiene**: Enqueue background jobs via \`MessageQueueService\` (never call job \`.handle()\` directly), and never leave empty \`catch\` blocks without handling/rethrowing/documenting.

## Workflow (Minimal Turns, Maximum Precision)
1. **Turn 1  -  One-Shot Graph Context Pack**: Call the warm CodeExplorer context compiler ONCE via \`run_command\`:
   \`Invoke-RestMethod "http://127.0.0.1:19580/api/context?q=&lt;plus-separated-keywords&gt;"\`
   (Pass 3 to 6 domain keywords from the task, e.g. \`api+key+revoke+usage+limit\` or \`application+delete+public+domain+dns\` or \`subdomain+availability+public+domain\`).
   This returns in one shot:
   - \`anchors\`, \`serviceMethods\`, \`outboundSideEffects\`, \`inboundCallersAndRoutes\`, \`tablesAndCanonicalOwners\`
   - \`moduleWiringAndCycleSafety\`: lists the exact \`owningModules\` (\`*.module.ts\` file paths, \`provides\`, \`directModuleImports\`) and pre-computed \`importSafetyVerdicts\` (\`SAFE_DIRECT_IMPORT\` vs \`CYCLE_WARNING\` with the exact cycle path).
   - \`guardrails\` (\`activeRules\` and \`legacyTrapsInSubgraphFiles\`) and \`readPlan\`.
   **IMPORTANT**: Trust \`moduleWiringAndCycleSafety.importSafetyVerdicts\`! When it says \`SAFE_DIRECT_IMPORT\`, import the module directly without grepping or reading unrelated \`*.module.ts\` files across the repo. When it says \`CYCLE_WARNING\`, follow its guidance or extract a focused cycle-free sub-module.
2. **Turn 2  -  Batch File Reads**: Read the files in \`readPlan\` and the 1 to 2 \`owningModules\` \`*.module.ts\` files in parallel \`read_file\` calls within a SINGLE turn. Only use \`rg\` if a specific constant/enum name is still needed.
3. **Implement**: Apply clean, minimal edits via \`edit_file\` / \`write_file\`.
4. **Final Turn  -  Single Verification Command**: Verify TypeScript compilation AND architectural gates in one command:
   \`&amp; "C:/Work/bench-pilot/repos/twenty-ctx-exp/node_modules/@typescript/native-preview-win32-x64/lib/tsgo.exe" -p "C:/Work/bench-pilot/repos/twenty-ctx-exp/packages/twenty-server/tsconfig.json" --declaration false --noEmit; Invoke-RestMethod "http://127.0.0.1:19580/api/gates/check"\`
   Ensure \`0\` TypeScript errors and \`newViolations: 0\` (\`status: passed\`), then return a concise summary.</pre>
            </div>
          </details>

          <details class="prompt-drawer">
            <summary>
              <span class="prompt-drawer-title">
                <code class="badge">System Prompt</code>
                <span>Bare Agent Baseline System Prompt (bare-agent.md)</span>
              </span>
              <span class="prompt-drawer-toggle"></span>
            </summary>
            <div class="prompt-drawer-body">
              <pre class="code-block"># Agent System Instructions (Bare Agent Baseline: ripgrep + read_file + edit_file)

You are a Senior Backend Engineer working inside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (\`packages/twenty-server\`).

## Strict Sandbox Rules
- Only read, search, edit, or create files inside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (excluding \`.codeexplorer/\`).
- Never read or access any path outside \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (no access to parent benchmark folders, \`code-explorer\`, or \`antigravity/brain\`), and never connect to local ports or \`ce\` CLI.
- Do not create \`.spec.ts\` or \`.test.ts\` files.

## Workspace Architectural Invariants (Strictly Enforced)
1. **Thin Transport Handlers (\`&lt;= 45 LOC\`, Zero Direct DB Access)**: GraphQL \`@Resolver\` and REST \`@Controller\` classes must keep handler methods \`&lt;= 45 LOC\` and must NEVER inject TypeORM \`@InjectRepository\` / \`Repository\` / \`DataSource\` directly  -  delegate persistence and domain logic to domain services.
2. **Strict Module Table Ownership**: Never inject another module's TypeORM Entity repository or query foreign domain/metadata tables directly across module boundaries (even if older legacy methods in the same file do so!). Import the owning NestJS module (avoiding circular NestJS module dependencies  -  extract a focused sub-module/service inside the owning domain if needed) and call its exported domain service so caches, counters, and external side-effects stay consistent.
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
              <pre class="code-block"># Bug Report: API Key Revocation Leaves Scoped Usage Limits, Cache &amp; Counters Behind

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
              <pre class="code-block"># Bug Report: Deleting an Application Orphans Its Public Domains and External DNS Hostnames

Implement the fix in \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (\`packages/twenty-server\`).

## Problem Description
Custom applications installed in a workspace can own registered public domains (\`publicDomain\` rows linked by \`applicationId\` and \`workspaceId\`).

Currently, when a workspace application is deleted (by \`universalIdentifier\` and \`workspaceId\`) in \`twenty-server\`, any public domains owned by that application are left orphaned in external DNS infrastructure or removed without running Twenty's full public-domain teardown lifecycle.

## Required Behavior
Update the workspace application deletion flow (taking \`universalIdentifier\` and \`workspaceId\`) so that:
1. If no application with \`universalIdentifier\` exists in \`workspaceId\`, throw the existing \`APPLICATION_NOT_FOUND\` exception without deleting any public domains or calling external DNS teardown.
2. When deleting an existing application in \`workspaceId\`, remove all public domains owned by that application in \`workspaceId\` through Twenty's canonical public-domain removal lifecycle  -  including tearing down each domain's external custom hostname (\`{ isPublicDomain: true }\`) and recomputing the relevant workspace caches  -  while leaving public domains and DNS hostnames belonging to other applications or other workspaces untouched.</pre>
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
              <pre class="code-block"># Bug Report: Workspace Subdomain Validation &amp; Suggestions Allow Collisions With Registered Public Domains

Implement the fix in \`C:/Work/bench-pilot/repos/twenty-ctx-exp\` (\`packages/twenty-server\`).

## Problem Description
In multi-workspace mode, workspaces choose a \`subdomain\`, and Twenty also allows registering custom \`PublicDomain\` hostnames. When the server configuration setting \`PUBLIC_DOMAIN_URL\` is a valid URL (for example \`https://twenty.app\` or \`https://Twenty.App:443\`), a workspace \`&lt;subdomain&gt;\` derives the public hostname \`&lt;subdomain&gt;.&lt;publicBaseHostname&gt;\` (case-insensitive, stripping any port).

Currently, Twenty's workspace subdomain validation, availability check (\`checkWorkspaceSubdomainAvailability\`), and available-subdomain suggestion generator only check collisions against existing \`Workspace.subdomain\` rows  -  allowing a workspace to claim or be suggested a subdomain whose derived \`&lt;subdomain&gt;.&lt;publicBaseHostname&gt;\` collides with an already-registered \`PublicDomain\`!

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
    </section>`}};function cu(){if(typeof window>"u"||typeof document>"u")return()=>{};const w=[{src:"all_services.png",description:"<strong>System Map:</strong> Services, HTTP/gRPC routes, queues, and SQL tables built straight from source code.",pills:['<span class="metric-chip metric-chip--accent">Synced with code</span>','<span class="metric-chip metric-chip--ok">Local SQLite</span>','<span class="metric-chip">Click to zoom</span>']},{src:"selected_services.png",description:"<strong>Focused Slice:</strong> Select the services you care about and hide surrounding noise while keeping indirect links.",pills:['<span class="metric-chip metric-chip--accent">Blast radius</span>','<span class="metric-chip metric-chip--ok">Noise filtered</span>','<span class="metric-chip">Click to zoom</span>']},{src:"project_flow.png",description:"<strong>Service Call Flow:</strong> Trace HTTP calls, broker events, and database reads and writes before editing code.",pills:['<span class="metric-chip metric-chip--accent">End-to-end flow</span>','<span class="metric-chip metric-chip--ok">SQL and queue lineage</span>','<span class="metric-chip">Click to zoom</span>']}];function D(A,ne){const $=w[A];if(!$)return;const Ee=document.getElementById("showcaseImage"),Xe=document.getElementById("showcaseDescription"),Me=document.getElementById("showcasePills"),vt=document.getElementById("showcasePathLabel");document.querySelectorAll("#cockpit .tab-btn").forEach(Lt=>Lt.classList.remove("active")),ne&&ne.classList.add("active"),Ee.style.opacity="0.35",setTimeout(()=>{Ee.src=$.src,Xe.innerHTML=$.description,Me.innerHTML=$.pills.join(""),vt&&(vt.textContent=$.src),Ee.style.opacity="1"},120)}function g(){const A=document.getElementById("lightboxModal"),ne=document.getElementById("lightboxImg"),$=document.getElementById("showcaseImage");ne.src=$.src,A.classList.add("open")}function V(){document.getElementById("lightboxModal").classList.remove("open")}document.addEventListener("keydown",A=>{A.key==="Escape"&&V()});const G={blast_radius:{tag:"blast_radius.cypher",raw:'MATCH path = (caller:Method)-[:CALLS*1..4]->(target:Method {name: "ChargeAsync"}) RETURN caller.fullName AS UpstreamCaller, length(path) AS Hops ORDER BY Hops ASC LIMIT 10',codeHtml:`<span class="syn-kw">MATCH</span> path = (caller:Method)-[:CALLS*<span class="syn-num">1..4</span>]-&gt;(target:Method {name: <span class="syn-str">"ChargeAsync"</span>})
<span class="syn-kw">RETURN</span> caller.fullName <span class="syn-kw">AS</span> UpstreamCaller,
       <span class="syn-fn">length</span>(path) <span class="syn-kw">AS</span> Hops
<span class="syn-kw">ORDER BY</span> Hops <span class="syn-kw">ASC</span>
<span class="syn-kw">LIMIT</span> <span class="syn-num">10</span>`,explanation:"Finds all upstream callers within 4 hops that can impact PaymentProcessor.ChargeAsync.",timing:"4 rows in 3.8 ms",headers:["UpstreamCaller","Hops"],rows:[["Billing.Application.Commands.CaptureOrderHandler.Handle","1"],["Orders.Api.Controllers.CheckoutController.SubmitOrder","2"],["Subscriptions.Worker.Consumers.RenewalConsumer.Consume","2"],["Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute","3"]]},cqrs_pipeline:{tag:"cqrs_pipeline.cypher",raw:"MATCH (ep:Endpoint)-[:CALLS*1..2]->(cmd:Class)-[:HANDLED_BY]->(h:Class)-[:WRITES_TO]->(t:SqlTable) RETURN ep.route AS Route, cmd.name AS Command, h.name AS Handler, t.name AS Table",codeHtml:`<span class="syn-kw">MATCH</span> (ep:Endpoint)-[:CALLS*<span class="syn-num">1..2</span>]-&gt;(cmd:Class)-[:HANDLED_BY]-&gt;(h:Class)-[:WRITES_TO]-&gt;(t:SqlTable)
<span class="syn-kw">RETURN</span> ep.route <span class="syn-kw">AS</span> Route,
       cmd.name <span class="syn-kw">AS</span> Command,
       h.name <span class="syn-kw">AS</span> Handler,
       t.name <span class="syn-kw">AS</span> Table`,explanation:"Traces HTTP endpoints through MediatR/CQRS commands to handlers and mutated SQL tables.",timing:"3 rows in 4.2 ms",headers:["Route","Command","Handler","Table"],rows:[["POST /api/v1/orders","CreateOrderCommand","CreateOrderCommandHandler","orders.purchase_orders"],["POST /api/v1/refunds","IssueRefundCommand","IssueRefundCommandHandler","billing.ledger_entries"],["PUT /api/v1/inventory/reserve","ReserveStockCommand","ReserveStockHandler","warehouse.stock_reservations"]]},api_to_db:{tag:"api_to_db.cypher",raw:'MATCH (s:Service)-[:CONTAINS*]->(m:Method)-[op:READS_FROM|WRITES_TO]->(tbl:SqlTable {name: "customer_ledgers"}) RETURN s.name AS Service, m.name AS Method, type(op) AS Operation',codeHtml:`<span class="syn-kw">MATCH</span> (s:Service)-[:CONTAINS*]-&gt;(m:Method)-[op:READS_FROM|WRITES_TO]-&gt;(tbl:SqlTable {name: <span class="syn-str">"customer_ledgers"</span>})
<span class="syn-kw">RETURN</span> s.name <span class="syn-kw">AS</span> Service,
       m.name <span class="syn-kw">AS</span> Method,
       <span class="syn-fn">type</span>(op) <span class="syn-kw">AS</span> Operation`,explanation:"Discovers every microservice and repository method reading from or writing to a shared database table.",timing:"4 rows in 2.9 ms",headers:["Service","Method","Operation"],rows:[["Billing.Service","LedgerRepository.AppendEntryAsync","WRITES_TO"],["Billing.Service","BalanceQueryService.GetBalanceAsync","READS_FROM"],["Reporting.Worker","DailyReconciliationJob.ExportLedgers","READS_FROM"],["Fraud.Engine","RiskScorer.EvaluateRecentVelocity","READS_FROM"]]},cyclic_deps:{tag:"cyclic_deps.cypher",raw:"MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON*1..3]->(a) WHERE a.name < b.name RETURN a.name AS ProjectA, b.name AS ProjectB",codeHtml:`<span class="syn-kw">MATCH</span> (a:Project)-[:DEPENDS_ON]-&gt;(b:Project)-[:DEPENDS_ON*<span class="syn-num">1..3</span>]-&gt;(a)
<span class="syn-kw">WHERE</span> a.name &lt; b.name
<span class="syn-kw">RETURN</span> a.name <span class="syn-kw">AS</span> ProjectA,
       b.name <span class="syn-kw">AS</span> ProjectB`,explanation:"Detects direct or multi-hop circular project dependencies across the solution.",timing:"2 rows in 1.9 ms",headers:["ProjectA","ProjectB"],rows:[["Orders.Application","Shared.Fulfillment.Contracts"],["Identity.Core","Tenancy.Resolution"]]}};let re="blast_radius";function be(A,ne){const $=G[A];if(!$)return;re=A,document.querySelectorAll("#playground .preset-btn").forEach(Me=>Me.classList.remove("active")),ne&&ne.classList.add("active"),document.getElementById("queryPresetTag").textContent=$.tag,document.getElementById("queryCodeArea").innerHTML=$.codeHtml,document.getElementById("queryExplanation").textContent=$.explanation,document.getElementById("queryTimingBadge").textContent=$.timing;const Ee=$.headers.map(Me=>`<th>${Me}</th>`).join(""),Xe=$.rows.map(Me=>`<tr>${Me.map(vt=>`<td>${vt}</td>`).join("")}</tr>`).join("");document.getElementById("queryResultsArea").innerHTML=`<table class="data-table"><thead><tr>${Ee}</tr></thead><tbody>${Xe}</tbody></table>`}function ye(){const A=document.getElementById("runQueryBtn"),ne=document.getElementById("queryTimingBadge"),$=G[re];A.textContent="Running...",ne.textContent="Executing query...",setTimeout(()=>{A.textContent="Run Query",ne.textContent=$.timing},180)}function Z(){const ne=`ce query '${G[re].raw}'`;Ce(ne,"Copied OpenCypher CLI command!")}const de={boundaries:{file:".codeexplorer/gates/domain-purity.yaml",dimension:"Layer boundary",description:"Blocks any type inside a Domain module from depending on Infrastructure or EF Core types.",status:"Exit 1 (2 violations)",statusOk:!1,rawYaml:`id: ARCH-001
name: Domain Layer Must Not Reference Infrastructure
severity: error
query: |
  MATCH (d:Class)-[:DEPENDS_ON|CALLS]->(inf:Class)
  WHERE d.namespace CONTAINS ".Domain"
    AND (inf.namespace CONTAINS ".Infrastructure" OR inf.namespace CONTAINS "Microsoft.EntityFrameworkCore")
  RETURN d.fullName AS Violator, inf.fullName AS ForbiddenTarget`,yamlHtml:`<span class="syn-kw">id:</span> <span class="syn-str">ARCH-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">Domain Layer Must Not Reference Infrastructure</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (d:Class)-[:DEPENDS_ON|CALLS]-&gt;(inf:Class)
  <span class="syn-kw">WHERE</span> d.namespace <span class="syn-kw">CONTAINS</span> <span class="syn-str">".Domain"</span>
    <span class="syn-kw">AND</span> (inf.namespace <span class="syn-kw">CONTAINS</span> <span class="syn-str">".Infrastructure"</span>
      <span class="syn-kw">OR</span> inf.namespace <span class="syn-kw">CONTAINS</span> <span class="syn-str">"Microsoft.EntityFrameworkCore"</span>)
  <span class="syn-kw">RETURN</span> d.fullName <span class="syn-kw">AS</span> Violator, inf.fullName <span class="syn-kw">AS</span> ForbiddenTarget`,violations:[{rule:"ARCH-001 (error)",target:"Orders.Domain.Aggregates.Order -> Orders.Infrastructure.Persistence.OrderDbContext",loc:"src/Orders.Domain/Aggregates/Order.cs:84"},{rule:"ARCH-001 (error)",target:"Billing.Domain.Services.InvoiceCalculator -> Billing.Infrastructure.Http.TaxRateClient",loc:"src/Billing.Domain/Services/InvoiceCalculator.cs:41"}]},topology:{file:".codeexplorer/gates/no-shared-database-writes.yaml",dimension:"Table ownership",description:"Forbids two distinct microservices from writing to the same SQL table directly.",status:"Exit 1 (1 violation)",statusOk:!1,rawYaml:`id: TOPO-004
name: Single-Writer Microservice Table Ownership
severity: error
query: |
  MATCH (s1:Service)-[:CONTAINS*]->(:Method)-[:WRITES_TO]->(t:SqlTable)<-[:WRITES_TO]-(:Method)<-[:CONTAINS*]-(s2:Service)
  WHERE s1.name < s2.name
  RETURN t.name AS SharedTable, s1.name AS WriterA, s2.name AS WriterB`,yamlHtml:`<span class="syn-kw">id:</span> <span class="syn-str">TOPO-004</span>
<span class="syn-kw">name:</span> <span class="syn-str">Single-Writer Microservice Table Ownership</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (s1:Service)-[:CONTAINS*]-&gt;(:Method)-[:WRITES_TO]-&gt;(t:SqlTable)
        &lt;-[:WRITES_TO]-(:Method)&lt;-[:CONTAINS*]-(s2:Service)
  <span class="syn-kw">WHERE</span> s1.name &lt; s2.name
  <span class="syn-kw">RETURN</span> t.name <span class="syn-kw">AS</span> SharedTable, s1.name <span class="syn-kw">AS</span> WriterA, s2.name <span class="syn-kw">AS</span> WriterB`,violations:[{rule:"TOPO-004 (error)",target:'Table "inventory.stock_items" written by both Checkout.Service and Warehouse.Service',loc:"src/Checkout.Service/Repositories/StockWriter.cs:59"}]},visitor:{file:".codeexplorer/gates/no-sync-over-async.yaml",dimension:"Async safety",description:"Catches blocking .Result or .Wait() calls inside async methods that cause thread-pool starvation.",status:"Exit 1 (2 violations)",statusOk:!1,rawYaml:`id: AST-012
name: No Blocking .Result or .Wait() Inside Async Methods
severity: error
query: |
  MATCH (m:Method {isAsync: true})-[:CALLS]->(b:Method)
  WHERE b.name IN ["get_Result", "Wait", "GetResult"]
  RETURN m.fullName AS AsyncMethod, b.name AS BlockingCall, m.filePath AS File`,yamlHtml:`<span class="syn-kw">id:</span> <span class="syn-str">AST-012</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Blocking .Result or .Wait() Inside Async Methods</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (m:Method {isAsync: <span class="syn-num">true</span>})-[:CALLS]-&gt;(b:Method)
  <span class="syn-kw">WHERE</span> b.name <span class="syn-kw">IN</span> [<span class="syn-str">"get_Result"</span>, <span class="syn-str">"Wait"</span>, <span class="syn-str">"GetResult"</span>]
  <span class="syn-kw">RETURN</span> m.fullName <span class="syn-kw">AS</span> AsyncMethod, b.name <span class="syn-kw">AS</span> BlockingCall`,violations:[{rule:"AST-012 (error)",target:"Payments.Api.WebhookController.HandleStripeAsync calls Task.get_Result",loc:"src/Payments.Api/Controllers/WebhookController.cs:112"},{rule:"AST-012 (error)",target:"Notifications.Worker.EmailDispatcher.SendBatchAsync calls Task.Wait",loc:"src/Notifications.Worker/EmailDispatcher.cs:67"}]},literal:{file:".codeexplorer/gates/no-hardcoded-urls.yaml",dimension:"Config hygiene",description:"Prevents AI agents from hardcoding internal service URLs or connection strings in production classes.",status:"Exit 1 (1 violation)",statusOk:!1,rawYaml:`id: SEC-003
name: No Hardcoded Internal Service URLs Outside Config
severity: error
query: |
  MATCH (c:Class)-[:CONTAINS*]->(lit:Literal)
  WHERE lit.value STARTS WITH "http://" AND NOT c.filePath CONTAINS "Tests"
  RETURN c.fullName AS ClassName, lit.value AS HardcodedLiteral`,yamlHtml:`<span class="syn-kw">id:</span> <span class="syn-str">SEC-003</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Hardcoded Internal Service URLs Outside Config</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (c:Class)-[:CONTAINS*]-&gt;(lit:Literal)
  <span class="syn-kw">WHERE</span> lit.value <span class="syn-kw">STARTS WITH</span> <span class="syn-str">"http://"</span>
    <span class="syn-kw">AND NOT</span> c.filePath <span class="syn-kw">CONTAINS</span> <span class="syn-str">"Tests"</span>
  <span class="syn-kw">RETURN</span> c.fullName <span class="syn-kw">AS</span> ClassName, lit.value <span class="syn-kw">AS</span> HardcodedLiteral`,violations:[{rule:"SEC-003 (error)",target:'Shipping.Infrastructure.CarrierClient contains literal "http://carrier-internal.prod:8080/v2"',loc:"src/Shipping.Infrastructure/Clients/CarrierClient.cs:29"}]},ratchet:{file:".codeexplorer/gate-baseline.json",dimension:"Baseline ratchet",description:"Suppresses fingerprinted legacy debt while failing CI immediately on any net-new violation.",status:"Exit 0 (0 new, 3 baselined)",statusOk:!0,rawYaml:`{
  "schemaVersion": 1,
  "ratchetMode": "strict",
  "baselinedViolations": [
    { "gateId": "ARCH-001", "fingerprint": "sha256:9f84c2e1a0b3", "owner": "legacy-orders" },
    { "gateId": "TOPO-004", "fingerprint": "sha256:4d19b7c8e512", "owner": "legacy-warehouse" },
    { "gateId": "AST-012",  "fingerprint": "sha256:7a31e0f4c920", "owner": "legacy-billing" }
  ]
}`,yamlHtml:`{
  <span class="syn-kw">"schemaVersion"</span>: <span class="syn-num">1</span>,
  <span class="syn-kw">"ratchetMode"</span>: <span class="syn-str">"strict"</span>,
  <span class="syn-kw">"baselinedViolations"</span>: [
    { <span class="syn-kw">"gateId"</span>: <span class="syn-str">"ARCH-001"</span>, <span class="syn-kw">"fingerprint"</span>: <span class="syn-str">"sha256:9f84c2e1a0b3"</span> },
    { <span class="syn-kw">"gateId"</span>: <span class="syn-str">"TOPO-004"</span>, <span class="syn-kw">"fingerprint"</span>: <span class="syn-str">"sha256:4d19b7c8e512"</span> },
    { <span class="syn-kw">"gateId"</span>: <span class="syn-str">"AST-012"</span>,  <span class="syn-kw">"fingerprint"</span>: <span class="syn-str">"sha256:7a31e0f4c920"</span> }
  ]
}`,violations:[{rule:"RATCHET PASS (0 net-new violations)",target:"3 known legacy violations matched baseline fingerprints; 0 new violations introduced in PR.",loc:".codeexplorer/gate-baseline.json (verified in 6.4 ms)",ok:!0}]}};let we="boundaries";function b(A,ne){const $=de[A];if(!$)return;we=A,document.querySelectorAll("#gates .preset-btn").forEach(Me=>Me.classList.remove("active")),ne&&ne.classList.add("active"),document.getElementById("gateFileLabel").textContent=$.file,document.getElementById("gateDimensionBadge").textContent=$.dimension,document.getElementById("gateYamlArea").innerHTML=$.yamlHtml,document.getElementById("gateDescriptionText").textContent=$.description;const Ee=document.getElementById("gateStatusBadge");Ee.textContent=$.status,Ee.className=$.statusOk?"measured tone-ok":"measured tone-bad";const Xe=$.violations.map(Me=>`
        <div class="violation-item ${Me.ok?"violation-item--ok":""}">
          <strong class="${Me.ok?"tone-ok":"tone-bad"}">${Me.rule}</strong>
          <span>${Me.target}</span>
          <span class="tone-muted">${Me.loc}</span>
        </div>
      `).join("");document.getElementById("gateViolationsArea").innerHTML=Xe}function M(){const A=de[we];Ce(A.rawYaml,"Copied Gate configuration to clipboard!")}function j(A,ne){document.querySelectorAll("#install .tab-btn").forEach(Ee=>Ee.classList.remove("active")),ne&&ne.classList.add("active"),document.querySelectorAll("#install .install-pane").forEach(Ee=>Ee.classList.remove("active"));const $=document.getElementById("tab-"+A);$&&$.classList.add("active")}function _(A){Ce(A,"Copied install command to clipboard!")}function Ie(){Ce(`{
  "mcpServers": {
    "code-explorer": {
      "command": "ce",
      "args": ["mcp"]
    }
  }
}`,"Copied MCP Server JSON config!")}function Ce(A,ne){try{const $=A.includes("brew ")?"/event/copy-brew":A.includes("mcp")?"/event/copy-mcp":"/event/copy-cli";navigator.sendBeacon&&navigator.sendBeacon($)}catch{}if(navigator.clipboard&&navigator.clipboard.writeText)navigator.clipboard.writeText(A).then(()=>it(ne));else{const $=document.createElement("textarea");$.value=A,document.body.appendChild($),$.select(),document.execCommand("copy"),document.body.removeChild($),it(ne)}}function it(A){const ne=document.getElementById("toast");ne.textContent=A,ne.classList.add("show"),setTimeout(()=>ne.classList.remove("show"),2400)}window.switchShowcase=D,window.openLightbox=g,window.closeLightbox=V,window.loadQueryPreset=be,window.runQueryAnimation=ye,window.copyCurrentQuery=Z,window.loadGatePreset=b,window.copyCurrentGateYaml=M,window.switchInstallTab=j,window.copyInstallCmd=_,window.copyMCPConfig=Ie;const yt=document.querySelector("#playground .preset-btn");yt&&be("blast_radius",yt);const ft=document.querySelector("#gates .preset-btn");return ft&&b("boundaries",ft),fetch("/version.json",{cache:"no-cache"}).then(A=>A.ok?A.json():null).then(A=>{const ne=A&&(A.tag_name||A.version);if(ne){const $=ne.startsWith("v")?ne:"v"+ne,Ee=document.getElementById("heroReleaseBadge"),Xe=document.getElementById("footerCeVersion");Ee&&(Ee.textContent=$),Xe&&(Xe.textContent="CodeExplorer CLI & Extension "+$)}}).catch(()=>{}),()=>{}}const du="/",pu=()=>{Ea.useEffect(()=>cu(),[]);const w=jn[du];return R.jsx("main",{className:"page-home",dangerouslySetInnerHTML:{__html:w.mainHtml}})},uu=()=>R.jsxs(R.Fragment,{children:[R.jsx("div",{className:"lightbox-modal",id:"lightboxModal",role:"dialog","aria-modal":"true","aria-label":"Architecture Cockpit screenshot preview",children:R.jsx("img",{id:"lightboxImg",src:"all_services.png",alt:"Enlarged CodeExplorer Architecture Cockpit view"})}),R.jsx("div",{className:"toast",id:"toast",role:"status","aria-live":"polite",children:"Copied to clipboard!"})]});function mu(){if(typeof window>"u"||typeof document>"u")return()=>{};var w={agnostic:{domain:"/Domain/",app:"/Application/",infra:"/Infrastructure/",ui:"/Api/",modPrefix:"Modules/",slicePrefix:"Features/"},dotnet:{domain:".Domain/",app:".Application/",infra:".Infrastructure/",ui:".Api/",modPrefix:"src/Modules/",slicePrefix:"Features/"},typescript:{domain:"src/domain/",app:"src/application/",infra:"src/infrastructure/",ui:"src/controllers/",modPrefix:"packages/",slicePrefix:"src/features/"},go:{domain:"internal/domain/",app:"internal/usecase/",infra:"internal/adapter/repository/",ui:"internal/adapter/http/",modPrefix:"internal/modules/",slicePrefix:"internal/features/"},python:{domain:"/domain/",app:"/use_cases/",infra:"/adapters/orm/",ui:"/routers/",modPrefix:"apps/",slicePrefix:"features/"},java:{domain:"/domain/",app:"/application/",infra:"/infrastructure/",ui:"/web/",modPrefix:"modules/",slicePrefix:"features/"}},D={"clean-architecture":{title:"Clean Architecture (Onion Dependency Rule)",subtitle:"All source code dependencies point strictly inward toward Domain entities.",note:"Why AST + SQL graph beats import linters: import linters only check file imports. CodeExplorer catches transitive call chains, DI interface resolutions, and raw SQL or ORM table accesses crossing layer boundaries.",kv:[["Best fit","Complex domain rules, long-lived backends"],["Avoid when","Thin CRUD microservices or prototypes"],["Graph layers used","L2 Project + L3 Call/Type + L4 SQL/ORM"],["Legacy repo onboarding","SHA-256 ratchet (0 Day-1 rewrites)"]],layers:function(b){return[{name:"Presentation & Delivery (API / UI / CLI)",paths:b.ui+"**",core:!1},{name:"Infrastructure & Persistence (ORM / HTTP / Queue)",paths:b.infra+"**",core:!1},{name:"Application Use Cases (Commands / Queries / Ports)",paths:b.app+"**",core:!1},{name:"Domain Core (Entities / Value Objects / Policies)",paths:b.domain+"** (0 outward deps)",core:!0}]},edges:[{label:"Presentation to Application",allow:!0,code:"ALLOW"},{label:"Infrastructure to Application & Domain",allow:!0,code:"ALLOW"},{label:"Domain to Infrastructure or Web",allow:!1,code:"SIG-CA-B-001",drawer:"drawer-0"},{label:"Controller direct to DbContext / SQL",allow:!1,code:"SIG-CA-B-003",drawer:"drawer-2"}],gates:function(b,M){return[{id:"SIG-CA-B-001",name:"Domain Core Purity",coords:"Module x Boundaries",pattern:"(Domain)-[:DEPENDS_ON|CALLS]->(Application|Infrastructure|Presentation)",severity:"Error",description:"Domain types and functions must never call or depend on Application, Infrastructure, or Presentation layers.",query:`MATCH (src:Function)-[:CALLS]->(dst:Function)
  WHERE src.file_path CONTAINS '`+b.domain+`'
    AND (dst.file_path CONTAINS '`+b.infra+"' OR dst.file_path CONTAINS '"+b.app+"' OR dst.file_path CONTAINS '"+b.ui+`')
  RETURN src.file_path AS file, src.start_line AS line, src.name AS symbol, dst.name AS target,
         'Domain symbol ' + src.name + ' calls outer-layer symbol ' + dst.name AS message`,platform:M},{id:"SIG-CA-B-002",name:"Application Layer Isolation",coords:"Module x Boundaries",pattern:"(Application)-[:DEPENDS_ON|CALLS]->(Infrastructure|Presentation)",severity:"Error",description:"Application use cases orchestrate Domain logic without importing concrete Infrastructure or Delivery adapters.",query:`MATCH (src:Function)-[:CALLS]->(dst:Function)
  WHERE src.file_path CONTAINS '`+b.app+`'
    AND (dst.file_path CONTAINS '`+b.infra+"' OR dst.file_path CONTAINS '"+b.ui+`')
  RETURN src.file_path AS file, src.start_line AS line, src.name AS symbol, dst.name AS target,
         'Application use case ' + src.name + ' depends directly on adapter ' + dst.name AS message`,platform:M},{id:"SIG-CA-B-003",name:"No Delivery-to-Database Bypass",coords:"Method x Boundaries",pattern:"(Endpoint|Controller)-[:CALLS|READS_FROM|WRITES_TO]->(Query|Table)",severity:"Error",description:"Delivery handlers must dispatch through Application use cases rather than issuing SQL or ORM queries directly.",query:`MATCH (q:Query)-[:ACCESSED_BY]->(fn:Function)
  WHERE fn.file_path CONTAINS '`+b.ui+`'
  RETURN fn.file_path AS file, fn.start_line AS line, fn.name AS symbol, q.name AS target,
         'Delivery handler ' + fn.name + ' executes database query directly, bypassing Application layer' AS message`,platform:M},{id:"SIG-CA-C-004",name:"Persistence-Ignorant Domain Entities",coords:"Class x Contract",pattern:"(Type {layer:'Domain'}) with ORM or HTTP transport annotations",severity:"Warning",description:"Domain entities should stay free of database table or HTTP serialization decorators.",query:`MATCH (t:Type)
  WHERE t.file_path CONTAINS '`+b.domain+`'
    AND (coalesce(t.attributes, '') CONTAINS 'Table' OR coalesce(t.attributes, '') CONTAINS 'Entity' OR coalesce(t.attributes, '') CONTAINS 'JsonProperty')
  RETURN t.file_path AS file, t.start_line AS line, t.name AS symbol, t.attributes AS target,
         'Domain type ' + t.name + ' carries outer framework annotation ' + t.attributes AS message`,platform:M},{id:"SIG-CA-B-005",name:"Acyclic Project Dependency Graph",coords:"Module x Boundaries",pattern:"(p1:Project)-[:DEPENDS_ON]->(p2:Project)-[:DEPENDS_ON]->(p1)",severity:"Error",description:"Projects must form a strict directed acyclic graph without mutual dependencies.",query:`MATCH (p1:Project)-[:DEPENDS_ON]->(p2:Project)-[:DEPENDS_ON]->(p1)
  WHERE p1.id < p2.id
  RETURN p1.path AS file, 1 AS line, p1.name AS symbol, p2.name AS target,
         'Circular project dependency between ' + p1.name + ' and ' + p2.name AS message`,platform:M}]}},hexagonal:{title:"Hexagonal Architecture (Ports & Adapters)",subtitle:"Inbound driving adapters and outbound driven adapters meet exclusively through Core Port interfaces.",note:"Prevents HTTP controllers or CLI commands from short-circuiting directly into SQL repositories, Redis clients, or external SDKs.",kv:[["Best fit","Services with multiple protocols (HTTP, gRPC, CLI, Queue) and swappable infra"],["Avoid when","Single-adapter scripts with zero external integrations"],["Graph layers used","L3 Interface IMPLEMENTS + L4 Endpoint / ExternalService / DB"],["Legacy repo onboarding","SHA-256 ratchet (0 Day-1 rewrites)"]],layers:function(b){return[{name:"Driving Inbound Adapters (HTTP / gRPC / Queue Consumers)",paths:b.ui+"**",core:!1},{name:"Application Ports (Inbound & Outbound Interfaces)",paths:b.app+"ports/**",core:!0},{name:"Domain & Application Core",paths:b.domain+"**",core:!0},{name:"Driven Outbound Adapters (Postgres / Kafka / HTTP SDKs)",paths:b.infra+"**",core:!1}]},edges:[{label:"Driving Adapter to Inbound Port",allow:!0,code:"ALLOW"},{label:"Driven Adapter implements Outbound Port",allow:!0,code:"ALLOW"},{label:"Driving Adapter direct to Driven Adapter",allow:!1,code:"SIG-HEX-B-001",drawer:"drawer-0"},{label:"Core importing SDK or DB driver",allow:!1,code:"SIG-HEX-B-002",drawer:"drawer-1"}],gates:function(b,M){return[{id:"SIG-HEX-B-001",name:"No Adapter-to-Adapter Short-Circuit",coords:"Method x Boundaries",pattern:"(DrivingAdapter)-[:CALLS]->(DrivenAdapter) bypassing Core Port",severity:"Error",description:"Inbound HTTP/gRPC/CLI adapters must never invoke outbound persistence or external SDK adapters directly.",query:`MATCH (inbound:Function)-[:CALLS]->(outbound:Function)
  WHERE inbound.file_path CONTAINS '`+b.ui+"' AND outbound.file_path CONTAINS '"+b.infra+`'
  RETURN inbound.file_path AS file, inbound.start_line AS line, inbound.name AS symbol, outbound.name AS target,
         'Inbound adapter ' + inbound.name + ' bypasses Core Port and calls outbound adapter ' + outbound.name AS message`,platform:M},{id:"SIG-HEX-B-002",name:"Hexagon Core Infrastructure Isolation",coords:"Module x Boundaries",pattern:"(Core)-[:CALLS]->(DrivenAdapter|ExternalService)",severity:"Error",description:"Domain and Port definitions inside the hexagon must never call concrete adapter implementations.",query:`MATCH (core:Function)-[:CALLS]->(adapter:Function)
  WHERE (core.file_path CONTAINS '`+b.domain+"' OR core.file_path CONTAINS '"+b.app+`')
    AND adapter.file_path CONTAINS '`+b.infra+`'
  RETURN core.file_path AS file, core.start_line AS line, core.name AS symbol, adapter.name AS target,
         'Hexagon core function ' + core.name + ' calls concrete driven adapter ' + adapter.name AS message`,platform:M},{id:"SIG-HEX-C-003",name:"Driven Repository Implements Port Interface",coords:"Class x Contract",pattern:"Repository or Adapter class without IMPLEMENTS -> Port interface",severity:"Warning",description:"Every concrete outbound repository or client adapter must implement an explicit Port interface.",query:`MATCH (repo:Type)
  WHERE repo.file_path CONTAINS '`+b.infra+`' AND repo.name ENDS WITH 'Repository'
    AND coalesce(repo.implements, '') = ''
  RETURN repo.file_path AS file, repo.start_line AS line, repo.name AS symbol, 'PortInterface' AS target,
         'Driven adapter ' + repo.name + ' does not implement an outbound Port interface' AS message`,platform:M},{id:"SIG-HEX-B-004",name:"External HTTP Calls Confined to Driven Adapters",coords:"Service x Boundaries",pattern:"(ExternalService)-[:DECLARED_IN]->(File outside Driven Adapters)",severity:"Error",description:"Outbound HTTP/RPC calls must originate strictly from driven adapter modules.",query:`MATCH (es:ExternalService)-[:DECLARED_IN]->(f:File)
  WHERE NOT f.path CONTAINS '`+b.infra+`'
  RETURN f.path AS file, coalesce(es.start_line, 1) AS line, es.name AS symbol, es.url AS target,
         'External service call ' + es.name + ' is declared outside driven adapters' AS message`,platform:M}]}},"modular-monolith":{title:"Modular Monolith (Strict Module & Table Encapsulation)",subtitle:"Single deployable binary split into isolated business modules that share neither internal types nor SQL tables.",note:"The #1 killer of Modular Monoliths is cross-module SQL table joins and writes. CodeExplorer's L4 SQL lineage blocks cross-module table access automatically.",kv:[["Best fit","Growing product teams wanting microservice boundaries without distributed ops"],["Avoid when","Modules require independent hardware scaling or polyglot runtimes"],["Graph layers used","L2 Module + L3 Public Facade + L4 SQL READS_FROM / WRITES_TO"],["Legacy repo onboarding","Freeze cross-module SQL debt via ratchet, block all new leaks"]],layers:function(b){return[{name:"Module Public Contracts (Contracts / Events / Facades)",paths:b.modPrefix+"*/Contracts/**",core:!0},{name:"Module A Internals (Orders: Handlers, Domain, DB)",paths:b.modPrefix+"Orders/Internal/**",core:!1},{name:"Module B Internals (Billing: Handlers, Domain, DB)",paths:b.modPrefix+"Billing/Internal/**",core:!1},{name:"Module-Owned SQL Tables (orders_*, billing_*)",paths:"L4 Table ownership boundary",core:!1}]},edges:[{label:"Module A to Module B Public Contract",allow:!0,code:"ALLOW"},{label:"Module A writing its own SQL tables",allow:!0,code:"ALLOW"},{label:"Module A calling Module B Internal types",allow:!1,code:"SIG-MM-B-001",drawer:"drawer-0"},{label:"Module A writing Module B SQL tables",allow:!1,code:"SIG-MM-B-002",drawer:"drawer-1"}],gates:function(b,M){return[{id:"SIG-MM-B-001",name:"No Cross-Module Internal Symbol Calls",coords:"Module x Boundaries",pattern:"(ModuleA)-[:CALLS]->(ModuleB/Internal) bypassing Contracts",severity:"Error",description:"Modules may only invoke another module through its Contracts or Public API namespace.",query:`MATCH (caller:Function)-[:CALLS]->(callee:Function)
  WHERE caller.project_name <> callee.project_name
    AND callee.file_path CONTAINS '`+b.modPrefix+`'
    AND (callee.file_path CONTAINS '/Internal/' OR callee.file_path CONTAINS '/Domain/' OR callee.file_path CONTAINS '/Persistence/')
  RETURN caller.file_path AS file, caller.start_line AS line, caller.name AS symbol, callee.name AS target,
         'Cross-module encapsulation breach: ' + caller.project_name + ' calls internal symbol ' + callee.name AS message`,platform:M},{id:"SIG-MM-B-002",name:"Cross-Module Database Table Write Shield",coords:"Service x Boundaries",pattern:"(Two distinct Modules)<-[:DECLARED_IN]-(Query)-[:WRITES_TO]->(Shared Table)",severity:"Error",description:"Each database table must have a single owning module responsible for mutations.",query:`MATCH (q1:Query)-[:WRITES_TO]->(t:Table)<-[:WRITES_TO]-(q2:Query)
  MATCH (q1)-[:ACCESSED_BY]->(f1:Function), (q2)-[:ACCESSED_BY]->(f2:Function)
  WHERE f1.project_name < f2.project_name
  RETURN f1.file_path AS file, f1.start_line AS line, f1.project_name AS symbol, t.name AS target,
         'Table ' + t.name + ' is mutated by multiple modules: ' + f1.project_name + ' and ' + f2.project_name AS message`,platform:M},{id:"SIG-MM-B-003",name:"No Cross-Module Direct SQL Table Reads",coords:"Service x Boundaries",pattern:"(ModuleA Query)-[:READS_FROM]->(Table owned/written by ModuleB)",severity:"Warning",description:"Modules should read foreign module data via query facades or integration events instead of SQL joins.",query:`MATCH (qw:Query)-[:WRITES_TO]->(t:Table)<-[:READS_FROM]-(qr:Query)
  MATCH (qw)-[:ACCESSED_BY]->(fw:Function), (qr)-[:ACCESSED_BY]->(fr:Function)
  WHERE fw.project_name <> fr.project_name
  RETURN fr.file_path AS file, fr.start_line AS line, fr.name AS symbol, t.name AS target,
         'Module ' + fr.project_name + ' reads table ' + t.name + ' owned by module ' + fw.project_name AS message`,platform:M},{id:"SIG-MM-B-004",name:"Shared Kernel Purity",coords:"Module x Boundaries",pattern:"(SharedKernel)-[:DEPENDS_ON|CALLS]->(BusinessModule)",severity:"Error",description:"Shared building blocks must never depend back on any concrete business module.",query:`MATCH (pShared:Project)-[:DEPENDS_ON]->(pMod:Project)
  WHERE (pShared.name CONTAINS 'Shared' OR pShared.name CONTAINS 'Common' OR pShared.name CONTAINS 'BuildingBlocks')
    AND pMod.path CONTAINS '`+b.modPrefix+`'
  RETURN pShared.path AS file, 1 AS line, pShared.name AS symbol, pMod.name AS target,
         'Shared kernel ' + pShared.name + ' depends on business module ' + pMod.name AS message`,platform:M},{id:"SIG-MM-B-005",name:"Acyclic Inter-Module Contracts",coords:"Module x Boundaries",pattern:"(ModA)-[:DEPENDS_ON]->(ModB)-[:DEPENDS_ON]->(ModA)",severity:"Error",description:"Module contract dependencies must remain strictly acyclic.",query:`MATCH (m1:Project)-[:DEPENDS_ON]->(m2:Project)-[:DEPENDS_ON]->(m1)
  WHERE m1.id < m2.id
  RETURN m1.path AS file, 1 AS line, m1.name AS symbol, m2.name AS target,
         'Bidirectional coupling between modules ' + m1.name + ' and ' + m2.name AS message`,platform:M}]}},"vertical-slice":{title:"Vertical Slice Architecture (Zero Lateral Slice Coupling)",subtitle:"Organize code by self-contained feature slices rather than horizontal technical layers.",note:"AI coding agents love reusing helpers from a neighboring feature folder, silently tangling independent slices into a distributed spaghetti ball.",kv:[["Best fit","Feature-driven product apps, CQRS command/query pipelines"],["Avoid when","Heavy shared algorithmic engines with minimal feature endpoints"],["Graph layers used","L1 Folder Slice + L3 CALLS / INSTANTIATES"],["Legacy repo onboarding","Instant slice isolation check across Features/**"]],layers:function(b){return[{name:"Slice 1: CreateOrder (Endpoint + Command + Validator + SQL)",paths:b.slicePrefix+"Orders/CreateOrder/**",core:!1},{name:"Slice 2: CancelOrder (Endpoint + Command + Validator + SQL)",paths:b.slicePrefix+"Orders/CancelOrder/**",core:!1},{name:"Slice 3: IssueInvoice (Endpoint + Command + Validator + SQL)",paths:b.slicePrefix+"Billing/IssueInvoice/**",core:!1},{name:"Shared Domain & Infrastructure Primitives",paths:"Shared/**, Domain/** (inward only)",core:!0}]},edges:[{label:"Feature Slice to Shared Domain",allow:!0,code:"ALLOW"},{label:"Self-contained Handler to DB inside Slice",allow:!0,code:"ALLOW"},{label:"Feature Slice A calling Feature Slice B",allow:!1,code:"SIG-VSA-B-001",drawer:"drawer-0"},{label:"Shared folder calling into a Feature Slice",allow:!1,code:"SIG-VSA-B-002",drawer:"drawer-1"}],gates:function(b,M){return[{id:"SIG-VSA-B-001",name:"Zero Lateral Feature-to-Feature Calls",coords:"Module x Boundaries",pattern:"(Features/SliceA)-[:CALLS]->(Features/SliceB)",severity:"Error",description:"Feature slices must remain independent and never directly call handlers, DTOs, or helpers in sibling slices.",query:`MATCH (a:Function)-[:CALLS]->(b:Function)
  WHERE a.file_path CONTAINS '`+b.slicePrefix+"' AND b.file_path CONTAINS '"+b.slicePrefix+`'
    AND split(a.file_path, '/')[2] <> split(b.file_path, '/')[2]
  RETURN a.file_path AS file, a.start_line AS line, a.name AS symbol, b.name AS target,
         'Feature slice function ' + a.name + ' couples laterally to sibling slice symbol ' + b.name AS message`,platform:M},{id:"SIG-VSA-B-002",name:"Shared Primitives Never Call Feature Slices",coords:"Module x Boundaries",pattern:"(Shared|Common|Domain)-[:CALLS]->(Features/*)",severity:"Error",description:"Shared infrastructure and domain primitives must never depend upward on specific feature slices.",query:`MATCH (sh:Function)-[:CALLS]->(feat:Function)
  WHERE NOT sh.file_path CONTAINS '`+b.slicePrefix+"' AND feat.file_path CONTAINS '"+b.slicePrefix+`'
    AND (sh.file_path CONTAINS 'Shared' OR sh.file_path CONTAINS 'Common' OR sh.file_path CONTAINS 'Domain')
  RETURN sh.file_path AS file, sh.start_line AS line, sh.name AS symbol, feat.name AS target,
         'Shared symbol ' + sh.name + ' depends upward on feature slice ' + feat.name AS message`,platform:M},{id:"SIG-VSA-R-003",name:"Single-Responsibility Slice Handler Size",coords:"Method x Resources",pattern:"Slice Handler method exceeding 150 LOC",severity:"Warning",description:"Keep vertical slice handlers concise by pushing complex invariants into pure domain methods.",query:`MATCH (fn:Function)
  WHERE fn.file_path CONTAINS '`+b.slicePrefix+`' AND coalesce(fn.loc, 0) > 150
  RETURN fn.file_path AS file, fn.start_line AS line, fn.name AS symbol, toString(fn.loc) + ' LOC' AS target,
         'Slice handler ' + fn.name + ' exceeds 150 LOC ceiling' AS message`,platform:M},{id:"SIG-VSA-C-004",name:"No Bloated Generic Repository in Vertical Slices",coords:"Class x Contract",pattern:"Generic Repository class (>15 methods) shared across slices",severity:"Warning",description:"Vertical slices favor direct query/command access over giant shared repository classes.",query:`MATCH (t:Type)-[:HAS_METHOD]->(m:Function)
  WHERE t.name ENDS WITH 'Repository'
  WITH t, count(m) AS methods WHERE methods > 15
  RETURN t.file_path AS file, t.start_line AS line, t.name AS symbol, toString(methods) + ' methods' AS target,
         'Shared repository ' + t.name + ' accumulates ' + toString(methods) + ' methods across slices' AS message`,platform:M}]}},"feature-sliced-design":{title:"Feature-Sliced Design (FSD Frontend Hierarchy)",subtitle:"Strict downward layer hierarchy: app -> pages -> widgets -> features -> entities -> shared.",note:"Frontend repos suffer constant upward import drift when AI agents import a widget or feature inside entities or shared UI components.",kv:[["Best fit","Large React, Next.js, Vue, or Angular frontends"],["Avoid when","Small marketing sites or single-screen widgets"],["Graph layers used","L1 File/Folder + L3 Function/Type CALLS & references"],["Legacy repo onboarding","Ratchet existing FSD violations in 1 command"]],layers:function(){return[{name:"1. app & pages (Routing, Providers, Full Route Views)",paths:"src/app/**, src/pages/**",core:!1},{name:"2. widgets (Composite Page Blocks)",paths:"src/widgets/**",core:!1},{name:"3. features (User Interactions & Business Actions)",paths:"src/features/**",core:!1},{name:"4. entities & shared (Domain Models & Reusable UI Kit)",paths:"src/entities/**, src/shared/**",core:!0}]},edges:[{label:"pages -> widgets -> features -> entities -> shared",allow:!0,code:"ALLOW"},{label:"Slice consumed via public index.ts",allow:!0,code:"ALLOW"},{label:"entities or shared importing features/widgets",allow:!1,code:"SIG-FSD-B-001",drawer:"drawer-0"},{label:"Sibling feature-to-feature cross-call",allow:!1,code:"SIG-FSD-B-002",drawer:"drawer-1"}],gates:function(b,M){var j=M==="agnostic"?"typescript":M;return[{id:"SIG-FSD-B-001",name:"No Upward Layer Dependencies in FSD",coords:"Module x Boundaries",pattern:"(shared|entities)-[:CALLS]->(features|widgets|pages|app)",severity:"Error",description:"Lower FSD layers (shared, entities) must never reference higher layers (features, widgets, pages, app).",query:`MATCH (low:Function)-[:CALLS]->(high:Function)
  WHERE (low.file_path CONTAINS 'src/shared/' OR low.file_path CONTAINS 'src/entities/')
    AND (high.file_path CONTAINS 'src/features/' OR high.file_path CONTAINS 'src/widgets/' OR high.file_path CONTAINS 'src/pages/' OR high.file_path CONTAINS 'src/app/')
  RETURN low.file_path AS file, low.start_line AS line, low.name AS symbol, high.name AS target,
         'Upward FSD layer violation: ' + low.file_path + ' calls ' + high.file_path AS message`,platform:j},{id:"SIG-FSD-B-002",name:"No Cross-Imports Within Same FSD Layer",coords:"Module x Boundaries",pattern:"(src/features/Auth)-[:CALLS]->(src/features/Checkout)",severity:"Error",description:"Slices on the same FSD layer (features or entities) must not couple horizontally; compose them in widgets or pages.",query:`MATCH (a:Function)-[:CALLS]->(b:Function)
  WHERE a.file_path CONTAINS 'src/features/' AND b.file_path CONTAINS 'src/features/'
    AND split(a.file_path, '/')[2] <> split(b.file_path, '/')[2]
  RETURN a.file_path AS file, a.start_line AS line, a.name AS symbol, b.name AS target,
         'Horizontal FSD slice coupling between features/' + split(a.file_path, '/')[2] + ' and features/' + split(b.file_path, '/')[2] AS message`,platform:j},{id:"SIG-FSD-B-003",name:"Features Must Not Depend on Widgets or Pages",coords:"Module x Boundaries",pattern:"(src/features/*)-[:CALLS]->(src/widgets/* | src/pages/*)",severity:"Error",description:"Features sit below widgets and pages and must never call upward into composite page blocks.",query:`MATCH (f:Function)-[:CALLS]->(up:Function)
  WHERE f.file_path CONTAINS 'src/features/'
    AND (up.file_path CONTAINS 'src/widgets/' OR up.file_path CONTAINS 'src/pages/')
  RETURN f.file_path AS file, f.start_line AS line, f.name AS symbol, up.name AS target,
         'Feature ' + f.name + ' calls upward FSD layer ' + up.file_path AS message`,platform:j},{id:"SIG-FSD-C-004",name:"Shared Layer Business-Entity Agnosticism",coords:"Module x Contract",pattern:"src/shared/** referencing domain API endpoints directly",severity:"Warning",description:"src/shared should provide generic UI/lib primitives rather than domain-specific API calls.",query:`MATCH (es:ExternalService)-[:DECLARED_IN]->(f:File)
  WHERE f.path CONTAINS 'src/shared/' AND NOT f.path CONTAINS 'src/shared/api/'
  RETURN f.path AS file, coalesce(es.start_line, 1) AS line, es.name AS symbol, es.url AS target,
         'Domain network call ' + es.name + ' leaked into generic src/shared layer' AS message`,platform:j}]}},"microservices-isolation":{title:"Shared-Nothing Microservices (DB & Sync Fan-Out Guard)",subtitle:"Each microservice owns its database schema exclusively and avoids deep synchronous call cascades.",note:"In polyglot monorepos (Go + TS + .NET + Python), only a unified L4 graph sees when two services read or write the exact same SQL table.",kv:[["Best fit","Polyglot multi-service monorepos and distributed platforms"],["Avoid when","Single-database monoliths"],["Graph layers used","L2 Service + L4 Endpoint / ExternalService / Table / Topic"],["Legacy repo onboarding","Audit shared tables & freeze migration baseline"]],layers:function(){return[{name:"API Gateway / Edge BFF",paths:"services/gateway/**, apps/bff/**",core:!1},{name:"Autonomous Microservices (Order, Payment, Inventory)",paths:"services/*/**",core:!0},{name:"Async Event Broker (Kafka / RabbitMQ / PubSub Topics)",paths:"L4 Topic contracts",core:!1},{name:"Isolated Per-Service Databases",paths:"1 owner service per Table",core:!1}]},edges:[{label:"Service A -> Broker Topic -> Service B",allow:!0,code:"ALLOW"},{label:"Service A -> Service A Dedicated DB",allow:!0,code:"ALLOW"},{label:"Two Services accessing same SQL Table",allow:!1,code:"SIG-MS-B-001",drawer:"drawer-0"},{label:"Cyclic synchronous HTTP/RPC call loop",allow:!1,code:"SIG-MS-B-002",drawer:"drawer-1"}],gates:function(b,M){return[{id:"SIG-MS-B-001",name:"Shared-Database Integration Anti-Pattern",coords:"System x Boundaries",pattern:"(ServiceA) and (ServiceB) both READS_FROM or WRITES_TO (Table)",severity:"Error",description:"No database table may be accessed directly by more than one microservice.",query:`MATCH (q1:Query)-[:READS_FROM|WRITES_TO]->(t:Table)<-[:READS_FROM|WRITES_TO]-(q2:Query)
  MATCH (q1)-[:ACCESSED_BY]->(f1:Function), (q2)-[:ACCESSED_BY]->(f2:Function)
  WHERE f1.project_name < f2.project_name
  RETURN f1.file_path AS file, f1.start_line AS line, f1.project_name AS symbol, t.name AS target,
         'Shared database coupling on table ' + t.name + ' between ' + f1.project_name + ' and ' + f2.project_name AS message`,platform:M},{id:"SIG-MS-B-002",name:"Distributed Synchronous Call Cycle Shield",coords:"System x Boundaries",pattern:"(SvcA)-[:SERVICE_CALL]->(SvcB)-[:SERVICE_CALL]->(SvcA)",severity:"Error",description:"Synchronous HTTP/gRPC service-to-service calls must never form distributed cycles.",query:`MATCH (a:App)-[:SERVICE_CALL]->(b:App)-[:SERVICE_CALL]->(a)
  WHERE a.id < b.id
  RETURN a.path AS file, 1 AS line, a.name AS symbol, b.name AS target,
         'Distributed synchronous call cycle between ' + a.name + ' and ' + b.name AS message`,platform:M},{id:"SIG-MS-B-003",name:"No Direct Cross-Service Source Code Imports",coords:"Service x Boundaries",pattern:"(ServiceA:App)-[:DEPENDS_ON]->(ServiceB:App)",severity:"Error",description:"Deployable microservices may depend on shared libraries, never directly on another deployable service.",query:`MATCH (s1:App)-[:IMPLEMENTED_BY]->(p1:Project)-[:DEPENDS_ON]->(p2:Project)<-[:IMPLEMENTED_BY]-(s2:App)
  WHERE p1.id <> p2.id
  RETURN p1.path AS file, 1 AS line, p1.name AS symbol, p2.name AS target,
         'Deployable service ' + p1.name + ' directly imports deployable service ' + p2.name AS message`,platform:M},{id:"SIG-MS-R-004",name:"N+1 Synchronous HTTP Call Inside Loop",coords:"Method x Resources",pattern:"Outbound HTTP client call inside loop execution context",severity:"Warning",description:"Avoid sequential cross-service HTTP calls inside loops; prefer batch endpoints or async fan-out.",query:`MATCH (es:ExternalService)<-[:CALLED_BY]-(fn:Function)
  WHERE fn.loc > 25
  RETURN fn.file_path AS file, coalesce(es.start_line, fn.start_line) AS line, fn.name AS symbol, es.name AS target,
         'Verify outbound RPC call ' + es.name + ' in ' + fn.name + ' uses batching and explicit timeout' AS message`,platform:M}]}}},g="clean-architecture",V="agnostic";function G(b){return String(b).replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function re(b){var M=b.coords.split(" x ");return["id: "+b.id,"category: Architectural","platform: "+b.platform,"scope: "+M[0],"dimension: "+M[1],"severity: "+b.severity,"name: "+b.name,"description: "+b.description,"query: >-","  "+b.query].join(`
`)}function be(){var b=D[g],M=w[V],j=b.gates(M,V),_=`# CodeExplorer Semantic Invariant Gate Pack
# Pack: `+g+" | Stack: "+V+`
# Place in .codeexplorer/gates/`+g+`.yaml and run: ce gate check
---
`;return _+j.map(re).join(`
---
`)+`
`}function ye(){var b=D[g],M=w[V],j=b.layers(M),_=b.gates(M,V);document.getElementById("bp-title").textContent=b.title,document.getElementById("bp-subtitle").textContent=b.subtitle,document.getElementById("bp-note").innerHTML="<strong>Architectural guard note:</strong> "+G(b.note),document.getElementById("pack-slug-badge").textContent="pack: "+g,document.getElementById("cli-cmd").textContent="ce gate install "+g+" --stack "+V;var Ie=b.kv.map(function(A){return'<li class="kv-row"><span class="kv-label">'+G(A[0])+'</span><span class="kv-val">'+G(A[1])+"</span></li>"}).join("");document.getElementById("pack-kv").innerHTML=Ie;var Ce=j.map(function(A){var ne=A.core?"topo-layer topo-layer--core":"topo-layer";return'<div class="'+ne+'"><span class="topo-layer-name">'+G(A.name)+'</span><span class="topo-layer-paths">'+G(A.paths)+"</span></div>"}).join(""),it='<div class="topo-connectors">'+b.edges.map(function(A){return A.allow?'<div class="edge-rule edge-rule--allow"><span>'+G(A.label)+'</span><code class="edge-rule-code">Allowed</code></div>':'<a href="#'+G(A.drawer)+'" class="edge-rule edge-rule--block" data-open-drawer="'+G(A.drawer)+'"><span>'+G(A.label)+'</span><code class="edge-rule-code">'+G(A.code.toLowerCase())+"</code></a>"}).join("")+"</div>";document.getElementById("topology-stage").innerHTML=Ce+it,document.getElementById("invariants-heading").textContent="1. Included Semantic Invariant Gates: "+b.title.split(" (")[0]+".",document.getElementById("table-pack-caption").textContent="Pack: "+g+" ("+_.length+" executable invariants)",document.getElementById("table-stack-caption").textContent="Stack preset: "+V,document.getElementById("gates-tbody").innerHTML=_.map(function(A){var ne=A.severity==="Error"?"badge badge--bad":"badge badge--accent";return"<tr><td><code>"+G(A.id)+"</code><br><strong>"+G(A.name)+"</strong></td><td>"+G(A.coords)+"</td><td><code>"+G(A.pattern)+'</code></td><td class="cell-num"><code class="'+ne+'">'+G(A.severity)+"</code></td></tr>"}).join("");var yt='<div class="prompt-stack-head"><h3 class="prompt-stack-title">Inspect executable YAML &amp; OpenCypher definitions</h3><span class="prompt-stack-note">Drop directly into <code>.codeexplorer/gates/</code> or adapt paths with your AI agent</span></div>',ft=_.map(function(A,ne){var $=ne===0?" open":"",Ee=A.severity==="Error"?"badge badge--bad":"badge badge--accent";return'<details id="drawer-'+ne+'" class="prompt-drawer"'+$+'><summary><span class="prompt-drawer-title"><code class="'+Ee+'">'+G(A.id)+"</code><span>"+G(A.name)+' (OpenCypher YAML)</span></span><span class="prompt-drawer-toggle"></span></summary><div class="prompt-drawer-body"><pre class="code-block">'+G(re(A))+"</pre></div></details>"}).join("");document.getElementById("yaml-drawers").innerHTML=yt+ft,document.querySelectorAll(".arch-tab").forEach(function(A){A.setAttribute("aria-selected",A.getAttribute("data-pack")===g?"true":"false")}),document.querySelectorAll(".stack-btn").forEach(function(A){A.setAttribute("aria-pressed",A.getAttribute("data-stack")===V?"true":"false")})}function Z(){var b=be(),M=new Blob([b],{type:"text/yaml;charset=utf-8"}),j=URL.createObjectURL(M),_=document.createElement("a");_.href=j,_.download=g+"-"+V+".gates.yaml",document.body.appendChild(_),_.click(),document.body.removeChild(_),URL.revokeObjectURL(j)}function de(b,M){var j=b.textContent;b.textContent=M,setTimeout(function(){b.textContent=j},1600)}var we=function(b){var M=b.target.closest(".arch-tab");if(M){g=M.getAttribute("data-pack"),ye();return}var j=b.target.closest(".stack-btn");if(j){V=j.getAttribute("data-stack"),ye();return}var _=b.target.closest("[data-select-pack]");if(_){g=_.getAttribute("data-select-pack"),ye(),document.getElementById("workbench").scrollIntoView({behavior:"smooth"});return}var Ie=b.target.closest("[data-open-drawer]");if(Ie){var Ce=Ie.getAttribute("data-open-drawer"),it=document.getElementById(Ce);it&&(b.preventDefault(),it.open=!0,it.scrollIntoView({behavior:"smooth",block:"center"}));return}};return document.addEventListener("click",we),document.getElementById("download-pack-btn").addEventListener("click",Z),document.getElementById("hero-download-btn").addEventListener("click",Z),document.getElementById("copy-cli-btn").addEventListener("click",function(){var b=this,M=document.getElementById("cli-cmd").textContent;navigator.clipboard&&navigator.clipboard.writeText(M).then(function(){de(b,"Copied")})}),document.getElementById("copy-prompt-btn").addEventListener("click",function(){var b=this,M="Inspect my workspace using CodeExplorer get_architecture_map, adapt the following '"+g+"' ("+V+`) Semantic Invariant Gate pack to match our actual folder/project paths, save the rules to .codeexplorer/gates/, run check_semantic_gates, and freeze existing baseline debt via ce gate ratchet --update:

`+be();navigator.clipboard&&navigator.clipboard.writeText(M).then(function(){de(b,"Prompt Copied")})}),()=>{document.removeEventListener("click",we)}}const hu="/architectures",gu=()=>{Ea.useEffect(()=>mu(),[]);const w=jn[hu];return R.jsx("main",{className:"arch-page",dangerouslySetInnerHTML:{__html:w.mainHtml}})};function yu(){if(typeof window>"u"||typeof document>"u")return()=>{};for(var w={commonProperties:[{name:"id",sourceName:"Id",type:"string",description:"Globally unique deterministic URN identifier of the node."},{name:"name",sourceName:"Name",type:"string",description:"Human-readable symbol, contract, or resource name."},{name:"path",sourceName:"Path",type:"string",description:"Workspace-relative path of the container or declaration."},{name:"project_id",sourceName:"ProjectId",type:"string",description:"URN of the enclosing compilation project, when applicable."}],nodes:[{kind:"Domain",pluralLabel:"Domains",layerId:5,layerKey:"L5_logical",icon:"globe",order:1,idScheme:"{workspaceId}:dom:{domainKey}",purpose:"Represents a high-level business domain (e.g. Core, Supporting, Generic, Shared) containing one or more bounded contexts in Level 5 (Logical Structure).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The canonical domain name."},{name:"display_name",sourceName:"DisplayName",type:"string",description:"The human-readable display name of the domain."},{name:"domain_type",sourceName:"DomainType",type:"enum (DomainType)",description:"The DDD classification of the domain (Core, Supporting, Generic, Shared)."},{name:"summary",sourceName:"Summary",type:"string",description:"A concise summary of the domain's business responsibility."},{name:"path",sourceName:"Path",type:"string",description:"Primary path associated with the domain."}],outEdges:[{rel:"DEPENDS_ON",toKind:"Domain",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",toKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"Domain",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."}],sampleCypher:`MATCH (n:Domain)-[:DEPENDS_ON]->(t:Domain)
RETURN n.name, n.display_name, n.domain_type, t.name
LIMIT 25`},{kind:"BoundedContext",pluralLabel:"Bounded Contexts",layerId:5,layerKey:"L5_logical",icon:"layers",order:2,idScheme:"{workspaceId}:bc:{contextKey}",purpose:"Represents a DDD Bounded Context in Level 5 (Logical Structure) that encapsulates semantic workloads, data stores, topics, and domain types.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The canonical bounded context name."},{name:"display_name",sourceName:"DisplayName",type:"string",description:"The human-readable display name of the bounded context."},{name:"domain_id",sourceName:"DomainId",type:"string",description:"The parent domain ID that owns this bounded context."},{name:"summary",sourceName:"Summary",type:"string",description:"A concise summary of the bounded context's business responsibility."},{name:"purity_percentage",sourceName:"PurityPercentage",type:"number",description:"Domain purity percentage (0-100)."},{name:"path",sourceName:"Path",type:"string",description:"Primary path associated with the bounded context."}],outEdges:[{rel:"DEPENDS_ON",toKind:"BoundedContext",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",toKind:"Database",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Module",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Topic",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DEFINES",toKind:"Type",category:"structural",description:"Indicates that an entity defines a particular property or configuration."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"BoundedContext",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",fromKind:"Domain",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:BoundedContext)-[:DEPENDS_ON]->(t:BoundedContext)
RETURN n.name, n.display_name, n.domain_id, t.name
LIMIT 25`},{kind:"Module",pluralLabel:"Software Modules",layerId:4,layerKey:"L4_semantic",icon:"package",order:0,idScheme:"{workspaceId}:{kindPrefix}:{moduleName}",purpose:"Abstract base software module encompassing executable workloads, shared libraries, and automated test suites.",isAbstract:!0,subtypes:["Workload","Library","TestSuite"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name."},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace."},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language."},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module."},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework."}],outEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."}],inEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies."},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Module)-[:DEPENDS_ON]->(t:Library)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Workload",pluralLabel:"Runtime Workloads",layerId:4,layerKey:"L4_semantic",icon:"server",order:1,idScheme:"{workspaceId}:{workloadPrefix}:{workloadName}",purpose:"Abstract executable runtime workload encompassing backend services, client applications, background workers, and CLI tools.",isAbstract:!0,extends:"Module",ancestors:["Module"],subtypes:["App","Service","Worker","CliTool"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to."},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."}],inEdges:[{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."}],inheritedOutEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Workload)-[:SERVICE_CALL]->(t:ExternalService)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"App",pluralLabel:"Applications",layerId:4,layerKey:"L4_semantic",icon:"browser",order:2,idScheme:"{workspaceId}:app:{appName}",purpose:"Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular, Electron).",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[],inEdges:[],inheritedOutEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",inheritedFrom:"Workload"},{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:App)-[:SERVICE_CALL]->(t:ExternalService)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Service",pluralLabel:"Services",layerId:4,layerKey:"L4_semantic",icon:"server-process",order:3,idScheme:"{workspaceId}:s:{serviceName}",purpose:"Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server).",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"CALLED_BY",toKind:"Function",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"EXPOSES",toKind:"Endpoint",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."},{rel:"COORDINATES_WITH",toKind:"PeerMesh",category:"type_dependency",description:"Links a workload to the symmetric peer mesh it coordinates with."}],inEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."}],inheritedOutEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",inheritedFrom:"Workload"},{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Service)-[:CALLED_BY]->(t:Function)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Worker",pluralLabel:"Workers",layerId:4,layerKey:"L4_semantic",icon:"gear",order:4,idScheme:"{workspaceId}:w:{workerName}",purpose:"Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service).",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"SUBSCRIBES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."}],inEdges:[],inheritedOutEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",inheritedFrom:"Workload"},{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Worker)-[:SUBSCRIBES_TO]->(t:Topic)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"CliTool",pluralLabel:"CLI Tools",layerId:4,layerKey:"L4_semantic",icon:"terminal",order:5,idScheme:"{workspaceId}:cli:{toolName}",purpose:"Represents a command-line tool, developer script, or administrative CLI utility.",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[],inEdges:[],inheritedOutEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",inheritedFrom:"Workload"},{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:CliTool)-[:SERVICE_CALL]->(t:ExternalService)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Library",pluralLabel:"Libraries & SDKs",layerId:4,layerKey:"L4_semantic",icon:"library",order:6,idScheme:"{workspaceId}:lib:{libraryName}",purpose:"Represents a shared library, utility module, DTO package, or domain contract reused across services.",extends:"Module",ancestors:["Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[],inEdges:[{rel:"DEPENDS_ON",fromKind:"Module",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."}],inheritedOutEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Library)-[:DEPENDS_ON]->(t:Library)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"TestSuite",pluralLabel:"Test Suites",layerId:4,layerKey:"L4_semantic",icon:"beaker",order:7,idScheme:"{workspaceId}:suite:{suiteName}",purpose:"Represents an automated test project, runner suite, or test module (e.g. NUnit/xUnit test project, Jest test suite, pytest suite).",extends:"Module",ancestors:["Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"TESTS",toKind:"Module",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies."}],inEdges:[],inheritedOutEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:TestSuite)-[:TESTS]->(t:Module)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Endpoint",pluralLabel:"API & Message Endpoints (REST, gRPC, WS, MSG)",layerId:4,layerKey:"L4_semantic",icon:"radio-tower",order:10,idScheme:"{workspaceId}:ep:{httpMethod}:{routeTemplate}",purpose:"Represents an exposed external API or message contract endpoint (REST, gRPC, GraphQL, WebSocket, WCF, or Broker Consumer).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The endpoint name (e.g. GET /api/orders, MSG:SubmitOrder)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"http_method",sourceName:"HttpMethod",type:"string",description:"The HTTP Verb or contract verb (GET, POST, PUT, DELETE, ALL, MSG, WS, RPC)."},{name:"route_template",sourceName:"RouteTemplate",type:"string",description:"The declared route template or message contract name."},{name:"protocol",sourceName:"Protocol",type:"string",description:"The contract protocol (REST, gRPC, GraphQL, WS, WCF, MSG, RPC)."},{name:"is_anonymous",sourceName:"IsAnonymous",type:"boolean",description:"Whether endpoint allows unauthenticated anonymous access."},{name:"required_roles",sourceName:"RequiredRoles",type:"string",description:"Comma-separated required roles or permissions."},{name:"policies",sourceName:"Policies",type:"string",description:"Authorization policies guarding the endpoint."},{name:"request_type",sourceName:"RequestType",type:"string",description:"The request payload type (e.g. StartRideRequest, CreateOrderDto)."},{name:"response_type",sourceName:"ResponseType",type:"string",description:"The response payload type (e.g. RideDto, OrderResponse)."},{name:"operation_type",sourceName:"OperationType",type:"enum (OperationType)",description:"The operation type for GraphQL (Query, Mutation, Subscription) or gRPC (Unary, Streaming)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring the endpoint."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the endpoint declaration."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed source line number of the route or contract mapping."},{name:"operation",sourceName:"Operation",type:"string",description:"Operation or handler action name associated with this endpoint."},{name:"evidence_kind",sourceName:"EvidenceKind",type:"string",description:"Extraction provenance kind (endpoint, grpc, graphql, ws, consumer)."},{name:"graphql_field",sourceName:"GraphqlField",type:"string",description:"Exposed GraphQL root field name when protocol is GraphQL."},{name:"graphql_sdl",sourceName:"IsGraphqlSdl",type:"boolean",description:"Indicates whether the endpoint was extracted from a GraphQL SDL schema."},{name:"status_code",sourceName:"StatusCode",type:"string",description:"Declared HTTP response status codes (e.g. 200, 201, 404)."},{name:"summary",sourceName:"Summary",type:"string",description:"OpenAPI / Swagger or docstring endpoint summary."}],outEdges:[{rel:"CALLS",toKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"EXPOSED_BY",toKind:"Function",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"EXPOSED_BY",toKind:"Type",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"TRIGGERS",toKind:"Function",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"ATTRIBUTED_TO",toKind:"Database",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"ATTRIBUTED_TO",toKind:"ExternalService",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"ATTRIBUTED_TO",toKind:"Query",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"PASSES_LITERAL",toKind:"CallLiteral",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"SUBSCRIBES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",toKind:"Project",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARES",toKind:"CatchClause",category:"structural",description:"Indicates that a container entity declares a sub-entity."}],inEdges:[{rel:"CALLS_ENDPOINT",fromKind:"ExternalService",category:"control_flow",description:"Links an external API call to the target HTTP endpoint it invokes."},{rel:"EXPOSES",fromKind:"Service",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."}],sampleCypher:`MATCH (n:Endpoint)-[:CALLS]->(t:Function)
RETURN n.name, n.http_method, n.route_template, t.name
LIMIT 25`},{kind:"Topic",pluralLabel:"Message Topics & Queues",layerId:4,layerKey:"L4_semantic",icon:"mail",order:11,idScheme:"{workspaceId}:top:{brokerType}:{topicName}",purpose:"Represents a message queue, event exchange, or topic boundary.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the topic or exchange."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"broker_type",sourceName:"BrokerType",type:"enum (BrokerType)",description:"The broker system type (rabbitmq, kafka, sqs, in-memory)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring or referencing the topic."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed source line of the publish or subscribe site."},{name:"messaging_direction",sourceName:"MessagingDirection",type:"string",description:"Direction of broker interaction (publish, subscribe, both)."},{name:"scope",sourceName:"Scope",type:"string",description:"Broker scope or virtual host / namespace."},{name:"message_type",sourceName:"MessageType",type:"string",description:"Payload or event contract type published/consumed on this topic."},{name:"resource_type",sourceName:"ResourceType",type:"string",description:"IaC resource type when provisioned by Terraform or cloud templates."},{name:"provisioned_by",sourceName:"ProvisionedBy",type:"string",description:"IaC tool or engine provisioning this topic (e.g. terraform)."}],outEdges:[{rel:"PUBLISHED_BY",toKind:"Function",category:"data_flow",description:"Links a topic to the function that publishes to it."},{rel:"SUBSCRIBED_BY",toKind:"Function",category:"data_flow",description:"Links a topic to the function that subscribes to it."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"PUBLISHES_TO",fromKind:"Workload",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to."},{rel:"SUBSCRIBES_TO",fromKind:"Endpoint",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{rel:"SUBSCRIBES_TO",fromKind:"Worker",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Topic)-[:PUBLISHED_BY]->(t:Function)
RETURN n.name, n.broker_type, n.file_path, t.name
LIMIT 25`},{kind:"ExternalService",pluralLabel:"External Services & Resources",layerId:4,layerKey:"L4_semantic",icon:"globe",order:12,idScheme:"{workspaceId}:es:{protocol}:{host}",purpose:"Represents an external API, managed cloud resource, or remote service dependency outside the workspace (REST, gRPC, GraphQL, S3, Stripe, Auth0).",properties:[{name:"name",sourceName:"Name",type:"string",description:"Logical host or external client dependency name."},{name:"protocol",sourceName:"Protocol",type:"string",description:"Outbound transport protocol (HTTP, HTTPS, gRPC, GraphQL, WS, WCF)."},{name:"domain_or_service",sourceName:"DomainOrService",type:"string",description:"Target domain hostname or logical service identifier."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file issuing the outbound call."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed line number of the outbound client call."},{name:"base_url",sourceName:"BaseUrl",type:"string",description:"Configured base URL or service address."},{name:"url",sourceName:"Url",type:"string",description:"Resolved outbound request URL or path template."},{name:"config_key",sourceName:"ConfigKey",type:"string",description:"Configuration setting or environment variable supplying the host URL."},{name:"graphql_field",sourceName:"GraphqlField",type:"string",description:"Queried GraphQL field name on the remote service."},{name:"operation_type",sourceName:"OperationType",type:"string",description:"Remote operation kind (Query, Mutation, Unary, Streaming)."},{name:"method",sourceName:"Method",type:"string",description:"Outbound HTTP verb or RPC method name."},{name:"provider",sourceName:"Provider",type:"string",description:"Cloud platform or SaaS vendor hosting this external service (e.g. AWS, GCP, Azure, Stripe, Auth0)."},{name:"resource_role",sourceName:"ResourceRole",type:"string",description:"Operational role when representing a managed cloud resource (storage, secrets, auth, compute, cdn, ai)."},{name:"is_external",sourceName:"IsExternal",type:"boolean",description:"True when the target resolves outside workspace services."}],outEdges:[{rel:"CALLED_BY",toKind:"Function",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"CALLS_ENDPOINT",toKind:"Endpoint",category:"control_flow",description:"Links an external API call to the target HTTP endpoint it invokes."},{rel:"TRANSITIVELY_CALLS",toKind:"Function",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",toKind:"Type",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",toKind:"Project",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"ATTRIBUTED_TO",fromKind:"Endpoint",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."}],sampleCypher:`MATCH (n:ExternalService)-[:CALLED_BY]->(t:Function)
RETURN n.name, n.protocol, n.domain_or_service, t.name
LIMIT 25`},{kind:"Cloud",pluralLabel:"Cloud Platforms & Vendors",layerId:4,layerKey:"L4_semantic",icon:"cloud",order:13,idScheme:"{workspaceId}:cloud:{provider}",purpose:"Represents a cloud platform, managed infrastructure provider, or SaaS vendor hosting external services, databases, brokers, or workloads (e.g. AWS, GCP, Azure, Cloudflare, Stripe, Supabase).",properties:[{name:"name",sourceName:"Name",type:"string",description:"Cloud platform or vendor display name (e.g. AWS, GCP, Azure, Cloudflare, Stripe)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"provider",sourceName:"Provider",type:"string",description:"Normalized cloud or vendor identifier (aws, gcp, azure, cloudflare, stripe, auth0, supabase, firebase)."},{name:"region",sourceName:"Region",type:"string",description:"Configured cloud region when declared (e.g. us-east-1, europe-west1)."},{name:"category",sourceName:"Category",type:"string",description:"Vendor category (hyperscaler, saas, edge, managed_data)."}],outEdges:[{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"HOSTED_IN",fromKind:"CloudResource",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"Database",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"ExternalService",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"Topic",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"Workload",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."}],sampleCypher:`MATCH (n:Cloud)-[:DECLARED_IN]->(t:File)
RETURN n.name, n.provider, n.region, t.name
LIMIT 25`},{kind:"CloudResource",pluralLabel:"Cloud Resources",layerId:4,layerKey:"L4_semantic",icon:"cloud-upload",order:14,idScheme:"{workspaceId}:p:{relativeProjectDir}:cr:{resourceAddress}",purpose:"Represents a provisioned or referenced cloud infrastructure resource (e.g. RDS instance, SQS queue, S3 bucket, VPC, IAM role, KMS key, Kubernetes workload) hosted in a Cloud platform.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The canonical resource address or name (e.g. aws_s3_bucket.assets)."},{name:"resource_type",sourceName:"ResourceType",type:"string",description:"The provider resource type identifier (e.g. aws_s3_bucket, aws_db_instance, google_sql_database_instance)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"provider",sourceName:"Provider",type:"string",description:"Cloud or infrastructure provider (e.g. aws, gcp, azure, kubernetes, digitalocean, cloudflare)."},{name:"resource_role",sourceName:"ResourceRole",type:"string",description:"Operational role of the cloud resource (database, messaging, storage, compute, network, security, secrets, cdn, dns, container, monitoring)."},{name:"mode",sourceName:"Mode",type:"string",description:"Declaration mode of the resource (managed or data)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring the cloud resource."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed starting line number of the resource declaration."},{name:"line_end",sourceName:"LineEnd",type:"integer",description:"1-indexed ending line number of the resource declaration."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the resource declaration."},{name:"end_line",sourceName:"EndLine",type:"integer",description:"1-indexed ending line number of the resource declaration."}],outEdges:[{rel:"DEPENDS_ON",toKind:"CloudResource",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPENDS_ON",toKind:"Member",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPLOYS",toKind:"Database",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"DEPLOYS",toKind:"ExternalService",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"DEPLOYS",toKind:"Topic",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"DEPLOYS",toKind:"Workload",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"CloudResource",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"IMPLEMENTED_BY",fromKind:"Database",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"ExternalService",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"Topic",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"Workload",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."}],sampleCypher:`MATCH (n:CloudResource)-[:DEPENDS_ON]->(t:CloudResource)
RETURN n.name, n.resource_type, n.provider, t.name
LIMIT 25`},{kind:"PeerMesh",pluralLabel:"Peer Meshes",layerId:4,layerKey:"L4_semantic",icon:"share-alt",order:14,idScheme:"{workspaceId}:mesh:peer-network",purpose:"Represents a symmetric peer-to-peer cluster or collective topology.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the peer mesh."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"member_count",sourceName:"MemberCount",type:"integer",description:"The number of configured peer members, when known."},{name:"protocols",sourceName:"Protocols",type:"string",description:"The detected communication protocols."}],outEdges:[],inEdges:[{rel:"COORDINATES_WITH",fromKind:"Service",category:"type_dependency",description:"Links a workload to the symmetric peer mesh it coordinates with."}],sampleCypher:`MATCH (s:Service)-[:COORDINATES_WITH]->(n:PeerMesh)
RETURN s.name, n.name, n.member_count, n.protocols
LIMIT 25`},{kind:"Database",pluralLabel:"Databases & Storage",layerId:4,layerKey:"L4_semantic",icon:"database",order:20,idScheme:"{workspaceId}:db:{dbType}:{dbName}",purpose:"Represents a database server instance or catalog configured by a workload and containing one or more DbSchema namespaces.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The database engine or catalog name."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"db_type",sourceName:"DbType",type:"enum (DatabaseType)",description:"The database system type (sqlserver, postgres, sqlite, mongodb, neo4j)."},{name:"display_name",sourceName:"DisplayName",type:"string",description:"Human-readable database or catalog display name."},{name:"engine",sourceName:"Engine",type:"string",description:"Detected database engine (e.g. postgresql, sqlserver, sqlite, mysql, mongodb, redis)."},{name:"technology",sourceName:"Technology",type:"string",description:"ORM, driver, or client technology accessing the database (e.g. EF Core, Dapper, Prisma, TypeORM)."},{name:"schema",sourceName:"Schema",type:"string",description:"Physical database schema name when declared (e.g. public, dbo, billing)."},{name:"provider",sourceName:"Provider",type:"string",description:"Database driver or provider package identifier."},{name:"role",sourceName:"Role",type:"string",description:"Storage role classification (primary, cache, analytics, read-replica)."},{name:"config_key",sourceName:"ConfigKey",type:"string",description:"Configuration key or connection string name bound to this database."},{name:"from_config",sourceName:"FromConfig",type:"boolean",description:"Indicates whether the database connection was discovered from configuration files."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether this database instance is used exclusively in tests."},{name:"is_orm",sourceName:"IsOrm",type:"boolean",description:"Indicates whether access was inferred via an ORM entity/DbContext mapping."},{name:"resource_type",sourceName:"ResourceType",type:"string",description:"IaC resource type when provisioned by Terraform or cloud templates."},{name:"provisioned_by",sourceName:"ProvisionedBy",type:"string",description:"IaC tool or engine provisioning this database (e.g. terraform)."}],outEdges:[{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"IMPLEMENTED_BY",toKind:"CloudResource",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"CONTAINS",toKind:"DbSchema",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"ATTRIBUTED_TO",fromKind:"Endpoint",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"USES_DB",fromKind:"Workload",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes."},{rel:"DEPLOYS",fromKind:"CloudResource",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys."},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Database)-[:TARGETED_BY]->(t:CallArgument)
RETURN n.name, n.db_type, n.display_name, t.name
LIMIT 25`},{kind:"DbSchema",pluralLabel:"Database Schemas",layerId:4,layerKey:"L4_semantic",icon:"symbol-namespace",order:21,idScheme:"{workspaceId}:db:{dbType}:{dbName}:schema:{schemaName}",purpose:"Represents a logical or physical schema namespace within a database catalog owning tables, views, and stored procedures (e.g. public, dbo, billing, main).",properties:[{name:"name",sourceName:"Name",type:"string",description:"Database schema name (e.g. public, dbo, billing, main)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"engine",sourceName:"Engine",type:"string",description:"Database engine owning this schema (e.g. PostgreSQL, SqlServer, SQLite)."},{name:"database_id",sourceName:"DatabaseId",type:"string",description:"URN of the parent Database catalog containing this schema."},{name:"is_default",sourceName:"IsDefault",type:"boolean",description:"True when synthesized for the engine's default schema (public, dbo, main)."}],outEdges:[{rel:"CONTAINS",toKind:"Procedure",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Table",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"CONTAINS",fromKind:"Database",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:DbSchema)-[:CONTAINS]->(t:Procedure)
RETURN n.name, n.engine, n.database_id, t.name
LIMIT 25`},{kind:"Table",pluralLabel:"Tables & Views",layerId:4,layerKey:"L4_semantic",icon:"table",order:22,idScheme:"{workspaceId}:tbl:{tableName}",purpose:"Represents a physical database table.",properties:[{name:"name",sourceName:"Name",type:"string",description:"Physical database table or view name."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"engine",sourceName:"Engine",type:"string",description:"Database engine hosting this table (e.g. postgresql, sqlserver, sqlite)."},{name:"schema",sourceName:"Schema",type:"string",description:"Database schema qualifying this table (e.g. dbo, public, core)."},{name:"entity",sourceName:"EntityName",type:"string",description:"Mapped ORM entity or domain model type name, if applicable."}],outEdges:[{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",toKind:"Query",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",toKind:"Type",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."}],inEdges:[{rel:"READS_FROM",fromKind:"Query",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from."},{rel:"WRITES_TO",fromKind:"Query",category:"data_flow",description:"Links a query or workload to the table or cloud resource it writes to."},{rel:"DEPENDS_ON",fromKind:"Query",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",fromKind:"DbSchema",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Table)-[:ACCESSED_BY]->(t:Function)
RETURN n.name, n.engine, n.schema, t.name
LIMIT 25`},{kind:"Procedure",pluralLabel:"Stored Procedures",layerId:4,layerKey:"L4_semantic",icon:"database",order:23,idScheme:"{workspaceId}:proc:{procedureName}",purpose:"Represents a stored procedure in a database.",properties:[],outEdges:[{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"CONTAINS",toKind:"Query",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],inEdges:[{rel:"CONTAINS",fromKind:"DbSchema",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Procedure)-[:ACCESSED_BY]->(t:Function)
RETURN n.name, n.path, t.name
LIMIT 25`},{kind:"Query",pluralLabel:"SQL Queries",layerId:4,layerKey:"L4_semantic",icon:"search",order:30,idScheme:"{workspaceId}:q:{queryHash}",purpose:"Represents a SQL query.",properties:[{name:"name",sourceName:"Name",type:"string",description:"Short query identifier or operation summary."},{name:"query_text",sourceName:"QueryText",type:"string",description:"Extracted SQL or query statement text."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the query in source."},{name:"verb",sourceName:"Verb",type:"string",description:"SQL DML/DDL verb (SELECT, INSERT, UPDATE, DELETE, MERGE, EXEC)."},{name:"operation",sourceName:"Operation",type:"string",description:"Normalized data access operation (read, write, schema)."},{name:"evidence_kind",sourceName:"EvidenceKind",type:"string",description:"Extraction provenance kind (query, orm, migration, procedure)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file path declaring the query."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed source line of the query call site."}],outEdges:[{rel:"TRANSITIVELY_CALLS",toKind:"Function",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",toKind:"Type",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"READS_FROM",toKind:"Table",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from."},{rel:"WRITES_TO",toKind:"Table",category:"data_flow",description:"Links a query or workload to the table or cloud resource it writes to."},{rel:"DEPENDS_ON",toKind:"Table",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"ACCESSED_BY",fromKind:"Table",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ATTRIBUTED_TO",fromKind:"Endpoint",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"CONTAINS",fromKind:"Procedure",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Query)-[:TRANSITIVELY_CALLS]->(t:Function)
RETURN n.name, n.query_text, n.start_line, t.name
LIMIT 25`},{kind:"Type",pluralLabel:"Types (Classes, Interfaces)",layerId:3,layerKey:"L3_syntactic",icon:"symbol-class",order:1,idScheme:"{workspaceId}:sym:{filePath}:Type:{name}:{line}",purpose:"Represents a type declaration (Class, Interface, Struct, Record, Enum, or Union type).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"symbol",sourceName:"Symbol",type:"string",description:"A globally unique ID for this symbol scope."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative path of the declaring file."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"The starting line number (1-indexed) of the declaration."},{name:"kind",sourceName:"TypeKind",type:"enum (TypeKind)",description:"The specific type kind (class, interface, struct, record, enum)."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the type is declared within test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."},{name:"loc",sourceName:"Loc",type:"integer",description:"Lines of code span for this type declaration."},{name:"is_exported",sourceName:"IsExported",type:"boolean",description:"Indicates whether the type is publicly exported from its module."},{name:"extends",sourceName:"Extends",type:"string",description:"Declared base type or component inherited by this type."},{name:"implements",sourceName:"Implements",type:"string",description:"Declared interfaces implemented by this type."},{name:"attributes",sourceName:"Attributes",type:"string",description:"Comma-separated annotations, attributes, or decorators applied to this type."}],outEdges:[{rel:"CALLS",toKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"IMPLEMENTS",toKind:"Type",category:"control_flow",description:"Links a class/struct declaration to the interface it implements."},{rel:"PASSES_LITERAL",toKind:"CallLiteral",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"INHERITS_FROM",toKind:"Type",category:"type_dependency",description:"Links a class/interface to its base class or inherited interface."},{rel:"POTENTIAL_TYPE",toKind:"Type",category:"type_dependency",description:"Links a variable or parameter to concrete classes that implement its declared interface type."},{rel:"USES_TYPE",toKind:"Type",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARES",toKind:"Type",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"HAS_MEMBER",toKind:"Member",category:"structural",description:"Links a type declaration to its declared member variables or fields."},{rel:"HAS_METHOD",toKind:"Function",category:"structural",description:"Links a type declaration to its declared methods or functions."}],inEdges:[{rel:"CALLS",fromKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"EXPOSED_BY",fromKind:"Endpoint",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"IMPLEMENTS",fromKind:"Type",category:"control_flow",description:"Links a class/struct declaration to the interface it implements."},{rel:"INSTANTIATES",fromKind:"Function",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"ACCESSED_BY",fromKind:"ExternalService",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Query",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Table",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"INHERITS_FROM",fromKind:"Type",category:"type_dependency",description:"Links a class/interface to its base class or inherited interface."},{rel:"OF_TYPE",fromKind:"Member",category:"type_dependency",description:"Links a member variable or field to its declared type."},{rel:"POTENTIAL_TYPE",fromKind:"Type",category:"type_dependency",description:"Links a variable or parameter to concrete classes that implement its declared interface type."},{rel:"USES_TYPE",fromKind:"Function",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references."},{rel:"USES_TYPE",fromKind:"Type",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references."},{rel:"DECLARES",fromKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",fromKind:"Type",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DEFINES",fromKind:"BoundedContext",category:"structural",description:"Indicates that an entity defines a particular property or configuration."}],sampleCypher:`MATCH (n:Type)-[:CALLS]->(t:Function)
RETURN n.name, n.symbol, n.file_path, t.name
LIMIT 25`},{kind:"Function",pluralLabel:"Functions & Methods",layerId:3,layerKey:"L3_syntactic",icon:"symbol-method",order:2,idScheme:"{workspaceId}:sym:{filePath}:Function:{name}:{line}",purpose:"Represents a parsed method, function, subroutine, or procedure.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"symbol",sourceName:"Symbol",type:"string",description:"A globally unique ID for this symbol scope."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative path of the declaring file."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"The starting line number (1-indexed) of the declaration."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the function is declared within test code."},{name:"test_framework",sourceName:"TestFramework",type:"string",description:"Detected test runner or framework (e.g. xunit, nunit, mstest, jest, pytest, go-test, junit)."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."},{name:"loc",sourceName:"Loc",type:"integer",description:"Lines of code span for this function."},{name:"is_exported",sourceName:"IsExported",type:"boolean",description:"Indicates whether the function is publicly exported from its module."},{name:"is_module_init",sourceName:"IsModuleInit",type:"boolean",description:"Indicates whether this node represents top-level module initialization code."},{name:"is_async",sourceName:"IsAsync",type:"boolean",description:"Indicates whether the function is asynchronous."},{name:"is_unsafe",sourceName:"IsUnsafe",type:"boolean",description:"Indicates whether the function uses unsafe code blocks or modifiers."},{name:"visibility",sourceName:"Visibility",type:"string",description:"Declared visibility or access modifier of the function (e.g. public, private, remote)."},{name:"graphql_operation",sourceName:"GraphqlOperation",type:"string",description:"GraphQL client operation name or flag."},{name:"operation_type",sourceName:"OperationType",type:"string",description:"Operation type for GraphQL or RPC operations."},{name:"graphql_fields",sourceName:"GraphqlFields",type:"string",description:"Comma-separated GraphQL root selection fields."},{name:"attributes",sourceName:"Attributes",type:"string",description:"Comma-separated annotations, attributes, or decorators applied to this function."}],outEdges:[{rel:"CALLS",toKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"CALLS",toKind:"Type",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"IMPLEMENTS",toKind:"EntryPoint",category:"control_flow",description:"Links a class/struct declaration to the interface it implements."},{rel:"INSTANTIATES",toKind:"EntryPoint",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"INSTANTIATES",toKind:"Function",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"INSTANTIATES",toKind:"Type",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"HAS_PARAMETER",toKind:"Parameter",category:"data_flow",description:"Links a function or method to one of its semantically colored parameters."},{rel:"PASSES_ARGUMENT",toKind:"CallArgument",category:"data_flow",description:"Links a calling function or scope to a semantically colored call argument."},{rel:"PASSES_LITERAL",toKind:"CallLiteral",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"USES_TYPE",toKind:"Type",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARES",toKind:"CatchClause",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",toKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",toKind:"Member",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",toKind:"Type",category:"structural",description:"Indicates that a container entity declares a sub-entity."}],inEdges:[{rel:"CALLED_BY",fromKind:"ExternalService",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"CALLED_BY",fromKind:"Service",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"CALLS",fromKind:"Endpoint",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"CALLS",fromKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"CALLS",fromKind:"Type",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"EXPOSED_BY",fromKind:"Endpoint",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"INSTANTIATES",fromKind:"Function",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"TRANSITIVELY_CALLS",fromKind:"ExternalService",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{rel:"TRANSITIVELY_CALLS",fromKind:"Query",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{rel:"TRIGGERS",fromKind:"Endpoint",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"ACCESSED_BY",fromKind:"ExternalService",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Member",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Procedure",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Query",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",fromKind:"Table",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"PUBLISHED_BY",fromKind:"Topic",category:"data_flow",description:"Links a topic to the function that publishes to it."},{rel:"SUBSCRIBED_BY",fromKind:"Topic",category:"data_flow",description:"Links a topic to the function that subscribes to it."},{rel:"DECLARES",fromKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"HAS_METHOD",fromKind:"Type",category:"structural",description:"Links a type declaration to its declared methods or functions."}],sampleCypher:`MATCH (n:Function)-[:CALLS]->(t:Function)
RETURN n.name, n.symbol, n.file_path, t.name
LIMIT 25`},{kind:"Member",pluralLabel:"Members & Fields",layerId:3,layerKey:"L3_syntactic",icon:"symbol-field",order:3,idScheme:"{workspaceId}:sym:{filePath}:Member:{name}:{line}",purpose:"Represents a declared field, property, parameter, or local variable.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"symbol",sourceName:"Symbol",type:"string",description:"A globally unique ID for this symbol scope."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative path of the declaring file."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"The starting line number (1-indexed) of the declaration."},{name:"kind",sourceName:"MemberKind",type:"enum (MemberKind)",description:"The specific member kind (field, property, parameter, variable)."},{name:"data_type",sourceName:"DataType",type:"string",description:"The declared data type of the member."},{name:"parameter_index",sourceName:"ParameterIndex",type:"integer",description:"0-based formal parameter index when the member is a function parameter."},{name:"variable_type",sourceName:"VariableClassification",type:"string",description:"Variable classification tags (e.g. parameter, config, etl, constant, global, property, field)."},{name:"initializer_expression",sourceName:"InitializerExpression",type:"string",description:"Bounded initializer expression text."},{name:"is_constant",sourceName:"IsConstant",type:"boolean",description:"Indicates whether the member is a compile-time or immutable constant."},{name:"attributes",sourceName:"Attributes",type:"string",description:"Comma-separated annotations, attributes, or decorators applied to this member."}],outEdges:[{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"OF_TYPE",toKind:"Type",category:"type_dependency",description:"Links a member variable or field to its declared type."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"CloudResource",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DECLARES",fromKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"HAS_MEMBER",fromKind:"Type",category:"structural",description:"Links a type declaration to its declared member variables or fields."}],sampleCypher:`MATCH (n:Member)-[:ACCESSED_BY]->(t:Function)
RETURN n.name, n.symbol, n.file_path, t.name
LIMIT 25`},{kind:"Parameter",pluralLabel:"Semantic Parameters",layerId:3,layerKey:"L3_syntactic",icon:"symbol-parameter",order:38,idScheme:"{workspaceId}:param:{functionSymbol}:{index}:{name}",purpose:"Represents a semantically colored function or method parameter that flows into an architectural sink.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The parameter identifier name."},{name:"function_symbol",sourceName:"FunctionSymbol",type:"string",description:"The symbol ID of the declaring function or method."},{name:"index",sourceName:"ParamIndex",type:"integer",description:"The 0-indexed position of the parameter in the function signature."},{name:"data_type",sourceName:"DataType",type:"string",description:"The declared parameter type annotation, if present."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative file path of the declaring function."},{name:"line",sourceName:"Line",type:"integer",description:"The 1-indexed declaration line number."},{name:"nature",sourceName:"Nature",type:"enum (ParameterNature)",description:"The semantic nature inferred from intra- or inter-procedural sink usage."},{name:"sink_callee",sourceName:"SinkCallee",type:"string",description:"The primary sink callee that determined the parameter's semantic nature."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the parameter belongs to test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."}],outEdges:[{rel:"FLOWS_TO",toKind:"CallArgument",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"FLOWS_TO",fromKind:"CallArgument",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{rel:"HAS_PARAMETER",fromKind:"Function",category:"data_flow",description:"Links a function or method to one of its semantically colored parameters."}],sampleCypher:`MATCH (n:Parameter)-[:FLOWS_TO]->(t:CallArgument)
RETURN n.name, n.function_symbol, n.index, t.name
LIMIT 25`},{kind:"CallArgument",pluralLabel:"Semantic Arguments",layerId:3,layerKey:"L3_syntactic",icon:"symbol-variable",order:39,idScheme:"{workspaceId}:arg:{filePath}:{line}:{col}:{slot}",purpose:"Represents a semantically colored invocation argument sliced backward to its provenance origin.",properties:[{name:"name",sourceName:"Name",type:"string",description:"Display label or slot representation of the argument."},{name:"callee",sourceName:"Callee",type:"string",description:"The method, function, or constructor being invoked."},{name:"arg_index",sourceName:"ArgIndex",type:"integer",description:"The 0-indexed positional slot of the argument."},{name:"property_key",sourceName:"PropertyKey",type:"string",description:"The object property key when passed via an options object (e.g. sql, text, url, topic)."},{name:"raw_expression",sourceName:"RawExpression",type:"string",description:"The syntactic expression passed at the call site."},{name:"resolved_value",sourceName:"ResolvedValue",type:"string",description:"The concrete value resolved via backward constant/template slicing, if determinable."},{name:"nature",sourceName:"Nature",type:"enum (ParameterNature)",description:"The semantic nature of the argument slot."},{name:"provenance",sourceName:"Provenance",type:"enum (ArgumentProvenance)",description:"Where the argument's value originates (InternalConstant, CallerParameter, ConfigOrEnv, MemberOrField, etc.)."},{name:"expression_role",sourceName:"Role",type:"enum (ExpressionRole)",description:"How the expression participates in constructing the argument value."},{name:"value_kind",sourceName:"ValueKind",type:"enum (ConstantValueKind)",description:"Fine-grained classification of the resolved constant or template value."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative file path where the call occurs."},{name:"line",sourceName:"Line",type:"integer",description:"The 1-indexed line number of the call argument."},{name:"origin_file",sourceName:"OriginFile",type:"string",description:"The relative file path where the value was originally defined, if distinct."},{name:"origin_line",sourceName:"OriginLine",type:"integer",description:"The 1-indexed line number where the value was originally defined."},{name:"origin_symbol",sourceName:"OriginSymbol",type:"string",description:"The variable, constant, field, parameter, or config key from which the value originated."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the call occurs within test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."}],outEdges:[{rel:"FLOWS_TO",toKind:"Parameter",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"FLOWS_TO",fromKind:"Parameter",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{rel:"PASSES_ARGUMENT",fromKind:"Function",category:"data_flow",description:"Links a calling function or scope to a semantically colored call argument."},{rel:"TARGETED_BY",fromKind:"Database",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"Endpoint",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"ExternalService",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"Procedure",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"Table",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"TARGETED_BY",fromKind:"Topic",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."}],sampleCypher:`MATCH (n:CallArgument)-[:FLOWS_TO]->(t:Parameter)
RETURN n.name, n.callee, n.arg_index, t.name
LIMIT 25`},{kind:"CallLiteral",pluralLabel:"Call Literals",layerId:3,layerKey:"L3_syntactic",icon:"quote",order:40,idScheme:"{workspaceId}:lit:{filePath}:{line}:{col}",purpose:"Represents a raw literal argument passed directly into a method or constructor invocation.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name or representation of the literal argument."},{name:"value",sourceName:"Value",type:"string",description:"The raw string value of the literal."},{name:"callee",sourceName:"Callee",type:"string",description:"The method or callee being invoked."},{name:"arg_index",sourceName:"ArgIndex",type:"integer",description:"The index of the argument (0-indexed)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative file path where the call occurs."},{name:"line",sourceName:"Line",type:"integer",description:"The 1-indexed line number of the argument."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the call occurs within test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."},{name:"role",sourceName:"Role",type:"string",description:"The semantic role of the literal argument (e.g. Business, Routing, ParamBinding, Config, Logging, QueryBuilder, DI)."},{name:"is_decorator",sourceName:"IsDecorator",type:"boolean",description:"Indicates whether the literal argument occurs within a decorator or attribute."},{name:"nature",sourceName:"Nature",type:"enum (ParameterNature)",description:"The semantic nature of the parameter/argument (e.g. SqlQuery, UrlOrRoute, ConnectionString, FilePath)."}],outEdges:[{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"PASSES_LITERAL",fromKind:"Endpoint",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"PASSES_LITERAL",fromKind:"Function",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"PASSES_LITERAL",fromKind:"Type",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."}],sampleCypher:`MATCH (n:CallLiteral)-[:DECLARED_IN]->(t:File)
RETURN n.name, n.value, n.callee, t.name
LIMIT 25`},{kind:"CatchClause",pluralLabel:"Catch Clauses",layerId:3,layerKey:"L3_syntactic",icon:"shield-alert",order:41,idScheme:"{workspaceId}:catch:{filePath}:{line}:{col}",purpose:"Represents a catch or except block in structured exception handling.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The identifier or descriptor of the catch clause."},{name:"exception_type",sourceName:"ExceptionType",type:"string",description:"The declared exception type caught by this block, if specified."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative file path where the catch clause occurs."},{name:"line",sourceName:"Line",type:"integer",description:"The 1-indexed line number where the catch clause starts."},{name:"is_empty",sourceName:"IsEmpty",type:"boolean",description:"Indicates whether the catch body contains no executable statements."},{name:"has_comment",sourceName:"HasComment",type:"boolean",description:"Indicates whether an empty catch block contains an explanatory comment."},{name:"statement_count",sourceName:"StatementCount",type:"integer",description:"The number of executable statements inside the catch body."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the catch clause occurs within test code."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."}],outEdges:[{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"DECLARES",fromKind:"Endpoint",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"DECLARES",fromKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."}],sampleCypher:`MATCH (n:CatchClause)-[:DECLARED_IN]->(t:File)
RETURN n.name, n.exception_type, n.file_path, t.name
LIMIT 25`},{kind:"Project",pluralLabel:"Projects",layerId:2,layerKey:"L2_boundaries",icon:"project",order:1,idScheme:"{workspaceId}:p:{relativeProjectDir}:",purpose:"Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The language/signature identifier (e.g. 'csharp', 'go', 'python', 'typescript')."},{name:"language",sourceName:"Language",type:"string",description:"The programming language (e.g. 'csharp', 'go', 'python', 'typescript')."},{name:"kind",sourceName:"KindValue",type:"string",description:"The physical entity classification kind (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test)."},{name:"role",sourceName:"Role",type:"string",description:"Architectural role of the project (e.g. Service, SharedLibrary, FrontendApp, Worker, CliTool, Test)."},{name:"archetype",sourceName:"Archetype",type:"enum (ProjectArchetype)",description:"Architectural archetype of the project (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon)."},{name:"primary_role",sourceName:"PrimaryRole",type:"string",description:"Primary architectural role of the project (e.g. WebService, Scheduler, WorkerService, SharedLibrary, FrontendFramework, CliFramework, TestFramework)."},{name:"framework",sourceName:"Framework",type:"string",description:"Detected primary framework of the project."},{name:"entity_type",sourceName:"EntityType",type:"string",description:"Normalized semantic entity type (app, service, worker, cli, test, library)."},{name:"external_apis",sourceName:"ExternalApis",type:"string",description:"Comma-separated external APIs or SDKs used by this project."},{name:"package_name",sourceName:"PackageName",type:"string",description:"Package name declared in the project manifest."},{name:"package_version",sourceName:"PackageVersion",type:"string",description:"Package version declared in the project manifest."},{name:"main_file",sourceName:"MainFile",type:"string",description:"Main entry file declared in the project manifest."},{name:"module_file",sourceName:"ModuleFile",type:"string",description:"ESM module entry file declared in the project manifest."},{name:"description",sourceName:"Description",type:"string",description:"Project description from manifest."},{name:"is_library",sourceName:"IsLibrary",type:"boolean",description:"Indicates whether the project is a shared library rather than an executable application."},{name:"entity_kind",sourceName:"EntityKind",type:"enum (ProjectEntityKind)",description:"The physical entity classification (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test)."},{name:"sub_kind",sourceName:"SubKind",type:"enum (ProjectEntitySubKind)",description:"Sub-classification for apps (e.g. None, Web, Mobile, Desktop, Cli)."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether this project is a test project."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The physical entity classification kind."}],outEdges:[{rel:"HAS_ENTRYPOINT",toKind:"EntryPoint",category:"control_flow",description:"Links an executable ProjectNode to its binary or process EntryPointNode."},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPENDS_ON",toKind:"Project",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"PRODUCES",toKind:"Package",category:"type_dependency",description:"Indicates that a library project builds and produces a package."},{rel:"USES_GIT",toKind:"GitSettings",category:"type_dependency",description:"Links the workspace to its repository configuration."},{rel:"CONTAINS",toKind:"EntryPoint",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"File",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Project",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"LOCATED_IN",toKind:"Folder",category:"structural",description:"Links a project to the physical folder or files structure where it is located."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"Project",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"IMPLEMENTED_BY",fromKind:"Module",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"IMPLEMENTED_BY",fromKind:"Package",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"PRODUCES",fromKind:"Package",category:"type_dependency",description:"Indicates that a library project builds and produces a package."},{rel:"BELONGS_TO",fromKind:"EntryPoint",category:"structural",description:"Links an entity to its parent project or container."},{rel:"CONTAINS",fromKind:"Project",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",fromKind:"Endpoint",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"ExternalService",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],sampleCypher:`MATCH (n:Project)-[:HAS_ENTRYPOINT]->(t:EntryPoint)
RETURN n.name, n.project_type, n.language, t.name
LIMIT 25`},{kind:"EntryPoint",pluralLabel:"Entry Points",layerId:2,layerKey:"L2_boundaries",icon:"zap",order:2,idScheme:"{workspaceId}:entry:{entryType}:{name}",purpose:"Represents the binary, process, or bundle bootstrap entry point of an executable project (e.g. Program.Main, func main, __main__, index.tsx root mount).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the binary or process entry point."},{name:"path",sourceName:"Path",type:"string",description:"The path of the entry point file relative to its parent container."},{name:"entry_type",sourceName:"EntryType",type:"enum (EntryPointType)",description:"The kind of binary/process entry point (e.g. main-method, top-level, script-main, bundle-entry)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Relative path of the source file declaring the entry point."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-based starting line number of the entry point."},{name:"command",sourceName:"Command",type:"string",description:"Shell command executed by a lifecycle or task entry point."},{name:"hook_kind",sourceName:"HookKind",type:"string",description:"Lifecycle hook classification (e.g. install, build)."},{name:"task_label",sourceName:"TaskLabel",type:"string",description:"Task label for VS Code folderOpen tasks."}],outEdges:[{rel:"TRIGGERS",toKind:"File",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"BELONGS_TO",toKind:"Project",category:"structural",description:"Links an entity to its parent project or container."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"HAS_ENTRYPOINT",fromKind:"Project",category:"control_flow",description:"Links an executable ProjectNode to its binary or process EntryPointNode."},{rel:"IMPLEMENTS",fromKind:"Function",category:"control_flow",description:"Links a class/struct declaration to the interface it implements."},{rel:"INSTANTIATES",fromKind:"Function",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates."},{rel:"CONTAINS",fromKind:"Project",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:EntryPoint)-[:TRIGGERS]->(t:File)
RETURN n.name, n.entry_type, n.file_path, t.name
LIMIT 25`},{kind:"Package",pluralLabel:"Packages & Dependencies",layerId:2,layerKey:"L2_boundaries",icon:"package",order:2,idScheme:"{workspaceId}:pkg:{ecosystem}:{packageName}",purpose:"Represents an external dependency package or workspace package referenced or produced by projects.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"version",sourceName:"Version",type:"string",description:"The package version."},{name:"is_internal",sourceName:"IsInternal",type:"boolean",description:"Whether this package is produced internally by a workspace project."},{name:"ecosystem",sourceName:"Ecosystem",type:"string",description:"The package ecosystem (e.g. nuget, npm, maven, pip, go)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"archetype",sourceName:"Archetype",type:"enum (ProjectArchetype)",description:"The architectural archetype of the package (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon)."},{name:"dependency_scope",sourceName:"DependencyScope",type:"string",description:"Dependency scope (e.g. runtime, dev, test, build)."},{name:"is_external",sourceName:"IsExternal",type:"boolean",description:"Whether this package is an external third-party dependency."}],outEdges:[{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."},{rel:"PRODUCES",toKind:"Project",category:"type_dependency",description:"Indicates that a library project builds and produces a package."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"Module",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPENDS_ON",fromKind:"Project",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"PRODUCES",fromKind:"Project",category:"type_dependency",description:"Indicates that a library project builds and produces a package."}],sampleCypher:`MATCH (n:Package)-[:IMPLEMENTED_BY]->(t:Project)
RETURN n.name, n.version, n.is_internal, t.name
LIMIT 25`},{kind:"File",pluralLabel:"Files",layerId:1,layerKey:"L1_physical",icon:"file-code",order:1,idScheme:"{workspaceId}:f:{relativeFilePath}",purpose:"Represents a source code file containing parsable content.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the file is part of a test suite or test project."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."},{name:"line_count",sourceName:"LineCount",type:"integer",description:"Total lines of code in this file."},{name:"size_bytes",sourceName:"SizeBytes",type:"integer",description:"Size of the file in bytes."},{name:"artifact_role",sourceName:"ArtifactRole",type:"string",description:"The physical artifact role of the file (source, dist, minified, declaration, script)."}],outEdges:[],inEdges:[{rel:"TRIGGERS",fromKind:"EntryPoint",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"CONTAINS",fromKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",fromKind:"Project",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",fromKind:"CallArgument",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"CallLiteral",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"CatchClause",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Cloud",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"CloudResource",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Database",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"DbSchema",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Endpoint",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"EntryPoint",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"ExternalService",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Function",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Member",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Parameter",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Query",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Topic",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Type",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],sampleCypher:`MATCH (s:EntryPoint)-[:TRIGGERS]->(n:File)
RETURN s.name, n.name, n.is_test, n.project_kind
LIMIT 25`},{kind:"Folder",pluralLabel:"Folders",layerId:1,layerKey:"L1_physical",icon:"folder",order:2,idScheme:"{workspaceId}:dir:{relativeDirectoryPath}",purpose:"Represents a directory within the indexed workspace.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the folder."},{name:"path",sourceName:"Path",type:"string",description:"The path to the folder."}],outEdges:[{rel:"CONTAINS",toKind:"File",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"GitSettings",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],inEdges:[{rel:"CONTAINS",fromKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"LOCATED_IN",fromKind:"Project",category:"structural",description:"Links a project to the physical folder or files structure where it is located."}],sampleCypher:`MATCH (n:Folder)-[:CONTAINS]->(t:File)
RETURN n.name, n.path, t.name
LIMIT 25`},{kind:"GitSettings",pluralLabel:"Git Settings",layerId:1,layerKey:"L1_physical",icon:"git-commit",order:3,idScheme:"{workspaceId}:git",purpose:"Represents the Git repository configuration settings for the workspace.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"branch",sourceName:"Branch",type:"string",description:"The currently checked-out branch name."},{name:"origin_url",sourceName:"OriginUrl",type:"string",description:"The remote origin repository URL."},{name:"user_name",sourceName:"UserName",type:"string",description:"The git user name."},{name:"user_email",sourceName:"UserEmail",type:"string",description:"The git user email address."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"commit_hash",sourceName:"CommitHash",type:"string",description:"Current Git HEAD commit hash."},{name:"head_commit",sourceName:"HeadCommit",type:"string",description:"Current Git HEAD commit hash alias."},{name:"repo_name",sourceName:"RepoName",type:"string",description:"Git repository name."}],outEdges:[],inEdges:[{rel:"USES_GIT",fromKind:"Project",category:"type_dependency",description:"Links the workspace to its repository configuration."},{rel:"CONTAINS",fromKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (s:Project)-[:USES_GIT]->(n:GitSettings)
RETURN s.name, n.name, n.branch, n.origin_url
LIMIT 25`}],relationships:[{rel:"CALLED_BY",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"ExternalService",toKind:"Function"},{fromKind:"Service",toKind:"Function"}]},{rel:"CALLS",category:"control_flow",description:"Links a calling function to the function it directly invokes.",properties:[{name:"line",type:"integer",description:"1-indexed source line of the primary invocation site."},{name:"call_count",type:"integer",description:"Aggregated number of call sites between caller and callee."},{name:"in_loop",type:"boolean",description:"True when invoked inside a for/foreach/while/do loop (N+1 risk signal)."},{name:"loop_depth",type:"integer",description:"Nesting depth of enclosing loops at the call site."},{name:"is_conditional",type:"boolean",description:"True when guarded by an if/switch/ternary branch."},{name:"in_try",type:"boolean",description:"True when enclosed inside a try/catch block."},{name:"arg_flow",type:"string",description:"Parameter-to-argument dataflow slot mapping across the call boundary."}],pairs:[{fromKind:"Endpoint",toKind:"Function"},{fromKind:"Function",toKind:"Function"},{fromKind:"Function",toKind:"Type"},{fromKind:"Type",toKind:"Function"}]},{rel:"CALLS_ENDPOINT",category:"control_flow",description:"Links an external API call to the target HTTP endpoint it invokes.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"ExternalService",toKind:"Endpoint"}]},{rel:"EXPOSED_BY",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"Function"},{fromKind:"Endpoint",toKind:"Type"}]},{rel:"EXPOSES",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Service",toKind:"Endpoint"}]},{rel:"HAS_ENTRYPOINT",category:"control_flow",description:"Links an executable ProjectNode to its binary or process EntryPointNode.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Project",toKind:"EntryPoint"}]},{rel:"IMPLEMENTS",category:"control_flow",description:"Links a class/struct declaration to the interface it implements.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"EntryPoint"},{fromKind:"Type",toKind:"Type"}]},{rel:"INSTANTIATES",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"EntryPoint"},{fromKind:"Function",toKind:"Function"},{fromKind:"Function",toKind:"Type"}]},{rel:"SERVICE_CALL",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Workload",toKind:"ExternalService"},{fromKind:"Workload",toKind:"Service"}]},{rel:"TRANSITIVELY_CALLS",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph.",properties:[{name:"hops",type:"integer",description:"Shortest call-chain hop distance from the calling function to the sink."}],pairs:[{fromKind:"ExternalService",toKind:"Function"},{fromKind:"Query",toKind:"Function"}]},{rel:"TRIGGERS",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"Function"},{fromKind:"EntryPoint",toKind:"File"}]},{rel:"ACCESSED_BY",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"ExternalService",toKind:"Function"},{fromKind:"ExternalService",toKind:"Type"},{fromKind:"Member",toKind:"Function"},{fromKind:"Procedure",toKind:"Function"},{fromKind:"Query",toKind:"Function"},{fromKind:"Query",toKind:"Type"},{fromKind:"Table",toKind:"Function"},{fromKind:"Table",toKind:"Query"},{fromKind:"Table",toKind:"Type"}]},{rel:"ATTRIBUTED_TO",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain.",properties:[{name:"hops",type:"integer",description:"Call-chain hop distance from the entrypoint or endpoint to the sink."},{name:"sink_kind",type:"string",description:"Target sink classification (Database, Query, ExternalService, Topic)."}],pairs:[{fromKind:"Endpoint",toKind:"Database"},{fromKind:"Endpoint",toKind:"ExternalService"},{fromKind:"Endpoint",toKind:"Query"}]},{rel:"FLOWS_TO",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the value transfer occurs."},{name:"arg_index",type:"integer",description:"0-indexed target parameter or argument slot."}],pairs:[{fromKind:"CallArgument",toKind:"Parameter"},{fromKind:"Parameter",toKind:"CallArgument"}]},{rel:"HAS_PARAMETER",category:"data_flow",description:"Links a function or method to one of its semantically colored parameters.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Parameter"}]},{rel:"PASSES_ARGUMENT",category:"data_flow",description:"Links a calling function or scope to a semantically colored call argument.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the value transfer occurs."},{name:"arg_index",type:"integer",description:"0-indexed target parameter or argument slot."}],pairs:[{fromKind:"Function",toKind:"CallArgument"}]},{rel:"PASSES_LITERAL",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the literal argument is passed."}],pairs:[{fromKind:"Endpoint",toKind:"CallLiteral"},{fromKind:"Function",toKind:"CallLiteral"},{fromKind:"Type",toKind:"CallLiteral"}]},{rel:"PUBLISHED_BY",category:"data_flow",description:"Links a topic to the function that publishes to it.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Topic",toKind:"Function"}]},{rel:"PUBLISHES_TO",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Workload",toKind:"Topic"}]},{rel:"READS_FROM",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Query",toKind:"Table"}]},{rel:"SUBSCRIBED_BY",category:"data_flow",description:"Links a topic to the function that subscribes to it.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Topic",toKind:"Function"}]},{rel:"SUBSCRIBES_TO",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Endpoint",toKind:"Topic"},{fromKind:"Worker",toKind:"Topic"}]},{rel:"TARGETED_BY",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the value transfer occurs."},{name:"arg_index",type:"integer",description:"0-indexed target parameter or argument slot."}],pairs:[{fromKind:"Database",toKind:"CallArgument"},{fromKind:"Endpoint",toKind:"CallArgument"},{fromKind:"ExternalService",toKind:"CallArgument"},{fromKind:"Procedure",toKind:"CallArgument"},{fromKind:"Table",toKind:"CallArgument"},{fromKind:"Topic",toKind:"CallArgument"}]},{rel:"USES_DB",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Workload",toKind:"Database"}]},{rel:"WRITES_TO",category:"data_flow",description:"Links a query or workload to the table or cloud resource it writes to.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Query",toKind:"Table"}]},{rel:"COORDINATES_WITH",category:"type_dependency",description:"Links a workload to the symmetric peer mesh it coordinates with.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Service",toKind:"PeerMesh"}]},{rel:"DEPENDS_ON",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"BoundedContext"},{fromKind:"CloudResource",toKind:"CloudResource"},{fromKind:"CloudResource",toKind:"Member"},{fromKind:"Domain",toKind:"Domain"},{fromKind:"Module",toKind:"Library"},{fromKind:"Module",toKind:"Package"},{fromKind:"Project",toKind:"Package"},{fromKind:"Project",toKind:"Project"},{fromKind:"Query",toKind:"Table"}]},{rel:"DEPLOYS",category:"type_dependency",description:"Links a cloud infrastructure resource or deployment configuration to the semantic entity it provisions or deploys.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"CloudResource",toKind:"Database"},{fromKind:"CloudResource",toKind:"ExternalService"},{fromKind:"CloudResource",toKind:"Topic"},{fromKind:"CloudResource",toKind:"Workload"}]},{rel:"HOSTED_IN",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source or config line where the cloud hosting binding is declared."}],pairs:[{fromKind:"CloudResource",toKind:"Cloud"},{fromKind:"Database",toKind:"Cloud"},{fromKind:"ExternalService",toKind:"Cloud"},{fromKind:"Topic",toKind:"Cloud"},{fromKind:"Workload",toKind:"Cloud"}]},{rel:"IMPLEMENTED_BY",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Database",toKind:"CloudResource"},{fromKind:"ExternalService",toKind:"CloudResource"},{fromKind:"Module",toKind:"Project"},{fromKind:"Package",toKind:"Project"},{fromKind:"Topic",toKind:"CloudResource"},{fromKind:"Workload",toKind:"CloudResource"}]},{rel:"INHERITS_FROM",category:"type_dependency",description:"Links a class/interface to its base class or inherited interface.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Type"}]},{rel:"OF_TYPE",category:"type_dependency",description:"Links a member variable or field to its declared type.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Member",toKind:"Type"}]},{rel:"POTENTIAL_TYPE",category:"type_dependency",description:"Links a variable or parameter to concrete classes that implement its declared interface type.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Type"}]},{rel:"PRODUCES",category:"type_dependency",description:"Indicates that a library project builds and produces a package.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Package",toKind:"Project"},{fromKind:"Project",toKind:"Package"}]},{rel:"TESTS",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"TestSuite",toKind:"Module"}]},{rel:"USES_GIT",category:"type_dependency",description:"Links the workspace to its repository configuration.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Project",toKind:"GitSettings"}]},{rel:"USES_TYPE",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Type"},{fromKind:"Type",toKind:"Type"}]},{rel:"BELONGS_TO",category:"structural",description:"Links an entity to its parent project or container.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"EntryPoint",toKind:"Project"}]},{rel:"CONTAINS",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"Database"},{fromKind:"BoundedContext",toKind:"Module"},{fromKind:"BoundedContext",toKind:"Topic"},{fromKind:"Database",toKind:"DbSchema"},{fromKind:"DbSchema",toKind:"Procedure"},{fromKind:"DbSchema",toKind:"Table"},{fromKind:"Domain",toKind:"BoundedContext"},{fromKind:"Folder",toKind:"File"},{fromKind:"Folder",toKind:"Folder"},{fromKind:"Folder",toKind:"GitSettings"},{fromKind:"Procedure",toKind:"Query"},{fromKind:"Project",toKind:"EntryPoint"},{fromKind:"Project",toKind:"File"},{fromKind:"Project",toKind:"Project"}]},{rel:"DECLARED_IN",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"CallArgument",toKind:"File"},{fromKind:"CallLiteral",toKind:"File"},{fromKind:"CatchClause",toKind:"File"},{fromKind:"Cloud",toKind:"File"},{fromKind:"CloudResource",toKind:"File"},{fromKind:"Database",toKind:"File"},{fromKind:"DbSchema",toKind:"File"},{fromKind:"Endpoint",toKind:"File"},{fromKind:"Endpoint",toKind:"Project"},{fromKind:"EntryPoint",toKind:"File"},{fromKind:"ExternalService",toKind:"File"},{fromKind:"ExternalService",toKind:"Project"},{fromKind:"Function",toKind:"File"},{fromKind:"Member",toKind:"File"},{fromKind:"Parameter",toKind:"File"},{fromKind:"Query",toKind:"File"},{fromKind:"Topic",toKind:"File"},{fromKind:"Type",toKind:"File"}]},{rel:"DECLARES",category:"structural",description:"Indicates that a container entity declares a sub-entity.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"CatchClause"},{fromKind:"Function",toKind:"CatchClause"},{fromKind:"Function",toKind:"Function"},{fromKind:"Function",toKind:"Member"},{fromKind:"Function",toKind:"Type"},{fromKind:"Type",toKind:"Type"}]},{rel:"DEFINES",category:"structural",description:"Indicates that an entity defines a particular property or configuration.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"Type"}]},{rel:"HAS_MEMBER",category:"structural",description:"Links a type declaration to its declared member variables or fields.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Member"}]},{rel:"HAS_METHOD",category:"structural",description:"Links a type declaration to its declared methods or functions.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Function"}]},{rel:"LOCATED_IN",category:"structural",description:"Links a project to the physical folder or files structure where it is located.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Project",toKind:"Folder"}]}]},D="graph",g="Endpoint",V=null,G=!1,re=null,be=null,ye="",Z={control_flow:!0,data_flow:!0,type_dependency:!0,structural:!0},de={5:!0,4:!0,3:!0,2:!0,1:!0},we={},b=0;b<w.nodes.length;b++)w.nodes[b].subtypes&&w.nodes[b].subtypes.length>0&&(we[w.nodes[b].kind]=!0);var M="";function j(o){return getComputedStyle(document.documentElement).getPropertyValue(o).trim()}for(var _={canvasBg:j("--ont-canvas-bg"),gridDot:j("--ont-grid-dot"),logical:j("--ont-c-logical"),module:j("--ont-c-module"),workload:j("--ont-c-workload"),contract:j("--ont-c-contract"),data:j("--ont-c-data"),cloud:j("--ont-c-cloud"),syntax:j("--ont-c-syntax"),slicing:j("--ont-c-slicing"),project:j("--ont-c-project"),physical:j("--ont-c-physical"),capOuterFill:j("--ont-capsule-outer-fill"),capOuterBorder:j("--ont-capsule-outer-border"),capInnerFill:j("--ont-capsule-inner-fill"),capInnerBorder:j("--ont-capsule-inner-border"),capHoverBorder:j("--ont-capsule-hover-border"),capHeadBg:j("--ont-capsule-head-bg"),capHeadHoverBg:j("--ont-capsule-head-hover-bg"),abstractRing:j("--ont-abstract-ring"),badgeBg:j("--ont-badge-bg"),badgeBorder:j("--ont-badge-border"),pillBg:j("--ont-pill-bg"),pillBorder:j("--ont-pill-border"),pillBorderActive:j("--ont-pill-border-active"),edgeIdle:j("--ont-edge-idle"),edgeDim:j("--ont-edge-dim"),edgeControl:j("--ont-edge-control"),edgeData:j("--ont-edge-data"),edgeType:j("--ont-edge-type"),edgeStruct:j("--ont-edge-struct"),nodeRing:j("--ont-node-ring"),nodeHalo:j("--ont-node-halo"),nodeHover:j("--ont-node-hover"),inkBright:j("--ont-ink-bright"),inkSoft:j("--ont-ink-soft"),inkDim:j("--ont-ink-dim"),inkZero:j("--ont-ink-zero")},Ie={Domain:{fill:_.logical,glyph:"globe",cluster:8},BoundedContext:{fill:_.logical,glyph:"layers",cluster:8},Module:{fill:_.module,glyph:"package",cluster:0},Workload:{fill:_.workload,glyph:"layers",cluster:0},App:{fill:_.workload,glyph:"browser",cluster:0},Service:{fill:_.workload,glyph:"server",cluster:0},Worker:{fill:_.workload,glyph:"gear",cluster:0},CliTool:{fill:_.workload,glyph:"terminal",cluster:0},Library:{fill:_.module,glyph:"library",cluster:0},TestSuite:{fill:_.module,glyph:"beaker",cluster:0},Endpoint:{fill:_.contract,glyph:"endpoint",cluster:1},Topic:{fill:_.contract,glyph:"topic",cluster:1},PeerMesh:{fill:_.contract,glyph:"mesh",cluster:1},Database:{fill:_.data,glyph:"database",cluster:2},DbSchema:{fill:_.data,glyph:"schema",cluster:2},Table:{fill:_.data,glyph:"table",cluster:2},Query:{fill:_.data,glyph:"query",cluster:2},Procedure:{fill:_.data,glyph:"procedure",cluster:2},ExternalService:{fill:_.cloud,glyph:"cloud",cluster:3},Cloud:{fill:_.cloud,glyph:"cloudUp",cluster:3},CloudResource:{fill:_.cloud,glyph:"server",cluster:3},Type:{fill:_.syntax,glyph:"type",cluster:4},Function:{fill:_.syntax,glyph:"function",cluster:4},Member:{fill:_.syntax,glyph:"member",cluster:4},CatchClause:{fill:_.syntax,glyph:"catch",cluster:4},Parameter:{fill:_.slicing,glyph:"param",cluster:5},CallArgument:{fill:_.slicing,glyph:"arg",cluster:5},CallLiteral:{fill:_.slicing,glyph:"literal",cluster:5},Project:{fill:_.project,glyph:"project",cluster:6},EntryPoint:{fill:_.project,glyph:"zap",cluster:6},Package:{fill:_.project,glyph:"package",cluster:6},File:{fill:_.physical,glyph:"file",cluster:7},Folder:{fill:_.physical,glyph:"folder",cluster:7},GitSettings:{fill:_.physical,glyph:"git",cluster:7}},Ce=[{x:255,y:235,rx:145,ry:105},{x:635,y:255,rx:145,ry:85},{x:1125,y:285,rx:135,ry:110},{x:925,y:155,rx:105,ry:60},{x:705,y:495,rx:165,ry:115},{x:1065,y:575,rx:125,ry:95},{x:305,y:605,rx:130,ry:90},{x:645,y:695,rx:155,ry:55},{x:595,y:115,rx:85,ry:24}],it={App:{x:115,y:145},CliTool:{x:115,y:265},Workload:{x:265,y:215},Service:{x:405,y:150},Worker:{x:405,y:270},Module:{x:295,y:385},Library:{x:135,y:385},TestSuite:{x:155,y:495}},yt={App:1,Worker:1,CliTool:1,Library:1,TestSuite:1},ft={control_flow:_.edgeControl,data_flow:_.edgeData,type_dependency:_.edgeType,structural:_.edgeStruct},A={},ne=0;ne<w.nodes.length;ne++)A[w.nodes[ne].kind]=w.nodes[ne];for(var $={},Ee=0;Ee<w.relationships.length;Ee++)$[w.relationships[Ee].rel]=w.relationships[Ee];function Xe(o,u){var v=A[o];if(!v||!v.subtypes||v.subtypes.length===0)return[];for(var k=[],S=0;S<v.subtypes.length;S++){var c=v.subtypes[S];if(!u[c]){u[c]=!0,k.push(c);for(var L=Xe(c,u),N=0;N<L.length;N++)k.push(L[N])}}return k}for(var Me=[],vt=0;vt<w.nodes.length;vt++){var Lt=w.nodes[vt];Lt.subtypes&&Lt.subtypes.length>0&&Me.push({kind:Lt.kind,depth:Lt.ancestors?Lt.ancestors.length:0,allDescendants:Xe(Lt.kind,{})})}Me.sort(function(o,u){return o.depth-u.depth||(o.kind<u.kind?-1:1)});function ln(o){for(var u={},v=0;v<o.length;v++)u[o[v].kind]=!0;for(var k={},S={},c={},L=0;L<Me.length;L++){var N=Me[L];if(!we[N.kind]&&!(!u[N.kind]||k[N.kind])){for(var H=0,O=0;O<N.allDescendants.length;O++){var Q=N.allDescendants[O];u[Q]&&!k[Q]&&(k[Q]=!0,H++),S[Q]||(S[Q]=N.kind)}H>0&&(c[N.kind]=H)}}for(var F=[],K=0;K<o.length;K++)k[o[K].kind]||F.push(o[K]);return{visibleDtos:F,redirectMap:S,collapsedCounts:c}}function ue(o){return String(o||"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function tn(o,u){if(!u||o.kind.toLowerCase().indexOf(u)!==-1||o.pluralLabel.toLowerCase().indexOf(u)!==-1||o.purpose.toLowerCase().indexOf(u)!==-1||o.idScheme.toLowerCase().indexOf(u)!==-1||o.extends&&o.extends.toLowerCase().indexOf(u)!==-1)return!0;if(o.subtypes){for(var v=0;v<o.subtypes.length;v++)if(o.subtypes[v].toLowerCase().indexOf(u)!==-1)return!0}for(var k=0;k<o.properties.length;k++){var S=o.properties[k];if(S.name.toLowerCase().indexOf(u)!==-1||S.description.toLowerCase().indexOf(u)!==-1)return!0}for(var c=o.inheritedOutEdges?o.outEdges.concat(o.inheritedOutEdges):o.outEdges,L=0;L<c.length;L++){var N=c[L];if(N.rel.toLowerCase().indexOf(u)!==-1||N.toKind.toLowerCase().indexOf(u)!==-1)return!0}for(var H=o.inheritedInEdges?o.inEdges.concat(o.inheritedInEdges):o.inEdges,O=0;O<H.length;O++){var Q=H[O];if(Q.rel.toLowerCase().indexOf(u)!==-1||Q.fromKind.toLowerCase().indexOf(u)!==-1)return!0}return!1}var ut=690,tt=440,qe=.0228,B=.001,se=.6,x=[],m={},C=[],ee=1,me=1,te=0,ke=0,fe=!1,_e=null,He="";try{var Nt=localStorage.getItem("ce_ontology_air");if(Nt!==null){var Hn=parseFloat(Nt);!isNaN(Hn)&&Hn>=.1&&Hn<=2.4&&(ee=Hn)}var Rr=localStorage.getItem("ce_ontology_hub");if(Rr!==null){var Wn=parseFloat(Rr);!isNaN(Wn)&&Wn>=0&&Wn<=5&&(me=Wn)}}catch{}function Pr(o){for(var u={},v=0;v<o.length;v++){var k=o[v],S=Ie[k.kind]||{fill:_.physical,glyph:"file",cluster:7};u[S.cluster]||(u[S.cluster]=[]),u[S.cluster].push(k)}var c={};for(var L in u)for(var N=u[L],H=Ce[L]||Ce[0],O=0;O<N.length;O++){var Q=N[O],F=Ie[Q.kind]||{fill:_.physical,glyph:"file",cluster:7},K=Number(L)===0&&N.length>1?it[Q.kind]:null,ae=N.length===1?0:O/N.length*Math.PI*2-Math.PI/2,Le=K?K.x:H.x+(N.length===1?0:Math.cos(ae)*H.rx),pe=K?K.y:H.y+(N.length===1?0:Math.sin(ae)*H.ry);c[Q.kind]={ax:Le,ay:pe,style:F,anchorStrength:K?.072:.048}}return c}function ys(o,u){var v=[],k={},S={};function c(Oe,Be,We){if(Oe!==Be){var Ye=o[Oe],Ze=o[Be];if(!(!Ye||!Ze)){var he=Oe<Be?Oe:Be,Ke=Oe<Be?Be:Oe,mt=he+"::"+Ke;k[mt]||(k[mt]=!0,v.push({a:Ye,b:Ze,isExt:We}),S[Oe]=(S[Oe]||0)+1,S[Be]=(S[Be]||0)+1)}}}for(var L=0;L<w.nodes.length;L++){var N=w.nodes[L];N.extends&&c(N.kind,N.extends,!0)}for(var H=0;H<w.relationships.length;H++){var O=w.relationships[H];if(Z[O.category])for(var Q=0;Q<O.pairs.length;Q++){var F=O.pairs[Q],K=u&&u[F.fromKind]||F.fromKind,ae=u&&u[F.toKind]||F.toKind;c(K,ae,!1)}}for(var Le in o)o[Le].degree=Math.max(1,S[Le]||1);for(var pe=[],Ne=0;Ne<v.length;Ne++){var ve=v[Ne],Ae=ve.a.degree||1,Pe=ve.b.degree||1,oe=Math.max(.22,Math.min(.78,Ae/(Ae+Pe))),U=Math.max(1,Math.min(Ae,Pe)),X=ve.isExt?.54:.48/Math.sqrt(U),De=Math.max(.16,Math.min(.56,X)),Qe=ve.a.style.cluster===ve.b.style.cluster,ze=ve.isExt?126:Qe?144:212;pe.push({source:ve.a,target:ve.b,distance:ze,strength:De,bias:oe})}return pe}function Xa(o,u,v,k,S){for(var c=typeof S=="number"?S:1,L=Math.pow(540*k*(1+c*.25),2),N=-620*k*v,H=0;H<o.length;H++)for(var O=o[H],Q=O.degree||1,F=1+c*.24*Math.max(0,Q-2),K=H+1;K<o.length;K++){var ae=o[K],Le=ae.degree||1,pe=ae.x-O.x,Ne=ae.y-O.y,ve=pe*pe+Ne*Ne;if(!(ve>L)){ve<1&&(pe=H%2===0?1:-1,Ne=K%2===0?.5:-.5,ve=1.25);var Ae=1+c*.24*Math.max(0,Le-2),Pe=.5*(F+Ae)*(1+c*.08*Math.min(Q,Le)),oe=N*Pe/Math.max(ve,900);O.fx===null&&(O.vx+=pe*oe,O.vy+=Ne*oe),ae.fx===null&&(ae.vx-=pe*oe,ae.vy-=Ne*oe)}}for(var U=0;U<u.length;U++){var X=u[U],De=X.target.x+X.target.vx-X.source.x-X.source.vx,Qe=X.target.y+X.target.vy-X.source.y-X.source.vy,ze=Math.sqrt(De*De+Qe*Qe)||1,Oe=Math.max(0,(X.source.degree||1)+(X.target.degree||1)-4),Be=1+c*.042*Oe,We=1/(1+c*.055*Oe),Ye=X.distance*k*Be,Ze=(ze-Ye)/ze*v*X.strength*We;De*=Ze,Qe*=Ze,X.target.fx===null&&(X.target.vx-=De*X.bias,X.target.vy-=Qe*X.bias),X.source.fx===null&&(X.source.vx+=De*(1-X.bias),X.source.vy+=Qe*(1-X.bias))}for(var he=0;he<o.length;he++){var Ke=o[he];if(Ke.fx===null){var mt=ut+(Ke.ax-ut)*k,Rt=tt+(Ke.ay-tt)*k,an=Ke.anchorStrength/(1+c*.35+c*.06*Math.max(0,(Ke.degree||1)-2));Ke.vx+=(mt-Ke.x)*an*v,Ke.vy+=(Rt-Ke.y)*an*v}}for(var Pt=26*Math.min(k,2.2),ct=0;ct<2;ct++)for(var st=0;st<o.length;st++)for(var je=o[st],xt=Pt*(1+c*.035*Math.max(0,(je.degree||1)-2)),kt=st+1;kt<o.length;kt++){var Ge=o[kt],vn=Pt*(1+c*.035*Math.max(0,(Ge.degree||1)-2)),_t=xt+vn,dt=Ge.x+Ge.vx-(je.x+je.vx),Yt=Ge.y+Ge.vy-(je.y+je.vy),Wt=dt*dt+Yt*Yt;if(!(Wt>=_t*_t)){Wt<.01&&(dt=.5,Yt=.5,Wt=.5);var Mt=Math.sqrt(Wt),bn=(_t-Mt)/Mt*.42,Gt=dt*bn,Ut=Yt*bn;je.fx===null&&Ge.fx===null?(je.vx-=Gt,je.vy-=Ut,Ge.vx+=Gt,Ge.vy+=Ut):je.fx!==null&&Ge.fx===null?(Ge.vx+=Gt*2,Ge.vy+=Ut*2):Ge.fx!==null&&je.fx===null&&(je.vx-=Gt*2,je.vy-=Ut*2)}}for(var Gn=0;Gn<o.length;Gn++){var Ue=o[Gn];if(Ue.fx!==null&&Ue.fy!==null){Ue.x=Ue.fx,Ue.y=Ue.fy,Ue.vx=0,Ue.vy=0;continue}Ue.vx*=se,Ue.vy*=se;var qt=Math.sqrt(Ue.vx*Ue.vx+Ue.vy*Ue.vy);qt>28&&(Ue.vx=Ue.vx/qt*28,Ue.vy=Ue.vy/qt*28),Ue.x+=Ue.vx,Ue.y+=Ue.vy}}function _a(o){Et=[],cn={};for(var u=0;u<x.length;u++){var v=x[u],k=!1;if(v.dto.subtypes){for(var S=0;S<v.dto.subtypes.length;S++)if(m[v.dto.subtypes[S]]){k=!0;break}}var c={kind:v.dto.kind,x:v.x,y:v.y,ax:v.ax,ay:v.ay,radius:25,fill:v.style.fill,glyph:v.style.glyph,isAbstract:!!v.dto.isAbstract,extendsKind:v.dto.extends||null,collapsedCount:o&&o[v.dto.kind]?o[v.dto.kind]:0,hasExpandedSubtypes:k};Et.push(c),cn[c.kind]=c}}var Aa={};function xr(o,u,v){Aa=v||{};for(var k=x.length===0,S=m,c=Pr(o),L=[],N={},H=[],O=0;O<o.length;O++){var Q=o[O];H.push(Q.kind);var F=c[Q.kind],K=S[Q.kind];if(K){K.dto=Q,K.style=F.style,K.ax=F.ax,K.ay=F.ay,K.anchorStrength=F.anchorStrength,L.push(K),N[Q.kind]=K;continue}var ae=ut+(F.ax-ut)*ee,Le=tt+(F.ay-tt)*ee,pe=ae,Ne=Le;if(!k&&Q.extends){var ve=S[Q.extends]||N[Q.extends];ve&&(pe=ve.x+(ae-ve.x)*.32,Ne=ve.y+(Le-ve.y)*.32)}var Ae={dto:Q,style:F.style,x:pe,y:Ne,ax:F.ax,ay:F.ay,vx:0,vy:0,fx:null,fy:null,anchorStrength:F.anchorStrength};L.push(Ae),N[Q.kind]=Ae}x=L,m=N,C=ys(N,u);var Pe=[];for(var oe in Z)Z[oe]&&Pe.push(oe);var U=H.join(",")+"|"+Pe.join(",");if(k){for(var X=1,De=0;De<220;De++)X+=(0-X)*qe,Xa(x,C,X,ee,me);for(var Qe=0;Qe<x.length;Qe++)x[Qe].vx=0,x[Qe].vy=0;te=0}else U!==He&&!fe&&(te=Math.max(te,.42));He=U,_a(Aa)}function Ia(){for(var o=0;o<x.length;o++){var u=x[o];u.x=ut+(u.ax-ut)*ee,u.y=tt+(u.ay-tt)*ee,u.vx=0,u.vy=0,u.fx=null,u.fy=null}for(var v=1,k=0;k<220;k++)v+=(0-v)*qe,Xa(x,C,v,ee,me);for(var S=0;S<x.length;S++)x[S].vx=0,x[S].vy=0;te=0,ke=0,_a(Aa)}function aa(o,u){for(var v={},k=[],S=0;S<w.nodes.length;S++){var c=w.nodes[S];if(c.extends&&!(!o[c.kind]||!o[c.extends])){var L=c.kind+"|EXTENDS|"+c.extends,N={fromKind:c.kind,toKind:c.extends,rel:"EXTENDS",category:"type_dependency",isExtends:!0,priority:1};v[L]=N,k.push(N)}}for(var H=0;H<w.relationships.length;H++){var O=w.relationships[H];if(Z[O.category])for(var Q=0;Q<O.pairs.length;Q++){var F=O.pairs[Q],K=u&&u[F.fromKind]||F.fromKind,ae=u&&u[F.toKind]||F.toKind;if(!(!o[K]||!o[ae])&&!(K===ae&&F.fromKind!==F.toKind)){var Le=1;O.rel==="DECLARED_IN"||O.rel==="BELONGS_TO"?Le=4:yt[K]||yt[ae]?Le=3:(K==="Service"||ae==="Service"||K==="Workload"||ae==="Workload"||O.category==="structural")&&(Le=2);var pe=K+"|"+O.rel+"|"+ae;if(v[pe])Le<v[pe].priority&&(v[pe].priority=Le);else{var Ne={fromKind:K,toKind:ae,rel:O.rel,category:O.category,priority:Le};v[pe]=Ne,k.push(Ne)}}}}for(var ve={},Ae={},Pe=[],oe=0;oe<k.length;oe++){var U=k[oe];if(U.fromKind===U.toKind){var X=ve[U.fromKind]||0;ve[U.fromKind]=X+1,Pe.push({fromKind:U.fromKind,toKind:U.toKind,rel:U.rel,category:U.category,isSelf:!0,slot:X,offset:0,priority:U.priority})}else{var De=U.fromKind<U.toKind?U.fromKind:U.toKind,Qe=U.fromKind<U.toKind?U.toKind:U.fromKind,ze=De+"::"+Qe;Ae[ze]||(Ae[ze]=[]),Ae[ze].push(U)}}for(var Oe in Ae)for(var Be=Ae[Oe],We=0;We<Be.length;We++){var Ye=Be[We],Ze=22;Be.length>1&&(Ze=(We-(Be.length-1)/2)*34,Math.abs(Ze)<6&&(Ze=14)),Pe.push({fromKind:Ye.fromKind,toKind:Ye.toKind,rel:Ye.rel,category:Ye.category,isExtends:!!Ye.isExtends,isSelf:!1,slot:0,offset:Ze,priority:Ye.priority})}return Pe.sort(function(he,Ke){return he.priority-Ke.priority}),Pe}var Et=[],cn={},fn=[],ce={panX:0,panY:0,zoom:.72},Za=!1,nn=!0,La=0,Mr=0,Fe=null;function jt(o,u,v,k,S,c){var L=Math.min(c,k/2,S/2);o.beginPath(),o.moveTo(u+L,v),o.arcTo(u+k,v,u+k,v+S,L),o.arcTo(u+k,v+S,u,v+S,L),o.arcTo(u,v+S,u,v,L),o.arcTo(u,v,u+k,v,L),o.closePath()}function yo(o,u,v,k){o.save(),o.translate(v,k),o.strokeStyle=_.inkBright,o.fillStyle=_.inkBright,o.lineWidth=1.65,o.lineCap="round",o.lineJoin="round",u==="globe"?(o.beginPath(),o.arc(0,0,6.8,0,Math.PI*2),o.stroke(),o.beginPath(),o.ellipse(0,0,3.1,6.8,0,0,Math.PI*2),o.stroke(),o.beginPath(),o.moveTo(-6.6,0),o.lineTo(6.6,0),o.stroke()):u==="database"||u==="procedure"?(o.beginPath(),o.ellipse(0,-5,6.5,2.6,0,0,Math.PI*2),o.stroke(),o.beginPath(),o.moveTo(-6.5,-5),o.lineTo(-6.5,5),o.arc(0,5,6.5,Math.PI,0,!0),o.lineTo(6.5,-5),o.stroke()):u==="schema"?(jt(o,-6.5,-6,13,4.5,1.5),o.stroke(),jt(o,-6.5,1.5,5.2,4.5,1.2),o.stroke(),jt(o,1.3,1.5,5.2,4.5,1.2),o.stroke(),o.beginPath(),o.moveTo(0,-1.5),o.lineTo(0,0),o.moveTo(-3.9,0),o.lineTo(3.9,0),o.moveTo(-3.9,0),o.lineTo(-3.9,1.5),o.moveTo(3.9,0),o.lineTo(3.9,1.5),o.stroke()):u==="layers"?(o.beginPath(),o.moveTo(0,-6.5),o.lineTo(7,-3),o.lineTo(0,.5),o.lineTo(-7,-3),o.closePath(),o.stroke(),o.beginPath(),o.moveTo(-7,.8),o.lineTo(0,4.2),o.lineTo(7,.8),o.moveTo(-7,4.2),o.lineTo(0,7.2),o.lineTo(7,4.2),o.stroke()):u==="table"?(jt(o,-7,-6,14,12,2),o.stroke(),o.beginPath(),o.moveTo(-7,-1.5),o.lineTo(7,-1.5),o.moveTo(-1.5,-1.5),o.lineTo(-1.5,6),o.stroke()):u==="query"?(o.beginPath(),o.arc(-1.5,-1.5,5,0,Math.PI*2),o.moveTo(2.2,2.2),o.lineTo(6.8,6.8),o.stroke()):u==="endpoint"?(o.beginPath(),o.arc(0,0,2.2,0,Math.PI*2),o.fill(),o.beginPath(),o.arc(0,0,5.5,-Math.PI*.7,Math.PI*.7),o.stroke(),o.beginPath(),o.arc(0,0,5.5,Math.PI*.3,Math.PI*1.7),o.stroke()):u==="topic"?(jt(o,-7,-5,14,10,2),o.stroke(),o.beginPath(),o.moveTo(-6.5,-4.2),o.lineTo(0,1),o.lineTo(6.5,-4.2),o.stroke()):u==="cloud"||u==="cloudUp"?(o.beginPath(),o.arc(-2.5,1,3.8,Math.PI*.5,Math.PI*1.5),o.arc(1,-1.5,4.2,Math.PI,Math.PI*1.95),o.arc(4.2,1.5,3.2,Math.PI*1.5,Math.PI*.5),o.closePath(),o.stroke()):u==="function"?(o.beginPath(),o.moveTo(2.5,-6.5),o.quadraticCurveTo(-1,-6.5,-1,-2),o.lineTo(-1,6.5),o.moveTo(-4.5,-1),o.lineTo(3,-1),o.stroke()):u==="type"?(o.beginPath(),o.moveTo(0,-7),o.lineTo(6.5,-3.5),o.lineTo(6.5,3.5),o.lineTo(0,7),o.lineTo(-6.5,3.5),o.lineTo(-6.5,-3.5),o.closePath(),o.stroke()):u==="param"||u==="arg"||u==="literal"?(o.beginPath(),o.moveTo(-4,-5.5),o.lineTo(-7,0),o.lineTo(-4,5.5),o.moveTo(4,-5.5),o.lineTo(7,0),o.lineTo(4,5.5),u!=="param"&&(o.moveTo(-1.5,0),o.lineTo(1.5,0)),o.stroke()):u==="folder"?(o.beginPath(),o.moveTo(-7,-5),o.lineTo(-2,-5),o.lineTo(0,-3),o.lineTo(7,-3),o.lineTo(7,5.5),o.lineTo(-7,5.5),o.closePath(),o.stroke()):u==="file"?(o.beginPath(),o.moveTo(-5,-6.5),o.lineTo(2,-6.5),o.lineTo(5.5,-3),o.lineTo(5.5,6.5),o.lineTo(-5,6.5),o.closePath(),o.stroke()):u==="package"||u==="project"?(jt(o,-6.5,-5.5,13,11,2),o.stroke(),o.beginPath(),o.moveTo(-6.5,-1.5),o.lineTo(6.5,-1.5),o.stroke()):u==="zap"?(o.beginPath(),o.moveTo(1,-7),o.lineTo(-5,.5),o.lineTo(0,.5),o.lineTo(-1,7),o.lineTo(5,-.5),o.lineTo(0,-.5),o.closePath(),o.stroke()):(jt(o,-6.5,-5.5,13,11,2.5),o.stroke(),o.beginPath(),o.moveTo(-3.5,0),o.lineTo(3.5,0),o.stroke()),o.restore()}function Dr(o,u,v,k,S,c){o.save(),o.translate(u,v),o.rotate(k),o.fillStyle=S,o.beginPath(),o.moveTo(0,0),o.lineTo(-c,-c*.54),o.lineTo(-c*.78,0),o.lineTo(-c,c*.54),o.closePath(),o.fill(),o.restore()}function fs(o,u,v,k,S,c){o.save(),o.translate(u,v),o.rotate(k),o.fillStyle=_.canvasBg,o.strokeStyle=S,o.lineWidth=1.5,o.lineJoin="round",o.beginPath(),o.moveTo(0,0),o.lineTo(-c,-c*.58),o.lineTo(-c,c*.58),o.closePath(),o.fill(),o.stroke(),o.restore()}function Ln(){var o=document.getElementById("ont-canvas-frame");if(!(!o||Et.length===0)){var u=o.clientWidth||760,v=o.clientHeight||640,k=document.getElementById("ont-filter-overlay"),S=0;k&&getComputedStyle(k).position==="absolute"&&(S=(k.offsetHeight||30)+14);for(var c=Math.max(160,v-S),L=1e9,N=1e9,H=-1e9,O=-1e9,Q=0;Q<Et.length;Q++){var F=Et[Q],K=F.x,ae=F.y;if(te>.05&&typeof F.ax=="number"){var Le=ut+(F.ax-ut)*ee,pe=tt+(F.ay-tt)*ee;Le-68<L&&(L=Le-68),pe-74<N&&(N=pe-74),Le+68>H&&(H=Le+68),pe+88>O&&(O=pe+88)}K-68<L&&(L=K-68),ae-74<N&&(N=ae-74),K+68>H&&(H=K+68),ae+108>O&&(O=ae+108)}var Ne=Math.max(200,H-L),ve=Math.max(200,O-N),Ae=Math.max(.22,Math.min(1.85,Math.min((u-40)/Ne,(c-36)/ve)));ce.zoom=Ae,ce.panX=u/2-(L+H)/2*Ae,ce.panY=S+c/2-(N+O)/2*Ae}}function er(o,u){for(var v=(o-ce.panX)/ce.zoom,k=(u-ce.panY)/ce.zoom,S=Et.length-1;S>=0;S--){var c=Et[S],L=!!(c.collapsedCount&&c.collapsedCount>0||c.hasExpandedSubtypes);if(L){var N=c.x-c.radius*.78-14,H=c.y-c.radius*.74-9;if(v>=N-4&&v<=N+34&&k>=H-4&&k<=H+22)return c.kind}}return null}function tr(o,u){for(var v=(o-ce.panX)/ce.zoom,k=(u-ce.panY)/ce.zoom,S=Et.length-1;S>=0;S--){var c=Et[S],L=v-c.x,N=k-c.y;if(L*L+N*N<=1024)return c}return null}function bt(){if(D==="graph"){var o=document.getElementById("ont-graph-canvas"),u=document.getElementById("ont-canvas-frame");if(!(!o||!u)){var v=u.clientWidth||760,k=u.clientHeight||640;!Za&&v>80&&Et.length>0&&(Za=!0,Ln());var S=window.devicePixelRatio||1;o.width=Math.max(1,Math.floor(v*S)),o.height=Math.max(1,Math.floor(k*S));var c=o.getContext("2d");if(c){c.setTransform(S,0,0,S,0,0),c.fillStyle=_.canvasBg,c.fillRect(0,0,v,k),c.fillStyle=_.gridDot;for(var L=32,N=(ce.panX%L+L)%L,H=(ce.panY%L+L)%L,O=N;O<v;O+=L)for(var Q=H;Q<k;Q+=L)c.fillRect(O,Q,1.4,1.4);var F=V,K={};if(F){K[F]=!0;for(var ae=cn[F],Le={};ae&&ae.extendsKind&&!Le[ae.extendsKind];)Le[ae.extendsKind]=!0,K[ae.extendsKind]=!0,ae=cn[ae.extendsKind]}var pe={};for(var Ne in K)pe[Ne]=!0;if(F)for(var ve=0;ve<fn.length;ve++)K[fn[ve].fromKind]&&(pe[fn[ve].toKind]=!0),K[fn[ve].toKind]&&(pe[fn[ve].fromKind]=!0);c.save(),c.translate(ce.panX,ce.panY),c.scale(ce.zoom,ce.zoom);for(var Ae=[],Pe=0;Pe<fn.length;Pe++){var oe=fn[Pe],U=cn[oe.fromKind],X=cn[oe.toKind];if(!(!U||!X)){var De=!!(F&&(K[oe.fromKind]||K[oe.toKind])),Qe=!!(F&&!De),ze=Qe?_.edgeDim:oe.isExtends?De?_.capHoverBorder:_.abstractRing:De?ft[oe.category]||_.edgeControl:_.edgeIdle;if(c.strokeStyle=ze,c.lineWidth=De?1.9:oe.isExtends?1.5:1.25,oe.isSelf){var Oe=-Math.PI*.64-oe.slot*.55,Be=Oe-.42,We=Oe+.42,Ye=U.radius,Ze=U.x+Math.cos(Be)*Ye,he=U.y+Math.sin(Be)*Ye,Ke=U.x+Math.cos(We)*(Ye+3),mt=U.y+Math.sin(We)*(Ye+3),Rt=Ye+42+oe.slot*16,an=U.x+Math.cos(Be-.18)*Rt,Pt=U.y+Math.sin(Be-.18)*Rt,ct=U.x+Math.cos(We+.18)*Rt,st=U.y+Math.sin(We+.18)*Rt;c.beginPath(),c.moveTo(Ze,he),c.bezierCurveTo(an,Pt,ct,st,Ke,mt),c.stroke(),Qe||(Dr(c,Ke,mt,Math.atan2(mt-st,Ke-ct),ze,7),Ae.push({mx:.125*Ze+.375*an+.375*ct+.125*Ke,my:.125*he+.375*Pt+.375*st+.125*mt,text:oe.rel,active:De,prio:De?0:oe.priority}));continue}var je=oe.fromKind<oe.toKind,xt=je?U.x:X.x,kt=je?U.y:X.y,Ge=je?X.x:U.x,vn=je?X.y:U.y,_t=Ge-xt,dt=vn-kt,Yt=Math.sqrt(_t*_t+dt*dt)||1,Wt=(U.x+X.x)/2+-dt/Yt*oe.offset,Mt=(U.y+X.y)/2+_t/Yt*oe.offset,bn=Wt-U.x,Gt=Mt-U.y,Ut=Math.sqrt(bn*bn+Gt*Gt)||1,Gn=U.x+bn/Ut*U.radius,Ue=U.y+Gt/Ut*U.radius,qt=X.x-Wt,wn=X.y-Mt,Un=Math.sqrt(qt*qt+wn*wn)||1,qn=X.x-qt/Un*(X.radius+(oe.isExtends?4:3)),sr=X.y-wn/Un*(X.radius+(oe.isExtends?4:3));c.save(),oe.isExtends&&c.setLineDash([5,4]),c.beginPath(),c.moveTo(Gn,Ue),c.quadraticCurveTo(Wt,Mt,qn,sr),c.stroke(),c.restore(),Qe||(oe.isExtends?fs(c,qn,sr,Math.atan2(wn,qt),ze,De?9.5:8.4):Dr(c,qn,sr,Math.atan2(wn,qt),ze,De?7.8:6.6),Ae.push({mx:.25*Gn+.5*Wt+.25*qn,my:.25*Ue+.5*Mt+.25*sr,text:oe.rel,active:De,isExtends:oe.isExtends,prio:De?0:oe.priority}))}}for(var or=[],Oa=0;Oa<Et.length;Oa++){var Se=Et[Oa],la=Se.kind===V,ws=Se.kind===re,kn=!!(F&&!pe[Se.kind]),ir=Se.x*ce.zoom+ce.panX,Ba=Se.y*ce.zoom+ce.panY,Fa=(Se.radius+14)*ce.zoom;if(or.push({minX:ir-Fa,minY:Ba-Fa,maxX:ir+Fa,maxY:Ba+Fa+18*ce.zoom}),c.save(),kn&&(c.globalAlpha=.34),(la||ws)&&(c.beginPath(),c.arc(Se.x,Se.y,Se.radius+6.5,0,Math.PI*2),c.fillStyle=la?_.nodeHalo:_.nodeHover,c.fill()),Se.isAbstract&&(c.save(),c.beginPath(),c.arc(Se.x,Se.y,Se.radius+4,0,Math.PI*2),c.setLineDash([3.5,3]),c.lineWidth=1.3,c.strokeStyle=_.abstractRing,c.stroke(),c.restore()),c.beginPath(),c.arc(Se.x,Se.y,Se.radius,0,Math.PI*2),c.fillStyle=Se.fill,c.fill(),c.lineWidth=la?2.4:1.7,c.strokeStyle=la?_.edgeControl:_.nodeRing,c.stroke(),yo(c,Se.glyph,Se.x,Se.y),Se.collapsedCount&&Se.collapsedCount>0||Se.hasExpandedSubtypes){var Ka=Se.collapsedCount&&Se.collapsedCount>0?"+"+Se.collapsedCount:"−";c.font='400 10px "Atkinson Hyperlegible Mono", ui-monospace, monospace';var ca=Math.max(22,c.measureText(Ka).width+10),lr=17,Dt=Se.x-Se.radius*.78-ca/2,Qn=Se.y-Se.radius*.74-lr/2,cr=be===Se.kind;jt(c,Dt,Qn,ca,lr,8.5),c.fillStyle=cr?_.capHeadHoverBg:_.capHeadBg,c.fill(),c.lineWidth=1.2,c.strokeStyle=cr?_.capHoverBorder:_.capInnerBorder,c.stroke(),c.fillStyle=_.inkBright,c.textAlign="center",c.textBaseline="middle",c.fillText(Ka,Dt+ca/2,Qn+lr/2+.5)}c.font='400 12px "Host Grotesk", system-ui, sans-serif',c.fillStyle=kn?_.inkDim:_.inkBright,c.textAlign="center",c.textBaseline="top",c.fillText(Se.kind,Se.x,Se.y+Se.radius+7),c.restore()}if(c.restore(),ce.zoom>=.42){Ae.sort(function(Wr,fo){return Wr.prio-fo.prio}),c.font='400 9.5px "Atkinson Hyperlegible Mono", ui-monospace, monospace',c.textAlign="center",c.textBaseline="middle";for(var zn=0;zn<Ae.length;zn++){var Sn=Ae[zn],xn=Sn.mx*ce.zoom+ce.panX,Mn=Sn.my*ce.zoom+ce.panY;if(!(xn<-40||xn>v+40||Mn<-20||Mn>k+20)){var Dn=c.measureText(Sn.text).width+11,Yn=16,da={minX:xn-Dn/2-3,minY:Mn-Yn/2-2,maxX:xn+Dn/2+3,maxY:Mn+Yn/2+2},ks=!1;if(!Sn.active)for(var Hr=0;Hr<or.length;Hr++){var dr=or[Hr];if(!(da.maxX<dr.minX||da.minX>dr.maxX||da.maxY<dr.minY||da.minY>dr.maxY)){ks=!0;break}}ks||(or.push(da),jt(c,xn-Dn/2,Mn-Yn/2,Dn,Yn,5),c.fillStyle=_.pillBg,c.fill(),c.lineWidth=1,c.strokeStyle=Sn.active?_.pillBorderActive:Sn.isExtends?_.capInnerBorder:_.pillBorder,c.stroke(),c.fillStyle=Sn.active?_.inkBright:Sn.isExtends?_.capHoverBorder:_.inkSoft,c.fillText(Sn.text,xn,Mn+.5))}}}}}}}function Or(o){var u={},v=[],k={},S=A[o];if(!S)return{badges:u,conns:v};for(var c=S.inheritedOutEdges?S.outEdges.concat(S.inheritedOutEdges):S.outEdges,L=0;L<c.length;L++){var N=c[L];if(Z[N.category]){var H="out|"+N.rel+"|"+N.toKind;k[H]||(k[H]=!0,u[N.toKind]||(u[N.toKind]=[]),u[N.toKind].push({dir:"out",rel:N.rel,cat:N.category}),v.push({peer:N.toKind,rel:N.rel,dir:"out",cat:N.category}))}}for(var O=S.inheritedInEdges?S.inEdges.concat(S.inheritedInEdges):S.inEdges,Q=0;Q<O.length;Q++){var F=O[Q];if(Z[F.category]){var K="in|"+F.rel+"|"+F.fromKind;k[K]||(k[K]=!0,u[F.fromKind]||(u[F.fromKind]=[]),u[F.fromKind].push({dir:"in",rel:F.rel,cat:F.category}),v.push({peer:F.fromKind,rel:F.rel,dir:"in",cat:F.category}))}}return{badges:u,conns:v}}function Br(o,u){if(o){var v=document.getElementById("insp-layer-pill");v.textContent="Layer "+o.layerId,v.className="ont-layer-pill ont-layer-pill--l"+o.layerId;var k=document.getElementById("insp-abstract-pill");k&&(k.hidden=!o.isAbstract);var S=document.getElementById("insp-lineage");if(S){var c="";if(o.ancestors&&o.ancestors.length>0){c+='<div class="ont-lineage-row"><span class="ont-layer-sub">Extends</span>';for(var L=0;L<o.ancestors.length;L++){var N=o.ancestors[L];c+='<button type="button" class="ont-jump-btn" data-select-kind="'+ue(N)+'">:'+ue(N)+"</button>"}c+="</div>"}if(o.subtypes&&o.subtypes.length>0){c+='<div class="ont-lineage-row"><span class="ont-layer-sub">Subtypes ('+o.subtypes.length+")</span>";for(var H=0;H<o.subtypes.length;H++){var O=o.subtypes[H];c+='<button type="button" class="ont-jump-btn" data-select-kind="'+ue(O)+'">:'+ue(O)+"</button>"}c+="</div>"}S.innerHTML=c,S.hidden=!c}document.getElementById("insp-plural").textContent=o.pluralLabel,document.getElementById("insp-title").textContent=":"+o.kind,document.getElementById("insp-purpose").textContent=o.purpose,document.getElementById("insp-urn").textContent=o.idScheme||"n/a",document.getElementById("insp-props-heading").textContent="Collected properties ("+o.properties.length+" + "+w.commonProperties.length+" base)";for(var Q="",F=0;F<o.properties.length;F++){var K=o.properties[F],ae=u&&(K.name.toLowerCase().indexOf(u)!==-1||K.description.toLowerCase().indexOf(u)!==-1),Le=K.inheritedFrom?'<code class="ont-edge-prop">via :'+ue(K.inheritedFrom)+"</code>":"";Q+='<div class="ont-prop-item'+(ae?" is-match":"")+'"><div class="ont-prop-top"><code class="ont-prop-name">'+ue(K.name)+'</code><span class="ont-prop-right">'+Le+'<code class="ont-prop-type">'+ue(K.type)+'</code></span></div><span class="ont-prop-desc">'+ue(K.description)+"</span></div>"}document.getElementById("insp-props-list").innerHTML=Q;var pe=o.inheritedOutEdges?o.outEdges.concat(o.inheritedOutEdges):o.outEdges,Ne=o.inheritedInEdges?o.inEdges.concat(o.inheritedInEdges):o.inEdges;document.getElementById("insp-rels-heading").textContent="Typed relationships ("+pe.length+" out, "+Ne.length+" in)";for(var ve="",Ae=0;Ae<pe.length;Ae++){var Pe=pe[Ae],oe=$[Pe.rel],U="";if(oe&&oe.properties&&oe.properties.length>1){U='<div class="ont-edge-props-row">';for(var X=0;X<oe.properties.length;X++){var De=oe.properties[X];U+='<code class="ont-edge-prop" title="'+ue(De.description)+'">r.'+ue(De.name)+"</code>"}U+="</div>"}var Qe=Pe.inheritedFrom?'<code class="ont-edge-prop">via :'+ue(Pe.inheritedFrom)+"</code>":"";ve+='<div class="ont-rel-row"><div class="ont-rel-line"><code class="ont-edge-tag ont-edge-tag--'+ue(Pe.category)+'">-[:'+ue(Pe.rel)+']-&gt;</code><button type="button" class="ont-jump-btn" data-select-kind="'+ue(Pe.toKind)+'">:'+ue(Pe.toKind)+"</button>"+Qe+'</div><span class="ont-prop-desc">'+ue(Pe.description)+"</span>"+U+"</div>"}for(var ze=0;ze<Ne.length;ze++){var Oe=Ne[ze],Be=Oe.inheritedFrom?'<code class="ont-edge-prop">via :'+ue(Oe.inheritedFrom)+"</code>":"";ve+='<div class="ont-rel-row"><div class="ont-rel-line"><button type="button" class="ont-jump-btn" data-select-kind="'+ue(Oe.fromKind)+'">:'+ue(Oe.fromKind)+'</button><code class="ont-edge-tag ont-edge-tag--'+ue(Oe.category)+'">-[:'+ue(Oe.rel)+"]-&gt;</code>"+Be+'</div><span class="ont-prop-desc">'+ue(Oe.description)+"</span></div>"}document.getElementById("insp-rels-list").innerHTML=ve,document.getElementById("insp-cypher").textContent=o.sampleCypher;var We=document.getElementById("insp-compact-props-label");We&&(We.textContent="Properties ("+o.properties.length+" + "+w.commonProperties.length+" base)");var Ye=document.getElementById("insp-compact-props");if(Ye){for(var Ze="",he=Math.min(6,o.properties.length),Ke=0;Ke<he;Ke++){var mt=o.properties[Ke];Ze+='<code class="ont-edge-prop" title="'+ue(mt.description)+'">'+ue(mt.name)+": "+ue(mt.type)+"</code>"}o.properties.length>6&&(Ze+='<code class="ont-edge-prop">+'+(o.properties.length-6)+" more</code>"),Ye.innerHTML=Ze}var Rt=document.getElementById("insp-compact-rels-label");Rt&&(Rt.textContent="Connected entities ("+pe.length+" out, "+Ne.length+" in)");var an=document.getElementById("insp-compact-rels");if(an){for(var Pt={},ct=[],st=0;st<pe.length;st++){var je=pe[st].toKind;Pt[je]||(Pt[je]=!0,ct.push(je))}for(var xt=0;xt<Ne.length;xt++){var kt=Ne[xt].fromKind;Pt[kt]||(Pt[kt]=!0,ct.push(kt))}for(var Ge="",vn=Math.min(6,ct.length),_t=0;_t<vn;_t++){var dt=ct[_t];Ge+='<button type="button" class="ont-jump-btn" data-select-kind="'+ue(dt)+'">:'+ue(dt)+"</button>"}ct.length>6&&(Ge+='<code class="ont-edge-prop">+'+(ct.length-6)+" more</code>"),an.innerHTML=Ge}var Yt=document.getElementById("entity-inspector"),Wt=document.getElementById("ont-graph-panel"),Mt=!!(Wt&&Wt.classList.contains("is-fullscreen"));if(Yt){Yt.hidden=D==="graph"&&!V;var bn=D==="layers"||Mt||G;Yt.classList.toggle("is-compact",!bn),Yt.classList.toggle("is-expanded-view",bn)}var Gt=document.getElementById("insp-toggle-mode-btn");Gt&&(Gt.hidden=D==="layers"||Mt,Gt.textContent=G?"Compact":"Full");var Ut=document.getElementById("insp-close-btn");Ut&&(Ut.hidden=D==="layers")}}function ra(o){var u=document.getElementById("ont-focus-label"),v=document.getElementById("ont-focus-count"),k=document.getElementById("ont-focus-links");if(!(!u||!v||!k)){if(u.textContent="Active connections for :"+g,v.textContent=o.length+" active links",o.length===0){k.innerHTML='<span class="ont-layer-sub">Enable relationship category filters above to display connected entities.</span>';return}for(var S=Math.min(12,o.length),c="",L=0;L<S;L++){var N=o[L],H=N.dir==="out"?"out: "+ue(N.rel)+" :"+ue(N.peer):"in: :"+ue(N.peer)+" "+ue(N.rel);c+='<button type="button" class="ont-focus-chip ont-focus-chip--'+ue(N.cat)+'" data-select-kind="'+ue(N.peer)+'"><code>'+H+"</code></button>"}k.innerHTML=c}}function Nn(o,u){return!!de[o.layerId]&&tn(o,u)}function Rn(){if(_e!==null||fe)return;function o(){if(_e=null,!(fe||D!=="graph")){if(te+=(ke-te)*qe,te<B&&ke===0){te=0;return}Xa(x,C,te,ee,me),_a(Aa),bt(),_e=requestAnimationFrame(o)}}_e=requestAnimationFrame(o)}function vs(o){for(var u=[],v=0;v<w.nodes.length;v++){var k=w.nodes[v];Nn(k,o)&&u.push(k)}for(var S=ln(u),c=S.visibleDtos,L={},N=[],H=0;H<c.length;H++)L[c[H].kind]=!0,N.push(c[H].kind);for(var O=[],Q=0;Q<Me.length;Q++){var F=Me[Q].kind;O.push(F+":"+(we[F]?"1":"0"))}var K=N.join(",")+"|"+O.join(","),ae=!!(M&&M!==K);M=K,V&&!L[V]&&(V=S.redirectMap[V]||null),xr(c,S.redirectMap,S.collapsedCounts),fn=aa(L,S.redirectMap),ae&&Za&&Et.length>0&&(nn=!0,Ln()),!fe&&(te>=B||ke>0)&&Rn()}function wt(){var o=ye.trim().toLowerCase(),u=A[g];if(!u||!de[u.layerId]){for(var v=0;v<w.nodes.length;v++)if(Nn(w.nodes[v],o)){g=w.nodes[v].kind;break}if(!A[g]||!de[A[g].layerId]){for(var k=0;k<w.nodes.length;k++)if(de[w.nodes[k].layerId]){g=w.nodes[k].kind;break}}}for(var S=Or(g),c=document.querySelectorAll(".ont-node-btn"),L=["control_flow","data_flow","type_dependency","structural"],N=0;N<c.length;N++){var H=c[N],O=H.getAttribute("data-kind"),Q=A[O];if(Q){var F=Nn(Q,o);H.hidden=!F;var K=O===g,ae=S.badges[O]||[],Le=ae.length>0,pe=Le?ae[0].cat:"";H.setAttribute("aria-selected",K?"true":"false"),H.classList.toggle("is-neighbor",!K&&Le),H.classList.toggle("is-dimmed",!K&&!Le);for(var Ne=0;Ne<L.length;Ne++)H.classList.toggle("is-neighbor--"+L[Ne],!K&&Le&&pe===L[Ne])}}for(var ve=document.querySelectorAll(".ont-layer[data-layer-id]"),Ae=0;Ae<ve.length;Ae++){var Pe=Number(ve[Ae].getAttribute("data-layer-id"));ve[Ae].hidden=!de[Pe]}for(var oe=document.querySelectorAll(".ont-layer-btn"),U=0;U<oe.length;U++){var X=Number(oe[U].getAttribute("data-layer"));oe[U].setAttribute("aria-pressed",de[X]?"true":"false")}var De=document.getElementById("ont-capsule-group");De&&(De.hidden=D!=="graph"||!de[4]);for(var Qe=document.querySelectorAll(".ont-capsule-btn"),ze=0;ze<Qe.length;ze++){var Oe=Qe[ze].getAttribute("data-hierarchy"),Be=!!we[Oe],We=A[Oe];if(We&&We.ancestors)for(var Ye=0;Ye<We.ancestors.length;Ye++)we[We.ancestors[Ye]]||(Be=!1);Qe[ze].setAttribute("aria-pressed",Be?"true":"false")}var Ze=document.getElementById("ont-graph-panel"),he=document.getElementById("ont-canvas-frame"),Ke=document.getElementById("ont-graph-foot"),mt=document.getElementById("ont-graph-zoom-group"),Rt=document.getElementById("ont-swimlanes"),an=he?he.hidden:!1;Ze&&Ze.classList.toggle("is-layers-mode",D==="layers"),he&&(he.hidden=D!=="graph"),Ke&&(Ke.hidden=D!=="graph"),mt&&(mt.hidden=D!=="graph"),Rt&&(Rt.hidden=D!=="layers");var Pt=document.getElementById("graph-show-all");Pt&&(Pt.hidden=!V);for(var ct=document.querySelectorAll(".ont-mode-btn"),st=0;st<ct.length;st++){var je=ct[st].getAttribute("data-mode");ct[st].setAttribute("aria-pressed",je===D?"true":"false")}vs(o),an&&D==="graph"&&(nn=!0,Ln()),bt(),ra(S.conns),Br(A[g],o)}function Fr(o){if(o){var u=!we[o],v=A[o];if(v&&v.ancestors)for(var k=0;k<v.ancestors.length;k++)we[v.ancestors[k]]||(u=!0,we[v.ancestors[k]]=!0);we[o]=u,nn=!0,wt(),Ln(),bt()}}document.addEventListener("click",function(o){var u=o.target.closest("#insp-close-btn");if(u){V=null,G=!1,wt();return}var v=o.target.closest("#insp-toggle-mode-btn");if(v){G=!G,wt();return}var k=o.target.closest(".ont-mode-btn");if(k){D=k.getAttribute("data-mode")||"graph",wt();return}var S=o.target.closest(".ont-capsule-btn");if(S){Fr(S.getAttribute("data-hierarchy"));return}var c=o.target.closest(".ont-layer-btn");if(c){var L=Number(c.getAttribute("data-layer"));de[L]=!de[L],!de[5]&&!de[4]&&!de[3]&&!de[2]&&!de[1]&&(de={5:!0,4:!0,3:!0,2:!0,1:!0}),wt();return}var N=o.target.closest(".ont-node-btn");if(N){g=N.getAttribute("data-kind"),wt();return}var H=o.target.closest("[data-select-kind]");if(H){g=H.getAttribute("data-select-kind");var O=A[g];if(O&&(de[O.layerId]=!0,O.ancestors))for(var Q=0;Q<O.ancestors.length;Q++)we[O.ancestors[Q]]=!0;D==="graph"&&(V=g),wt();return}var F=o.target.closest(".ont-cat-btn");if(F){var K=F.getAttribute("data-cat");Z[K]=!Z[K],F.setAttribute("aria-pressed",Z[K]?"true":"false"),wt();return}});var lt=document.getElementById("ont-graph-canvas");lt&&(lt.style.cursor="grab",lt.addEventListener("pointerdown",function(o){var u=lt.getBoundingClientRect(),v=o.clientX-u.left,k=o.clientY-u.top,S=er(v,k);if(S){lt.setPointerCapture(o.pointerId),Fe={mode:"capsule",kind:S,sx:o.clientX,sy:o.clientY,ox:0,oy:0,moved:!1};return}var c=tr(v,k);if(lt.setPointerCapture(o.pointerId),lt.style.cursor="grabbing",c){Fe={mode:"node",kind:c.kind,sx:o.clientX,sy:o.clientY,ox:c.x,oy:c.y,moved:!1};var L=m[c.kind];L&&(L.fx=L.x,L.fy=L.y),fe||(ke=.3,te=Math.max(te,.3),Rn())}else Fe={mode:"pan",kind:null,sx:o.clientX,sy:o.clientY,ox:ce.panX,oy:ce.panY,moved:!1}}),lt.addEventListener("pointermove",function(o){var u=lt.getBoundingClientRect();if(Fe){var v=o.clientX-Fe.sx,k=o.clientY-Fe.sy;if((Math.abs(v)>4||Math.abs(k)>4)&&(Fe.moved=!0),Fe.mode==="pan")Fe.moved&&(nn=!1),ce.panX=Fe.ox+v,ce.panY=Fe.oy+k,bt();else if(Fe.mode==="node"&&Fe.kind){var S=Fe.ox+v/ce.zoom,c=Fe.oy+k/ce.zoom,L=m[Fe.kind];L&&(L.fx=S,L.fy=c,L.x=S,L.y=c),cn[Fe.kind]&&(cn[Fe.kind].x=S,cn[Fe.kind].y=c),fe||(te=Math.max(te,.3),Rn()),bt()}return}var N=o.clientX-u.left,H=o.clientY-u.top,O=er(N,H),Q=O?null:tr(N,H),F=Q?Q.kind:null;lt.style.cursor=O||F?"pointer":"grab",(O!==be||F!==re)&&(be=O,re=F,bt())}),lt.addEventListener("pointerup",function(o){lt.hasPointerCapture(o.pointerId)&&lt.releasePointerCapture(o.pointerId);var u=Fe;if(Fe=null,lt.style.cursor=be||re?"pointer":"grab",u&&u.mode==="node"&&u.kind){ke=0;var v=m[u.kind];v&&!fe&&(v.fx=null,v.fy=null),fe||Rn()}!u||u.moved||(u.mode==="capsule"&&u.kind?(be=null,Fr(u.kind)):u.mode==="node"&&u.kind?(g=u.kind,V=V===u.kind?null:u.kind,wt()):V&&(V=null,wt()))}),lt.addEventListener("pointerleave",function(){!Fe&&(re||be)&&(re=null,be=null,bt())}),lt.addEventListener("wheel",function(o){o.preventDefault(),nn=!1;var u=lt.getBoundingClientRect(),v=o.clientX-u.left,k=o.clientY-u.top,S=o.deltaY<0?1.12:.89,c=Math.max(.22,Math.min(2.8,ce.zoom*S)),L=c/ce.zoom;ce.zoom=c,ce.panX=v-(v-ce.panX)*L,ce.panY=k-(k-ce.panY)*L,bt()},{passive:!1}));function nr(o){nn=!1;var u=document.getElementById("ont-canvas-frame"),v=(u?u.clientWidth:760)/2,k=(u?u.clientHeight:640)/2,S=Math.max(.22,Math.min(2.8,ce.zoom*o)),c=S/ce.zoom;ce.zoom=S,ce.panX=v-(v-ce.panX)*c,ce.panY=k-(k-ce.panY)*c,bt()}var Kr=document.getElementById("graph-zoom-in"),sa=document.getElementById("graph-zoom-out"),ar=document.getElementById("graph-zoom-fit"),oa=document.getElementById("graph-show-all"),Na=document.getElementById("graph-fullscreen"),Ht=document.getElementById("ont-graph-panel"),Pn=document.getElementById("ont-canvas-frame");function Ra(){if(Pn){var o=Pn.clientWidth||0,u=Pn.clientHeight||0;o<=0||u<=0||o===La&&u===Mr||(La=o,Mr=u,nn&&Ln(),bt())}}function Pa(o){Ht&&(nn=!0,Ht.classList.toggle("is-fullscreen",o),document.body.classList.toggle("has-fullscreen-graph",o),Na&&(Na.setAttribute("aria-pressed",o?"true":"false"),Na.textContent=o?"Exit":"Expand"),wt(),Ln(),bt(),requestAnimationFrame(function(){Ln(),bt()}))}Na&&Ht&&Na.addEventListener("click",function(){var o=Ht.classList.contains("is-fullscreen");Pa(!o)}),document.addEventListener("keydown",function(o){o.key==="Escape"&&Ht&&Ht.classList.contains("is-fullscreen")&&Pa(!1)}),document.addEventListener("mousedown",function(o){Ht&&Ht.classList.contains("is-fullscreen")&&!Ht.contains(o.target)&&Pa(!1)});var xa=document.getElementById("graph-air-slider"),rr=document.getElementById("graph-air-val"),Ma=document.getElementById("graph-hub-slider"),dn=document.getElementById("graph-hub-val"),Da=document.getElementById("graph-freeze");if(xa&&(xa.value=String(ee)),rr&&(rr.textContent=ee.toFixed(1)+"x"),Ma&&(Ma.value=String(me)),dn&&(dn.textContent=me.toFixed(1)+"x"),xa&&xa.addEventListener("input",function(){var o=parseFloat(xa.value)||1;ee=Math.max(.1,Math.min(2.4,o)),rr&&(rr.textContent=ee.toFixed(1)+"x");try{localStorage.setItem("ce_ontology_air",String(ee))}catch{}fe||(te=Math.max(te,.38),Rn())}),Ma&&Ma.addEventListener("input",function(){var o=parseFloat(Ma.value),u=isNaN(o)?1:o;me=Math.max(0,Math.min(5,u)),dn&&(dn.textContent=me.toFixed(1)+"x");try{localStorage.setItem("ce_ontology_hub",String(me))}catch{}fe||(te=Math.max(te,.42),Rn())}),Da&&Da.addEventListener("click",function(){if(fe=!fe,Da.setAttribute("aria-pressed",fe?"true":"false"),Da.textContent=fe?"Frozen":"Freeze",fe){te=0,ke=0;for(var o=0;o<x.length;o++)x[o].vx=0,x[o].vy=0}else{for(var u=0;u<x.length;u++)x[u].fx=null,x[u].fy=null;te=Math.max(te,.28),Rn()}}),Kr&&Kr.addEventListener("click",function(){nr(1.18)}),sa&&sa.addEventListener("click",function(){nr(.84)}),ar&&ar.addEventListener("click",function(){V=null,nn=!0,Ia(),wt(),Ln(),bt()}),oa&&oa.addEventListener("click",function(){V=null,wt()}),Pn&&typeof ResizeObserver<"u"){var bs=new ResizeObserver(function(){Ra()});bs.observe(Pn)}window.addEventListener("resize",function(){Ra(),bt()});var jr=document.getElementById("ont-search");jr&&jr.addEventListener("input",function(){ye=jr.value||"",wt()});var ia=document.getElementById("copy-cypher-btn");return ia&&ia.addEventListener("click",function(){var o=document.getElementById("insp-cypher").textContent;navigator.clipboard&&navigator.clipboard.writeText(o).then(function(){var u=ia.textContent;ia.textContent="Copied",setTimeout(function(){ia.textContent=u},1500)})}),wt(),()=>{document.body.classList.remove("has-fullscreen-graph"),typeof _e<"u"&&_e!==null&&cancelAnimationFrame(_e)}}const fu="/ontology",vu=()=>{Ea.useEffect(()=>yu(),[]);const w=jn[fu];return R.jsx("main",{className:"ontology-page",dangerouslySetInnerHTML:{__html:w.mainHtml}})},bu="/benchmarks",wu=()=>{const w=jn[bu];return R.jsx("main",{dangerouslySetInnerHTML:{__html:w.mainHtml}})},ku="/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty",Su=({routePath:w})=>{const D=jn[w]||jn[ku];return R.jsx("main",{dangerouslySetInnerHTML:{__html:D.mainHtml}},w)},Cu="/privacy",Tu=()=>{const w=jn[Cu];return R.jsx("main",{className:"page-privacy",dangerouslySetInnerHTML:{__html:w.mainHtml}})},Ui="/",gs="/",hd="";function qi(w){const D=w.trim();if(!D||D===gs)return Ui;const g=D.startsWith(gs)?D:`${gs}${D}`,V=g.length>1&&g.endsWith(gs)?g.slice(0,-1):g;return jn[V]?V:Ui}function Eu(w,D,g){const V=`${D}${g}`,G=new URL(w,V);if(G.origin!==D)return{route:hd,hash:hd,isSpaRoute:!1};const re=G.pathname,be=re.length>1&&re.endsWith(gs)?re.slice(0,-1):re,ye=!!jn[be];return{route:ye?be:Ui,hash:G.hash,isSpaRoute:ye}}const _u="/",Au="/architectures",Iu="/ontology",Lu="/benchmarks",Nu="/privacy",gd="popstate",yd="click",Ru="a[href]",Pu="href",xu="target",Mu="download",Du="data-open-drawer",Ou="_blank",Bu="smooth",bd="#",fd=0;function vd(w){if(w&&w.startsWith(bd)&&w.length>1){const D=w.slice(1),g=document.getElementById(D);if(g){g.scrollIntoView({behavior:Bu});return}}window.scrollTo(fd,fd)}const Fu=({initialPath:w})=>{const[D,g]=Ea.useState(()=>qi(w));Ea.useEffect(()=>{const re=jn[D];re&&typeof document<"u"&&(document.title=re.title)},[D]),Ea.useEffect(()=>{const re=()=>{const ye=qi(window.location.pathname);g(ye);const Z=window.location.hash;requestAnimationFrame(()=>vd(Z))},be=ye=>{if(ye.defaultPrevented||ye.button!==0||ye.metaKey||ye.ctrlKey||ye.shiftKey||ye.altKey)return;const Z=ye.target;if(!Z||typeof Z.closest!="function")return;const de=Z.closest(Ru);if(!de||de.getAttribute(xu)===Ou||de.hasAttribute(Mu)||de.hasAttribute(Du))return;const we=de.getAttribute(Pu);if(!we||we.startsWith(bd))return;const b=Eu(we,window.location.origin,window.location.pathname);if(!b.isSpaRoute)return;ye.preventDefault();const M=`${b.route}${b.hash}`;window.history.pushState(null,"",M),g(b.route),requestAnimationFrame(()=>vd(b.hash))};return window.addEventListener(gd,re),document.addEventListener(yd,be),()=>{window.removeEventListener(gd,re),document.removeEventListener(yd,be)}},[]);let V,G=null;return D===_u?(V=R.jsx(pu,{}),G=R.jsx(uu,{})):D===Au?V=R.jsx(gu,{}):D===Iu?V=R.jsx(vu,{}):D===Lu?V=R.jsx(wu,{}):D===Nu?V=R.jsx(Tu,{}):V=R.jsx(Su,{routePath:D}),R.jsx(lu,{currentRoute:D,afterFooter:G,children:V})},Ku="root",ju="/",hs=document.getElementById(Ku);if(hs){const w=window.location.pathname,D=qi(w),g=R.jsx(Fu,{initialPath:w});hs.hasChildNodes()&&D===ju?ud.hydrateRoot(hs,g):(hs.innerHTML="",ud.createRoot(hs).render(g))}
