(function(){const D=document.createElement("link").relList;if(D&&D.supports&&D.supports("modulepreload"))return;for(const H of document.querySelectorAll('link[rel="modulepreload"]'))z(H);new MutationObserver(H=>{for(const ae of H)if(ae.type==="childList")for(const fe of ae.addedNodes)fe.tagName==="LINK"&&fe.rel==="modulepreload"&&z(fe)}).observe(document,{childList:!0,subtree:!0});function g(H){const ae={};return H.integrity&&(ae.integrity=H.integrity),H.referrerPolicy&&(ae.referrerPolicy=H.referrerPolicy),H.crossOrigin==="use-credentials"?ae.credentials="include":H.crossOrigin==="anonymous"?ae.credentials="omit":ae.credentials="same-origin",ae}function z(H){if(H.ep)return;H.ep=!0;const ae=g(H);fetch(H.href,ae)}})();var Hi={exports:{}},ho={},Gi={exports:{}},Ee={};/**
 * @license React
 * react.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var ad;function Up(){if(ad)return Ee;ad=1;var S=Symbol.for("react.element"),D=Symbol.for("react.portal"),g=Symbol.for("react.fragment"),z=Symbol.for("react.strict_mode"),H=Symbol.for("react.profiler"),ae=Symbol.for("react.provider"),fe=Symbol.for("react.context"),ye=Symbol.for("react.forward_ref"),ne=Symbol.for("react.suspense"),ge=Symbol.for("react.memo"),Ce=Symbol.for("react.lazy"),b=Symbol.iterator;function M(m){return m===null||typeof m!="object"?null:(m=b&&m[b]||m["@@iterator"],typeof m=="function"?m:null)}var j={isMounted:function(){return!1},enqueueForceUpdate:function(){},enqueueReplaceState:function(){},enqueueSetState:function(){}},T=Object.assign,Le={};function ke(m,k,Z){this.props=m,this.context=k,this.refs=Le,this.updater=Z||j}ke.prototype.isReactComponent={},ke.prototype.setState=function(m,k){if(typeof m!="object"&&typeof m!="function"&&m!=null)throw Error("setState(...): takes an object of state variables to update or a function which returns an object of state variables.");this.updater.enqueueSetState(this,m,k,"setState")},ke.prototype.forceUpdate=function(m){this.updater.enqueueForceUpdate(this,m,"forceUpdate")};function it(){}it.prototype=ke.prototype;function yt(m,k,Z){this.props=m,this.context=k,this.refs=Le,this.updater=Z||j}var ft=yt.prototype=new it;ft.constructor=yt,T(ft,ke.prototype),ft.isPureReactComponent=!0;var L=Array.isArray,te=Object.prototype.hasOwnProperty,$={current:null},Te={key:!0,ref:!0,__self:!0,__source:!0};function Je(m,k,Z){var ue,ee={},Se=null,ve=null;if(k!=null)for(ue in k.ref!==void 0&&(ve=k.ref),k.key!==void 0&&(Se=""+k.key),k)te.call(k,ue)&&!Te.hasOwnProperty(ue)&&(ee[ue]=k[ue]);var _e=arguments.length-2;if(_e===1)ee.children=Z;else if(1<_e){for(var He=Array(_e),Nt=0;Nt<_e;Nt++)He[Nt]=arguments[Nt+2];ee.children=He}if(m&&m.defaultProps)for(ue in _e=m.defaultProps,_e)ee[ue]===void 0&&(ee[ue]=_e[ue]);return{$$typeof:S,type:m,key:Se,ref:ve,props:ee,_owner:$.current}}function Me(m,k){return{$$typeof:S,type:m.type,key:k,ref:m.ref,props:m.props,_owner:m._owner}}function vt(m){return typeof m=="object"&&m!==null&&m.$$typeof===S}function It(m){var k={"=":"=0",":":"=2"};return"$"+m.replace(/[=:]/g,function(Z){return k[Z]})}var ln=/\/+/g;function pe(m,k){return typeof m=="object"&&m!==null&&m.key!=null?It(""+m.key):k.toString(36)}function tn(m,k,Z,ue,ee){var Se=typeof m;(Se==="undefined"||Se==="boolean")&&(m=null);var ve=!1;if(m===null)ve=!0;else switch(Se){case"string":case"number":ve=!0;break;case"object":switch(m.$$typeof){case S:case D:ve=!0}}if(ve)return ve=m,ee=ee(ve),m=ue===""?"."+pe(ve,0):ue,L(ee)?(Z="",m!=null&&(Z=m.replace(ln,"$&/")+"/"),tn(ee,k,Z,"",function(Nt){return Nt})):ee!=null&&(vt(ee)&&(ee=Me(ee,Z+(!ee.key||ve&&ve.key===ee.key?"":(""+ee.key).replace(ln,"$&/")+"/")+m)),k.push(ee)),1;if(ve=0,ue=ue===""?".":ue+":",L(m))for(var _e=0;_e<m.length;_e++){Se=m[_e];var He=ue+pe(Se,_e);ve+=tn(Se,k,Z,He,ee)}else if(He=M(m),typeof He=="function")for(m=He.call(m),_e=0;!(Se=m.next()).done;)Se=Se.value,He=ue+pe(Se,_e++),ve+=tn(Se,k,Z,He,ee);else if(Se==="object")throw k=String(m),Error("Objects are not valid as a React child (found: "+(k==="[object Object]"?"object with keys {"+Object.keys(m).join(", ")+"}":k)+"). If you meant to render a collection of children, use an array instead.");return ve}function ut(m,k,Z){if(m==null)return m;var ue=[],ee=0;return tn(m,ue,"","",function(Se){return k.call(Z,Se,ee++)}),ue}function tt(m){if(m._status===-1){var k=m._result;k=k(),k.then(function(Z){(m._status===0||m._status===-1)&&(m._status=1,m._result=Z)},function(Z){(m._status===0||m._status===-1)&&(m._status=2,m._result=Z)}),m._status===-1&&(m._status=0,m._result=k)}if(m._status===1)return m._result.default;throw m._result}var Qe={current:null},B={transition:null},oe={ReactCurrentDispatcher:Qe,ReactCurrentBatchConfig:B,ReactCurrentOwner:$};function x(){throw Error("act(...) is not supported in production builds of React.")}return Ee.Children={map:ut,forEach:function(m,k,Z){ut(m,function(){k.apply(this,arguments)},Z)},count:function(m){var k=0;return ut(m,function(){k++}),k},toArray:function(m){return ut(m,function(k){return k})||[]},only:function(m){if(!vt(m))throw Error("React.Children.only expected to receive a single React element child.");return m}},Ee.Component=ke,Ee.Fragment=g,Ee.Profiler=H,Ee.PureComponent=yt,Ee.StrictMode=z,Ee.Suspense=ne,Ee.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED=oe,Ee.act=x,Ee.cloneElement=function(m,k,Z){if(m==null)throw Error("React.cloneElement(...): The argument must be a React element, but you passed "+m+".");var ue=T({},m.props),ee=m.key,Se=m.ref,ve=m._owner;if(k!=null){if(k.ref!==void 0&&(Se=k.ref,ve=$.current),k.key!==void 0&&(ee=""+k.key),m.type&&m.type.defaultProps)var _e=m.type.defaultProps;for(He in k)te.call(k,He)&&!Te.hasOwnProperty(He)&&(ue[He]=k[He]===void 0&&_e!==void 0?_e[He]:k[He])}var He=arguments.length-2;if(He===1)ue.children=Z;else if(1<He){_e=Array(He);for(var Nt=0;Nt<He;Nt++)_e[Nt]=arguments[Nt+2];ue.children=_e}return{$$typeof:S,type:m.type,key:ee,ref:Se,props:ue,_owner:ve}},Ee.createContext=function(m){return m={$$typeof:fe,_currentValue:m,_currentValue2:m,_threadCount:0,Provider:null,Consumer:null,_defaultValue:null,_globalName:null},m.Provider={$$typeof:ae,_context:m},m.Consumer=m},Ee.createElement=Je,Ee.createFactory=function(m){var k=Je.bind(null,m);return k.type=m,k},Ee.createRef=function(){return{current:null}},Ee.forwardRef=function(m){return{$$typeof:ye,render:m}},Ee.isValidElement=vt,Ee.lazy=function(m){return{$$typeof:Ce,_payload:{_status:-1,_result:m},_init:tt}},Ee.memo=function(m,k){return{$$typeof:ge,type:m,compare:k===void 0?null:k}},Ee.startTransition=function(m){var k=B.transition;B.transition={};try{m()}finally{B.transition=k}},Ee.unstable_act=x,Ee.useCallback=function(m,k){return Qe.current.useCallback(m,k)},Ee.useContext=function(m){return Qe.current.useContext(m)},Ee.useDebugValue=function(){},Ee.useDeferredValue=function(m){return Qe.current.useDeferredValue(m)},Ee.useEffect=function(m,k){return Qe.current.useEffect(m,k)},Ee.useId=function(){return Qe.current.useId()},Ee.useImperativeHandle=function(m,k,Z){return Qe.current.useImperativeHandle(m,k,Z)},Ee.useInsertionEffect=function(m,k){return Qe.current.useInsertionEffect(m,k)},Ee.useLayoutEffect=function(m,k){return Qe.current.useLayoutEffect(m,k)},Ee.useMemo=function(m,k){return Qe.current.useMemo(m,k)},Ee.useReducer=function(m,k,Z){return Qe.current.useReducer(m,k,Z)},Ee.useRef=function(m){return Qe.current.useRef(m)},Ee.useState=function(m){return Qe.current.useState(m)},Ee.useSyncExternalStore=function(m,k,Z){return Qe.current.useSyncExternalStore(m,k,Z)},Ee.useTransition=function(){return Qe.current.useTransition()},Ee.version="18.3.1",Ee}var od;function qi(){return od||(od=1,Gi.exports=Up()),Gi.exports}/**
 * @license React
 * react-jsx-runtime.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var sd;function Qp(){if(sd)return ho;sd=1;var S=qi(),D=Symbol.for("react.element"),g=Symbol.for("react.fragment"),z=Object.prototype.hasOwnProperty,H=S.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED.ReactCurrentOwner,ae={key:!0,ref:!0,__self:!0,__source:!0};function fe(ye,ne,ge){var Ce,b={},M=null,j=null;ge!==void 0&&(M=""+ge),ne.key!==void 0&&(M=""+ne.key),ne.ref!==void 0&&(j=ne.ref);for(Ce in ne)z.call(ne,Ce)&&!ae.hasOwnProperty(Ce)&&(b[Ce]=ne[Ce]);if(ye&&ye.defaultProps)for(Ce in ne=ye.defaultProps,ne)b[Ce]===void 0&&(b[Ce]=ne[Ce]);return{$$typeof:D,type:ye,key:M,ref:j,props:b,_owner:H.current}}return ho.Fragment=g,ho.jsx=fe,ho.jsxs=fe,ho}var id;function zp(){return id||(id=1,Hi.exports=Qp()),Hi.exports}var A=zp(),gs={},Wi={exports:{}},en={},Ui={exports:{}},Qi={};/**
 * @license React
 * scheduler.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var ld;function qp(){return ld||(ld=1,(function(S){function D(B,oe){var x=B.length;B.push(oe);e:for(;0<x;){var m=x-1>>>1,k=B[m];if(0<H(k,oe))B[m]=oe,B[x]=k,x=m;else break e}}function g(B){return B.length===0?null:B[0]}function z(B){if(B.length===0)return null;var oe=B[0],x=B.pop();if(x!==oe){B[0]=x;e:for(var m=0,k=B.length,Z=k>>>1;m<Z;){var ue=2*(m+1)-1,ee=B[ue],Se=ue+1,ve=B[Se];if(0>H(ee,x))Se<k&&0>H(ve,ee)?(B[m]=ve,B[Se]=x,m=Se):(B[m]=ee,B[ue]=x,m=ue);else if(Se<k&&0>H(ve,x))B[m]=ve,B[Se]=x,m=Se;else break e}}return oe}function H(B,oe){var x=B.sortIndex-oe.sortIndex;return x!==0?x:B.id-oe.id}if(typeof performance=="object"&&typeof performance.now=="function"){var ae=performance;S.unstable_now=function(){return ae.now()}}else{var fe=Date,ye=fe.now();S.unstable_now=function(){return fe.now()-ye}}var ne=[],ge=[],Ce=1,b=null,M=3,j=!1,T=!1,Le=!1,ke=typeof setTimeout=="function"?setTimeout:null,it=typeof clearTimeout=="function"?clearTimeout:null,yt=typeof setImmediate<"u"?setImmediate:null;typeof navigator<"u"&&navigator.scheduling!==void 0&&navigator.scheduling.isInputPending!==void 0&&navigator.scheduling.isInputPending.bind(navigator.scheduling);function ft(B){for(var oe=g(ge);oe!==null;){if(oe.callback===null)z(ge);else if(oe.startTime<=B)z(ge),oe.sortIndex=oe.expirationTime,D(ne,oe);else break;oe=g(ge)}}function L(B){if(Le=!1,ft(B),!T)if(g(ne)!==null)T=!0,tt(te);else{var oe=g(ge);oe!==null&&Qe(L,oe.startTime-B)}}function te(B,oe){T=!1,Le&&(Le=!1,it(Je),Je=-1),j=!0;var x=M;try{for(ft(oe),b=g(ne);b!==null&&(!(b.expirationTime>oe)||B&&!It());){var m=b.callback;if(typeof m=="function"){b.callback=null,M=b.priorityLevel;var k=m(b.expirationTime<=oe);oe=S.unstable_now(),typeof k=="function"?b.callback=k:b===g(ne)&&z(ne),ft(oe)}else z(ne);b=g(ne)}if(b!==null)var Z=!0;else{var ue=g(ge);ue!==null&&Qe(L,ue.startTime-oe),Z=!1}return Z}finally{b=null,M=x,j=!1}}var $=!1,Te=null,Je=-1,Me=5,vt=-1;function It(){return!(S.unstable_now()-vt<Me)}function ln(){if(Te!==null){var B=S.unstable_now();vt=B;var oe=!0;try{oe=Te(!0,B)}finally{oe?pe():($=!1,Te=null)}}else $=!1}var pe;if(typeof yt=="function")pe=function(){yt(ln)};else if(typeof MessageChannel<"u"){var tn=new MessageChannel,ut=tn.port2;tn.port1.onmessage=ln,pe=function(){ut.postMessage(null)}}else pe=function(){ke(ln,0)};function tt(B){Te=B,$||($=!0,pe())}function Qe(B,oe){Je=ke(function(){B(S.unstable_now())},oe)}S.unstable_IdlePriority=5,S.unstable_ImmediatePriority=1,S.unstable_LowPriority=4,S.unstable_NormalPriority=3,S.unstable_Profiling=null,S.unstable_UserBlockingPriority=2,S.unstable_cancelCallback=function(B){B.callback=null},S.unstable_continueExecution=function(){T||j||(T=!0,tt(te))},S.unstable_forceFrameRate=function(B){0>B||125<B?console.error("forceFrameRate takes a positive int between 0 and 125, forcing frame rates higher than 125 fps is not supported"):Me=0<B?Math.floor(1e3/B):5},S.unstable_getCurrentPriorityLevel=function(){return M},S.unstable_getFirstCallbackNode=function(){return g(ne)},S.unstable_next=function(B){switch(M){case 1:case 2:case 3:var oe=3;break;default:oe=M}var x=M;M=oe;try{return B()}finally{M=x}},S.unstable_pauseExecution=function(){},S.unstable_requestPaint=function(){},S.unstable_runWithPriority=function(B,oe){switch(B){case 1:case 2:case 3:case 4:case 5:break;default:B=3}var x=M;M=B;try{return oe()}finally{M=x}},S.unstable_scheduleCallback=function(B,oe,x){var m=S.unstable_now();switch(typeof x=="object"&&x!==null?(x=x.delay,x=typeof x=="number"&&0<x?m+x:m):x=m,B){case 1:var k=-1;break;case 2:k=250;break;case 5:k=1073741823;break;case 4:k=1e4;break;default:k=5e3}return k=x+k,B={id:Ce++,callback:oe,priorityLevel:B,startTime:x,expirationTime:k,sortIndex:-1},x>m?(B.sortIndex=x,D(ge,B),g(ne)===null&&B===g(ge)&&(Le?(it(Je),Je=-1):Le=!0,Qe(L,x-m))):(B.sortIndex=k,D(ne,B),T||j||(T=!0,tt(te))),B},S.unstable_shouldYield=It,S.unstable_wrapCallback=function(B){var oe=M;return function(){var x=M;M=oe;try{return B.apply(this,arguments)}finally{M=x}}}})(Qi)),Qi}var cd;function Yp(){return cd||(cd=1,Ui.exports=qp()),Ui.exports}/**
 * @license React
 * react-dom.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var dd;function Vp(){if(dd)return en;dd=1;var S=qi(),D=Yp();function g(e){for(var t="https://reactjs.org/docs/error-decoder.html?invariant="+e,n=1;n<arguments.length;n++)t+="&args[]="+encodeURIComponent(arguments[n]);return"Minified React error #"+e+"; visit "+t+" for the full message or use the non-minified dev environment for full errors and additional helpful warnings."}var z=new Set,H={};function ae(e,t){fe(e,t),fe(e+"Capture",t)}function fe(e,t){for(H[e]=t,e=0;e<t.length;e++)z.add(t[e])}var ye=!(typeof window>"u"||typeof window.document>"u"||typeof window.document.createElement>"u"),ne=Object.prototype.hasOwnProperty,ge=/^[:A-Z_a-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD][:A-Z_a-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD\-.0-9\u00B7\u0300-\u036F\u203F-\u2040]*$/,Ce={},b={};function M(e){return ne.call(b,e)?!0:ne.call(Ce,e)?!1:ge.test(e)?b[e]=!0:(Ce[e]=!0,!1)}function j(e,t,n,r){if(n!==null&&n.type===0)return!1;switch(typeof t){case"function":case"symbol":return!0;case"boolean":return r?!1:n!==null?!n.acceptsBooleans:(e=e.toLowerCase().slice(0,5),e!=="data-"&&e!=="aria-");default:return!1}}function T(e,t,n,r){if(t===null||typeof t>"u"||j(e,t,n,r))return!0;if(r)return!1;if(n!==null)switch(n.type){case 3:return!t;case 4:return t===!1;case 5:return isNaN(t);case 6:return isNaN(t)||1>t}return!1}function Le(e,t,n,r,a,o,i){this.acceptsBooleans=t===2||t===3||t===4,this.attributeName=r,this.attributeNamespace=a,this.mustUseProperty=n,this.propertyName=e,this.type=t,this.sanitizeURL=o,this.removeEmptyString=i}var ke={};"children dangerouslySetInnerHTML defaultValue defaultChecked innerHTML suppressContentEditableWarning suppressHydrationWarning style".split(" ").forEach(function(e){ke[e]=new Le(e,0,!1,e,null,!1,!1)}),[["acceptCharset","accept-charset"],["className","class"],["htmlFor","for"],["httpEquiv","http-equiv"]].forEach(function(e){var t=e[0];ke[t]=new Le(t,1,!1,e[1],null,!1,!1)}),["contentEditable","draggable","spellCheck","value"].forEach(function(e){ke[e]=new Le(e,2,!1,e.toLowerCase(),null,!1,!1)}),["autoReverse","externalResourcesRequired","focusable","preserveAlpha"].forEach(function(e){ke[e]=new Le(e,2,!1,e,null,!1,!1)}),"allowFullScreen async autoFocus autoPlay controls default defer disabled disablePictureInPicture disableRemotePlayback formNoValidate hidden loop noModule noValidate open playsInline readOnly required reversed scoped seamless itemScope".split(" ").forEach(function(e){ke[e]=new Le(e,3,!1,e.toLowerCase(),null,!1,!1)}),["checked","multiple","muted","selected"].forEach(function(e){ke[e]=new Le(e,3,!0,e,null,!1,!1)}),["capture","download"].forEach(function(e){ke[e]=new Le(e,4,!1,e,null,!1,!1)}),["cols","rows","size","span"].forEach(function(e){ke[e]=new Le(e,6,!1,e,null,!1,!1)}),["rowSpan","start"].forEach(function(e){ke[e]=new Le(e,5,!1,e.toLowerCase(),null,!1,!1)});var it=/[\-:]([a-z])/g;function yt(e){return e[1].toUpperCase()}"accent-height alignment-baseline arabic-form baseline-shift cap-height clip-path clip-rule color-interpolation color-interpolation-filters color-profile color-rendering dominant-baseline enable-background fill-opacity fill-rule flood-color flood-opacity font-family font-size font-size-adjust font-stretch font-style font-variant font-weight glyph-name glyph-orientation-horizontal glyph-orientation-vertical horiz-adv-x horiz-origin-x image-rendering letter-spacing lighting-color marker-end marker-mid marker-start overline-position overline-thickness paint-order panose-1 pointer-events rendering-intent shape-rendering stop-color stop-opacity strikethrough-position strikethrough-thickness stroke-dasharray stroke-dashoffset stroke-linecap stroke-linejoin stroke-miterlimit stroke-opacity stroke-width text-anchor text-decoration text-rendering underline-position underline-thickness unicode-bidi unicode-range units-per-em v-alphabetic v-hanging v-ideographic v-mathematical vector-effect vert-adv-y vert-origin-x vert-origin-y word-spacing writing-mode xmlns:xlink x-height".split(" ").forEach(function(e){var t=e.replace(it,yt);ke[t]=new Le(t,1,!1,e,null,!1,!1)}),"xlink:actuate xlink:arcrole xlink:role xlink:show xlink:title xlink:type".split(" ").forEach(function(e){var t=e.replace(it,yt);ke[t]=new Le(t,1,!1,e,"http://www.w3.org/1999/xlink",!1,!1)}),["xml:base","xml:lang","xml:space"].forEach(function(e){var t=e.replace(it,yt);ke[t]=new Le(t,1,!1,e,"http://www.w3.org/XML/1998/namespace",!1,!1)}),["tabIndex","crossOrigin"].forEach(function(e){ke[e]=new Le(e,1,!1,e.toLowerCase(),null,!1,!1)}),ke.xlinkHref=new Le("xlinkHref",1,!1,"xlink:href","http://www.w3.org/1999/xlink",!0,!1),["src","href","action","formAction"].forEach(function(e){ke[e]=new Le(e,1,!1,e.toLowerCase(),null,!0,!0)});function ft(e,t,n,r){var a=ke.hasOwnProperty(t)?ke[t]:null;(a!==null?a.type!==0:r||!(2<t.length)||t[0]!=="o"&&t[0]!=="O"||t[1]!=="n"&&t[1]!=="N")&&(T(t,n,a,r)&&(n=null),r||a===null?M(t)&&(n===null?e.removeAttribute(t):e.setAttribute(t,""+n)):a.mustUseProperty?e[a.propertyName]=n===null?a.type===3?!1:"":n:(t=a.attributeName,r=a.attributeNamespace,n===null?e.removeAttribute(t):(a=a.type,n=a===3||a===4&&n===!0?"":""+n,r?e.setAttributeNS(r,t,n):e.setAttribute(t,n))))}var L=S.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED,te=Symbol.for("react.element"),$=Symbol.for("react.portal"),Te=Symbol.for("react.fragment"),Je=Symbol.for("react.strict_mode"),Me=Symbol.for("react.profiler"),vt=Symbol.for("react.provider"),It=Symbol.for("react.context"),ln=Symbol.for("react.forward_ref"),pe=Symbol.for("react.suspense"),tn=Symbol.for("react.suspense_list"),ut=Symbol.for("react.memo"),tt=Symbol.for("react.lazy"),Qe=Symbol.for("react.offscreen"),B=Symbol.iterator;function oe(e){return e===null||typeof e!="object"?null:(e=B&&e[B]||e["@@iterator"],typeof e=="function"?e:null)}var x=Object.assign,m;function k(e){if(m===void 0)try{throw Error()}catch(n){var t=n.stack.trim().match(/\n( *(at )?)/);m=t&&t[1]||""}return`
`+m+e}var Z=!1;function ue(e,t){if(!e||Z)return"";Z=!0;var n=Error.prepareStackTrace;Error.prepareStackTrace=void 0;try{if(t)if(t=function(){throw Error()},Object.defineProperty(t.prototype,"props",{set:function(){throw Error()}}),typeof Reflect=="object"&&Reflect.construct){try{Reflect.construct(t,[])}catch(f){var r=f}Reflect.construct(e,[],t)}else{try{t.call()}catch(f){r=f}e.call(t.prototype)}else{try{throw Error()}catch(f){r=f}e()}}catch(f){if(f&&r&&typeof f.stack=="string"){for(var a=f.stack.split(`
`),o=r.stack.split(`
`),i=a.length-1,l=o.length-1;1<=i&&0<=l&&a[i]!==o[l];)l--;for(;1<=i&&0<=l;i--,l--)if(a[i]!==o[l]){if(i!==1||l!==1)do if(i--,l--,0>l||a[i]!==o[l]){var d=`
`+a[i].replace(" at new "," at ");return e.displayName&&d.includes("<anonymous>")&&(d=d.replace("<anonymous>",e.displayName)),d}while(1<=i&&0<=l);break}}}finally{Z=!1,Error.prepareStackTrace=n}return(e=e?e.displayName||e.name:"")?k(e):""}function ee(e){switch(e.tag){case 5:return k(e.type);case 16:return k("Lazy");case 13:return k("Suspense");case 19:return k("SuspenseList");case 0:case 2:case 15:return e=ue(e.type,!1),e;case 11:return e=ue(e.type.render,!1),e;case 1:return e=ue(e.type,!0),e;default:return""}}function Se(e){if(e==null)return null;if(typeof e=="function")return e.displayName||e.name||null;if(typeof e=="string")return e;switch(e){case Te:return"Fragment";case $:return"Portal";case Me:return"Profiler";case Je:return"StrictMode";case pe:return"Suspense";case tn:return"SuspenseList"}if(typeof e=="object")switch(e.$$typeof){case It:return(e.displayName||"Context")+".Consumer";case vt:return(e._context.displayName||"Context")+".Provider";case ln:var t=e.render;return e=e.displayName,e||(e=t.displayName||t.name||"",e=e!==""?"ForwardRef("+e+")":"ForwardRef"),e;case ut:return t=e.displayName||null,t!==null?t:Se(e.type)||"Memo";case tt:t=e._payload,e=e._init;try{return Se(e(t))}catch{}}return null}function ve(e){var t=e.type;switch(e.tag){case 24:return"Cache";case 9:return(t.displayName||"Context")+".Consumer";case 10:return(t._context.displayName||"Context")+".Provider";case 18:return"DehydratedFragment";case 11:return e=t.render,e=e.displayName||e.name||"",t.displayName||(e!==""?"ForwardRef("+e+")":"ForwardRef");case 7:return"Fragment";case 5:return t;case 4:return"Portal";case 3:return"Root";case 6:return"Text";case 16:return Se(t);case 8:return t===Je?"StrictMode":"Mode";case 22:return"Offscreen";case 12:return"Profiler";case 21:return"Scope";case 13:return"Suspense";case 19:return"SuspenseList";case 25:return"TracingMarker";case 1:case 0:case 17:case 2:case 14:case 15:if(typeof t=="function")return t.displayName||t.name||null;if(typeof t=="string")return t}return null}function _e(e){switch(typeof e){case"boolean":case"number":case"string":case"undefined":return e;case"object":return e;default:return""}}function He(e){var t=e.type;return(e=e.nodeName)&&e.toLowerCase()==="input"&&(t==="checkbox"||t==="radio")}function Nt(e){var t=He(e)?"checked":"value",n=Object.getOwnPropertyDescriptor(e.constructor.prototype,t),r=""+e[t];if(!e.hasOwnProperty(t)&&typeof n<"u"&&typeof n.get=="function"&&typeof n.set=="function"){var a=n.get,o=n.set;return Object.defineProperty(e,t,{configurable:!0,get:function(){return a.call(this)},set:function(i){r=""+i,o.call(this,i)}}),Object.defineProperty(e,t,{enumerable:n.enumerable}),{getValue:function(){return r},setValue:function(i){r=""+i},stopTracking:function(){e._valueTracker=null,delete e[t]}}}}function Hn(e){e._valueTracker||(e._valueTracker=Nt(e))}function Pa(e){if(!e)return!1;var t=e._valueTracker;if(!t)return!0;var n=t.getValue(),r="";return e&&(r=He(e)?e.checked?"true":"false":e.value),e=r,e!==n?(t.setValue(e),!0):!1}function Gn(e){if(e=e||(typeof document<"u"?document:void 0),typeof e>"u")return null;try{return e.activeElement||e.body}catch{return e.body}}function Ra(e,t){var n=t.checked;return x({},t,{defaultChecked:void 0,defaultValue:void 0,value:void 0,checked:n??e._wrapperState.initialChecked})}function fo(e,t){var n=t.defaultValue==null?"":t.defaultValue,r=t.checked!=null?t.checked:t.defaultChecked;n=_e(t.value!=null?t.value:n),e._wrapperState={initialChecked:r,initialValue:n,controlled:t.type==="checkbox"||t.type==="radio"?t.checked!=null:t.value!=null}}function Jr(e,t){t=t.checked,t!=null&&ft(e,"checked",t,!1)}function _r(e,t){Jr(e,t);var n=_e(t.value),r=t.type;if(n!=null)r==="number"?(n===0&&e.value===""||e.value!=n)&&(e.value=""+n):e.value!==""+n&&(e.value=""+n);else if(r==="submit"||r==="reset"){e.removeAttribute("value");return}t.hasOwnProperty("value")?xa(e,t.type,n):t.hasOwnProperty("defaultValue")&&xa(e,t.type,_e(t.defaultValue)),t.checked==null&&t.defaultChecked!=null&&(e.defaultChecked=!!t.defaultChecked)}function Ar(e,t,n){if(t.hasOwnProperty("value")||t.hasOwnProperty("defaultValue")){var r=t.type;if(!(r!=="submit"&&r!=="reset"||t.value!==void 0&&t.value!==null))return;t=""+e._wrapperState.initialValue,n||t===e.value||(e.value=t),e.defaultValue=t}n=e.name,n!==""&&(e.name=""),e.defaultChecked=!!e._wrapperState.initialChecked,n!==""&&(e.name=n)}function xa(e,t,n){(t!=="number"||Gn(e.ownerDocument)!==e)&&(n==null?e.defaultValue=""+e._wrapperState.initialValue:e.defaultValue!==""+n&&(e.defaultValue=""+n))}var Lr=Array.isArray;function rr(e,t,n,r){if(e=e.options,t){t={};for(var a=0;a<n.length;a++)t["$"+n[a]]=!0;for(n=0;n<e.length;n++)a=t.hasOwnProperty("$"+e[n].value),e[n].selected!==a&&(e[n].selected=a),a&&r&&(e[n].defaultSelected=!0)}else{for(n=""+_e(n),t=null,a=0;a<e.length;a++){if(e[a].value===n){e[a].selected=!0,r&&(e[a].defaultSelected=!0);return}t!==null||e[a].disabled||(t=e[a])}t!==null&&(t.selected=!0)}}function Tt(e,t){if(t.dangerouslySetInnerHTML!=null)throw Error(g(91));return x({},t,{value:void 0,defaultValue:void 0,children:""+e._wrapperState.initialValue})}function cn(e,t){var n=t.value;if(n==null){if(n=t.children,t=t.defaultValue,n!=null){if(t!=null)throw Error(g(92));if(Lr(n)){if(1<n.length)throw Error(g(93));n=n[0]}t=n}t==null&&(t=""),n=t}e._wrapperState={initialValue:_e(n)}}function fn(e,t){var n=_e(t.value),r=_e(t.defaultValue);n!=null&&(n=""+n,n!==e.value&&(e.value=n),t.defaultValue==null&&e.defaultValue!==n&&(e.defaultValue=n)),r!=null&&(e.defaultValue=""+r)}function ce(e){var t=e.textContent;t===e._wrapperState.initialValue&&t!==""&&t!==null&&(e.value=t)}function Zr(e){switch(e){case"svg":return"http://www.w3.org/2000/svg";case"math":return"http://www.w3.org/1998/Math/MathML";default:return"http://www.w3.org/1999/xhtml"}}function nn(e,t){return e==null||e==="http://www.w3.org/1999/xhtml"?Zr(t):e==="http://www.w3.org/2000/svg"&&t==="foreignObject"?"http://www.w3.org/1999/xhtml":e}var Ir,Ma=(function(e){return typeof MSApp<"u"&&MSApp.execUnsafeLocalFunction?function(t,n,r,a){MSApp.execUnsafeLocalFunction(function(){return e(t,n,r,a)})}:e})(function(e,t){if(e.namespaceURI!=="http://www.w3.org/2000/svg"||"innerHTML"in e)e.innerHTML=t;else{for(Ir=Ir||document.createElement("div"),Ir.innerHTML="<svg>"+t.valueOf().toString()+"</svg>",t=Ir.firstChild;e.firstChild;)e.removeChild(e.firstChild);for(;t.firstChild;)e.appendChild(t.firstChild)}});function Fe(e,t){if(t){var n=e.firstChild;if(n&&n===e.lastChild&&n.nodeType===3){n.nodeValue=t;return}}e.textContent=t}var jt={animationIterationCount:!0,aspectRatio:!0,borderImageOutset:!0,borderImageSlice:!0,borderImageWidth:!0,boxFlex:!0,boxFlexGroup:!0,boxOrdinalGroup:!0,columnCount:!0,columns:!0,flex:!0,flexGrow:!0,flexPositive:!0,flexShrink:!0,flexNegative:!0,flexOrder:!0,gridArea:!0,gridRow:!0,gridRowEnd:!0,gridRowSpan:!0,gridRowStart:!0,gridColumn:!0,gridColumnEnd:!0,gridColumnSpan:!0,gridColumnStart:!0,fontWeight:!0,lineClamp:!0,lineHeight:!0,opacity:!0,order:!0,orphans:!0,tabSize:!0,widows:!0,zIndex:!0,zoom:!0,fillOpacity:!0,floodOpacity:!0,stopOpacity:!0,strokeDasharray:!0,strokeDashoffset:!0,strokeMiterlimit:!0,strokeOpacity:!0,strokeWidth:!0},vs=["Webkit","ms","Moz","O"];Object.keys(jt).forEach(function(e){vs.forEach(function(t){t=t+e.charAt(0).toUpperCase()+e.substring(1),jt[t]=jt[e]})});function Da(e,t,n){return t==null||typeof t=="boolean"||t===""?"":n||typeof t!="number"||t===0||jt.hasOwnProperty(e)&&jt[e]?(""+t).trim():t+"px"}function vo(e,t){e=e.style;for(var n in t)if(t.hasOwnProperty(n)){var r=n.indexOf("--")===0,a=Da(n,t[n],r);n==="float"&&(n="cssFloat"),r?e.setProperty(n,a):e[n]=a}}var In=x({menuitem:!0},{area:!0,base:!0,br:!0,col:!0,embed:!0,hr:!0,img:!0,input:!0,keygen:!0,link:!0,meta:!0,param:!0,source:!0,track:!0,wbr:!0});function ea(e,t){if(t){if(In[e]&&(t.children!=null||t.dangerouslySetInnerHTML!=null))throw Error(g(137,e));if(t.dangerouslySetInnerHTML!=null){if(t.children!=null)throw Error(g(60));if(typeof t.dangerouslySetInnerHTML!="object"||!("__html"in t.dangerouslySetInnerHTML))throw Error(g(61))}if(t.style!=null&&typeof t.style!="object")throw Error(g(62))}}function ta(e,t){if(e.indexOf("-")===-1)return typeof t.is=="string";switch(e){case"annotation-xml":case"color-profile":case"font-face":case"font-face-src":case"font-face-uri":case"font-face-format":case"font-face-name":case"missing-glyph":return!1;default:return!0}}var bt=null;function Oa(e){return e=e.target||e.srcElement||window,e.correspondingUseElement&&(e=e.correspondingUseElement),e.nodeType===3?e.parentNode:e}var Ba=null,ar=null,Nn=null;function Pn(e){if(e=Ja(e)){if(typeof Ba!="function")throw Error(g(280));var t=e.stateNode;t&&(t=No(t),Ba(e.stateNode,e.type,t))}}function bo(e){ar?Nn?Nn.push(e):Nn=[e]:ar=e}function St(){if(ar){var e=ar,t=Nn;if(Nn=ar=null,Pn(e),t)for(e=0;e<t.length;e++)Pn(t[e])}}function Fa(e,t){return e(t)}function lt(){}var na=!1;function Ka(e,t,n){if(na)return e(t,n);na=!0;try{return Fa(e,t,n)}finally{na=!1,(ar!==null||Nn!==null)&&(lt(),St())}}function or(e,t){var n=e.stateNode;if(n===null)return null;var r=No(n);if(r===null)return null;n=r[t];e:switch(t){case"onClick":case"onClickCapture":case"onDoubleClick":case"onDoubleClickCapture":case"onMouseDown":case"onMouseDownCapture":case"onMouseMove":case"onMouseMoveCapture":case"onMouseUp":case"onMouseUpCapture":case"onMouseEnter":(r=!r.disabled)||(e=e.type,r=!(e==="button"||e==="input"||e==="select"||e==="textarea")),e=!r;break e;default:e=!1}if(e)return null;if(n&&typeof n!="function")throw Error(g(231,t,typeof n));return n}var ra=!1;if(ye)try{var sr={};Object.defineProperty(sr,"passive",{get:function(){ra=!0}}),window.addEventListener("test",sr,sr),window.removeEventListener("test",sr,sr)}catch{ra=!1}function Nr(e,t,n,r,a,o,i,l,d){var f=Array.prototype.slice.call(arguments,3);try{t.apply(n,f)}catch(_){this.onError(_)}}var Ht=!1,Rn=null,Pr=!1,Rr=null,xr={onError:function(e){Ht=!0,Rn=e}};function aa(e,t,n,r,a,o,i,l,d){Ht=!1,Rn=null,Nr.apply(xr,arguments)}function Mr(e,t,n,r,a,o,i,l,d){if(aa.apply(this,arguments),Ht){if(Ht){var f=Rn;Ht=!1,Rn=null}else throw Error(g(198));Pr||(Pr=!0,Rr=f)}}function dn(e){var t=e,n=e;if(e.alternate)for(;t.return;)t=t.return;else{e=t;do t=e,(t.flags&4098)!==0&&(n=t.return),e=t.return;while(e)}return t.tag===3?n:null}function Dr(e){if(e.tag===13){var t=e.memoizedState;if(t===null&&(e=e.alternate,e!==null&&(t=e.memoizedState)),t!==null)return t.dehydrated}return null}function So(e){if(dn(e)!==e)throw Error(g(188))}function ja(e){var t=e.alternate;if(!t){if(t=dn(e),t===null)throw Error(g(188));return t!==e?null:e}for(var n=e,r=t;;){var a=n.return;if(a===null)break;var o=a.alternate;if(o===null){if(r=a.return,r!==null){n=r;continue}break}if(a.child===o.child){for(o=a.child;o;){if(o===n)return So(a),e;if(o===r)return So(a),t;o=o.sibling}throw Error(g(188))}if(n.return!==r.return)n=a,r=o;else{for(var i=!1,l=a.child;l;){if(l===n){i=!0,n=a,r=o;break}if(l===r){i=!0,r=a,n=o;break}l=l.sibling}if(!i){for(l=o.child;l;){if(l===n){i=!0,n=o,r=a;break}if(l===r){i=!0,r=o,n=a;break}l=l.sibling}if(!i)throw Error(g(189))}}if(n.alternate!==r)throw Error(g(190))}if(n.tag!==3)throw Error(g(188));return n.stateNode.current===n?e:t}function ir(e){return e=ja(e),e!==null?s(e):null}function s(e){if(e.tag===5||e.tag===6)return e;for(e=e.child;e!==null;){var t=s(e);if(t!==null)return t;e=e.sibling}return null}var u=D.unstable_scheduleCallback,v=D.unstable_cancelCallback,w=D.unstable_shouldYield,C=D.unstable_requestPaint,c=D.unstable_now,N=D.unstable_getCurrentPriorityLevel,P=D.unstable_ImmediatePriority,G=D.unstable_UserBlockingPriority,O=D.unstable_NormalPriority,q=D.unstable_LowPriority,F=D.unstable_IdlePriority,K=null,re=null;function Ie(e){if(re&&typeof re.onCommitFiberRoot=="function")try{re.onCommitFiberRoot(K,e,void 0,(e.current.flags&128)===128)}catch{}}var de=Math.clz32?Math.clz32:Ae,Ne=Math.log,be=Math.LN2;function Ae(e){return e>>>=0,e===0?32:31-(Ne(e)/be|0)|0}var Re=64,se=4194304;function U(e){switch(e&-e){case 1:return 1;case 2:return 2;case 4:return 4;case 8:return 8;case 16:return 16;case 32:return 32;case 64:case 128:case 256:case 512:case 1024:case 2048:case 4096:case 8192:case 16384:case 32768:case 65536:case 131072:case 262144:case 524288:case 1048576:case 2097152:return e&4194240;case 4194304:case 8388608:case 16777216:case 33554432:case 67108864:return e&130023424;case 134217728:return 134217728;case 268435456:return 268435456;case 536870912:return 536870912;case 1073741824:return 1073741824;default:return e}}function J(e,t){var n=e.pendingLanes;if(n===0)return 0;var r=0,a=e.suspendedLanes,o=e.pingedLanes,i=n&268435455;if(i!==0){var l=i&~a;l!==0?r=U(l):(o&=i,o!==0&&(r=U(o)))}else i=n&~a,i!==0?r=U(i):o!==0&&(r=U(o));if(r===0)return 0;if(t!==0&&t!==r&&(t&a)===0&&(a=r&-r,o=t&-t,a>=o||a===16&&(o&4194240)!==0))return t;if((r&4)!==0&&(r|=n&16),t=e.entangledLanes,t!==0)for(e=e.entanglements,t&=r;0<t;)n=31-de(t),a=1<<n,r|=e[n],t&=~a;return r}function De(e,t){switch(e){case 1:case 2:case 4:return t+250;case 8:case 16:case 32:case 64:case 128:case 256:case 512:case 1024:case 2048:case 4096:case 8192:case 16384:case 32768:case 65536:case 131072:case 262144:case 524288:case 1048576:case 2097152:return t+5e3;case 4194304:case 8388608:case 16777216:case 33554432:case 67108864:return-1;case 134217728:case 268435456:case 536870912:case 1073741824:return-1;default:return-1}}function ze(e,t){for(var n=e.suspendedLanes,r=e.pingedLanes,a=e.expirationTimes,o=e.pendingLanes;0<o;){var i=31-de(o),l=1<<i,d=a[i];d===-1?((l&n)===0||(l&r)!==0)&&(a[i]=De(l,t)):d<=t&&(e.expiredLanes|=l),o&=~l}}function qe(e){return e=e.pendingLanes&-1073741825,e!==0?e:e&1073741824?1073741824:0}function Oe(){var e=Re;return Re<<=1,(Re&4194240)===0&&(Re=64),e}function Be(e){for(var t=[],n=0;31>n;n++)t.push(e);return t}function Ge(e,t,n){e.pendingLanes|=t,t!==536870912&&(e.suspendedLanes=0,e.pingedLanes=0),e=e.eventTimes,t=31-de(t),e[t]=n}function Ye(e,t){var n=e.pendingLanes&~t;e.pendingLanes=t,e.suspendedLanes=0,e.pingedLanes=0,e.expiredLanes&=t,e.mutableReadLanes&=t,e.entangledLanes&=t,t=e.entanglements;var r=e.eventTimes;for(e=e.expirationTimes;0<n;){var a=31-de(n),o=1<<a;t[a]=0,r[a]=-1,e[a]=-1,n&=~o}}function Ze(e,t){var n=e.entangledLanes|=t;for(e=e.entanglements;n;){var r=31-de(n),a=1<<r;a&t|e[r]&t&&(e[r]|=t),n&=~a}}var me=0;function Ke(e){return e&=-e,1<e?4<e?(e&268435455)!==0?16:536870912:4:1}var mt,Pt,rn,Rt,ct,ot=!1,je=[],xt=null,wt=null,We=null,vn=new Map,_t=new Map,dt=[],Yt="mousedown mouseup touchcancel touchend touchstart auxclick dblclick pointercancel pointerdown pointerup dragend dragstart drop compositionend compositionstart keydown keypress keyup input textInput copy cut paste click change contextmenu reset submit".split(" ");function Gt(e,t){switch(e){case"focusin":case"focusout":xt=null;break;case"dragenter":case"dragleave":wt=null;break;case"mouseover":case"mouseout":We=null;break;case"pointerover":case"pointerout":vn.delete(t.pointerId);break;case"gotpointercapture":case"lostpointercapture":_t.delete(t.pointerId)}}function Mt(e,t,n,r,a,o){return e===null||e.nativeEvent!==o?(e={blockedOn:t,domEventName:n,eventSystemFlags:r,nativeEvent:o,targetContainers:[a]},t!==null&&(t=Ja(t),t!==null&&Pt(t)),e):(e.eventSystemFlags|=r,t=e.targetContainers,a!==null&&t.indexOf(a)===-1&&t.push(a),e)}function bn(e,t,n,r,a){switch(t){case"focusin":return xt=Mt(xt,e,t,n,r,a),!0;case"dragenter":return wt=Mt(wt,e,t,n,r,a),!0;case"mouseover":return We=Mt(We,e,t,n,r,a),!0;case"pointerover":var o=a.pointerId;return vn.set(o,Mt(vn.get(o)||null,e,t,n,r,a)),!0;case"gotpointercapture":return o=a.pointerId,_t.set(o,Mt(_t.get(o)||null,e,t,n,r,a)),!0}return!1}function Wt(e){var t=jr(e.target);if(t!==null){var n=dn(t);if(n!==null){if(t=n.tag,t===13){if(t=Dr(n),t!==null){e.blockedOn=t,ct(e.priority,function(){rn(n)});return}}else if(t===3&&n.stateNode.current.memoizedState.isDehydrated){e.blockedOn=n.tag===3?n.stateNode.containerInfo:null;return}}}e.blockedOn=null}function Ut(e){if(e.blockedOn!==null)return!1;for(var t=e.targetContainers;0<t.length;){var n=lr(e.domEventName,e.eventSystemFlags,t[0],e.nativeEvent);if(n===null){n=e.nativeEvent;var r=new n.constructor(n.type,n);bt=r,n.target.dispatchEvent(r),bt=null}else return t=Ja(n),t!==null&&Pt(t),e.blockedOn=n,!1;t.shift()}return!0}function Wn(e,t,n){Ut(e)&&n.delete(t)}function Ue(){ot=!1,xt!==null&&Ut(xt)&&(xt=null),wt!==null&&Ut(wt)&&(wt=null),We!==null&&Ut(We)&&(We=null),vn.forEach(Wn),_t.forEach(Wn)}function Qt(e,t){e.blockedOn===t&&(e.blockedOn=null,ot||(ot=!0,D.unstable_scheduleCallback(D.unstable_NormalPriority,Ue)))}function Sn(e){function t(a){return Qt(a,e)}if(0<je.length){Qt(je[0],e);for(var n=1;n<je.length;n++){var r=je[n];r.blockedOn===e&&(r.blockedOn=null)}}for(xt!==null&&Qt(xt,e),wt!==null&&Qt(wt,e),We!==null&&Qt(We,e),vn.forEach(t),_t.forEach(t),n=0;n<dt.length;n++)r=dt[n],r.blockedOn===e&&(r.blockedOn=null);for(;0<dt.length&&(n=dt[0],n.blockedOn===null);)Wt(n),n.blockedOn===null&&dt.shift()}var Un=L.ReactCurrentBatchConfig,Qn=!0;function oa(e,t,n,r){var a=me,o=Un.transition;Un.transition=null;try{me=1,Or(e,t,n,r)}finally{me=a,Un.transition=o}}function sa(e,t,n,r){var a=me,o=Un.transition;Un.transition=null;try{me=4,Or(e,t,n,r)}finally{me=a,Un.transition=o}}function Or(e,t,n,r){if(Qn){var a=lr(e,t,n,r);if(a===null)Ps(e,t,r,we,n),Gt(e,r);else if(bn(a,e,t,n,r))r.stopPropagation();else if(Gt(e,r),t&4&&-1<Yt.indexOf(e)){for(;a!==null;){var o=Ja(a);if(o!==null&&mt(o),o=lr(e,t,n,r),o===null&&Ps(e,t,r,we,n),o===a)break;a=o}a!==null&&r.stopPropagation()}else Ps(e,t,r,null,n)}}var we=null;function lr(e,t,n,r){if(we=null,e=Oa(r),e=jr(e),e!==null)if(t=dn(e),t===null)e=null;else if(n=t.tag,n===13){if(e=Dr(t),e!==null)return e;e=null}else if(n===3){if(t.stateNode.current.memoizedState.isDehydrated)return t.tag===3?t.stateNode.containerInfo:null;e=null}else t!==e&&(e=null);return we=e,null}function wo(e){switch(e){case"cancel":case"click":case"close":case"contextmenu":case"copy":case"cut":case"auxclick":case"dblclick":case"dragend":case"dragstart":case"drop":case"focusin":case"focusout":case"input":case"invalid":case"keydown":case"keypress":case"keyup":case"mousedown":case"mouseup":case"paste":case"pause":case"play":case"pointercancel":case"pointerdown":case"pointerup":case"ratechange":case"reset":case"resize":case"seeked":case"submit":case"touchcancel":case"touchend":case"touchstart":case"volumechange":case"change":case"selectionchange":case"textInput":case"compositionstart":case"compositionend":case"compositionupdate":case"beforeblur":case"afterblur":case"beforeinput":case"blur":case"fullscreenchange":case"focus":case"hashchange":case"popstate":case"select":case"selectstart":return 1;case"drag":case"dragenter":case"dragexit":case"dragleave":case"dragover":case"mousemove":case"mouseout":case"mouseover":case"pointermove":case"pointerout":case"pointerover":case"scroll":case"toggle":case"touchmove":case"wheel":case"mouseenter":case"mouseleave":case"pointerenter":case"pointerleave":return 4;case"message":switch(N()){case P:return 1;case G:return 4;case O:case q:return 16;case F:return 536870912;default:return 16}default:return 16}}var wn=null,ia=null,Br=null;function Fr(){if(Br)return Br;var e,t=ia,n=t.length,r,a="value"in wn?wn.value:wn.textContent,o=a.length;for(e=0;e<n&&t[e]===a[e];e++);var i=n-e;for(r=1;r<=i&&t[n-r]===a[o-r];r++);return Br=a.slice(e,1<r?1-r:void 0)}function Kr(e){var t=e.keyCode;return"charCode"in e?(e=e.charCode,e===0&&t===13&&(e=13)):e=t,e===10&&(e=13),32<=e||e===13?e:0}function cr(){return!0}function la(){return!1}function Dt(e){function t(n,r,a,o,i){this._reactName=n,this._targetInst=a,this.type=r,this.nativeEvent=o,this.target=i,this.currentTarget=null;for(var l in e)e.hasOwnProperty(l)&&(n=e[l],this[l]=n?n(o):o[l]);return this.isDefaultPrevented=(o.defaultPrevented!=null?o.defaultPrevented:o.returnValue===!1)?cr:la,this.isPropagationStopped=la,this}return x(t.prototype,{preventDefault:function(){this.defaultPrevented=!0;var n=this.nativeEvent;n&&(n.preventDefault?n.preventDefault():typeof n.returnValue!="unknown"&&(n.returnValue=!1),this.isDefaultPrevented=cr)},stopPropagation:function(){var n=this.nativeEvent;n&&(n.stopPropagation?n.stopPropagation():typeof n.cancelBubble!="unknown"&&(n.cancelBubble=!0),this.isPropagationStopped=cr)},persist:function(){},isPersistent:cr}),t}var zn={eventPhase:0,bubbles:0,cancelable:0,timeStamp:function(e){return e.timeStamp||Date.now()},defaultPrevented:0,isTrusted:0},ca=Dt(zn),qn=x({},zn,{view:0,detail:0}),Cn=Dt(qn),xn,Mn,Dn,Yn=x({},qn,{screenX:0,screenY:0,clientX:0,clientY:0,pageX:0,pageY:0,ctrlKey:0,shiftKey:0,altKey:0,metaKey:0,getModifierState:Ss,button:0,buttons:0,relatedTarget:function(e){return e.relatedTarget===void 0?e.fromElement===e.srcElement?e.toElement:e.fromElement:e.relatedTarget},movementX:function(e){return"movementX"in e?e.movementX:(e!==Dn&&(Dn&&e.type==="mousemove"?(xn=e.screenX-Dn.screenX,Mn=e.screenY-Dn.screenY):Mn=xn=0,Dn=e),xn)},movementY:function(e){return"movementY"in e?e.movementY:Mn}}),dr=Dt(Yn),Co=x({},Yn,{dataTransfer:0}),Ha=Dt(Co),da=x({},qn,{relatedTarget:0}),Ga=Dt(da),bs=x({},zn,{animationName:0,elapsedTime:0,pseudoElement:0}),Cd=Dt(bs),kd=x({},zn,{clipboardData:function(e){return"clipboardData"in e?e.clipboardData:window.clipboardData}}),Ed=Dt(kd),Td=x({},zn,{data:0}),Yi=Dt(Td),_d={Esc:"Escape",Spacebar:" ",Left:"ArrowLeft",Up:"ArrowUp",Right:"ArrowRight",Down:"ArrowDown",Del:"Delete",Win:"OS",Menu:"ContextMenu",Apps:"ContextMenu",Scroll:"ScrollLock",MozPrintableKey:"Unidentified"},Ad={8:"Backspace",9:"Tab",12:"Clear",13:"Enter",16:"Shift",17:"Control",18:"Alt",19:"Pause",20:"CapsLock",27:"Escape",32:" ",33:"PageUp",34:"PageDown",35:"End",36:"Home",37:"ArrowLeft",38:"ArrowUp",39:"ArrowRight",40:"ArrowDown",45:"Insert",46:"Delete",112:"F1",113:"F2",114:"F3",115:"F4",116:"F5",117:"F6",118:"F7",119:"F8",120:"F9",121:"F10",122:"F11",123:"F12",144:"NumLock",145:"ScrollLock",224:"Meta"},Ld={Alt:"altKey",Control:"ctrlKey",Meta:"metaKey",Shift:"shiftKey"};function Id(e){var t=this.nativeEvent;return t.getModifierState?t.getModifierState(e):(e=Ld[e])?!!t[e]:!1}function Ss(){return Id}var Nd=x({},qn,{key:function(e){if(e.key){var t=_d[e.key]||e.key;if(t!=="Unidentified")return t}return e.type==="keypress"?(e=Kr(e),e===13?"Enter":String.fromCharCode(e)):e.type==="keydown"||e.type==="keyup"?Ad[e.keyCode]||"Unidentified":""},code:0,location:0,ctrlKey:0,shiftKey:0,altKey:0,metaKey:0,repeat:0,locale:0,getModifierState:Ss,charCode:function(e){return e.type==="keypress"?Kr(e):0},keyCode:function(e){return e.type==="keydown"||e.type==="keyup"?e.keyCode:0},which:function(e){return e.type==="keypress"?Kr(e):e.type==="keydown"||e.type==="keyup"?e.keyCode:0}}),Pd=Dt(Nd),Rd=x({},Yn,{pointerId:0,width:0,height:0,pressure:0,tangentialPressure:0,tiltX:0,tiltY:0,twist:0,pointerType:0,isPrimary:0}),Vi=Dt(Rd),xd=x({},qn,{touches:0,targetTouches:0,changedTouches:0,altKey:0,metaKey:0,ctrlKey:0,shiftKey:0,getModifierState:Ss}),Md=Dt(xd),Dd=x({},zn,{propertyName:0,elapsedTime:0,pseudoElement:0}),Od=Dt(Dd),Bd=x({},Yn,{deltaX:function(e){return"deltaX"in e?e.deltaX:"wheelDeltaX"in e?-e.wheelDeltaX:0},deltaY:function(e){return"deltaY"in e?e.deltaY:"wheelDeltaY"in e?-e.wheelDeltaY:"wheelDelta"in e?-e.wheelDelta:0},deltaZ:0,deltaMode:0}),Fd=Dt(Bd),Kd=[9,13,27,32],ws=ye&&"CompositionEvent"in window,Wa=null;ye&&"documentMode"in document&&(Wa=document.documentMode);var jd=ye&&"TextEvent"in window&&!Wa,$i=ye&&(!ws||Wa&&8<Wa&&11>=Wa),Xi=" ",Ji=!1;function Zi(e,t){switch(e){case"keyup":return Kd.indexOf(t.keyCode)!==-1;case"keydown":return t.keyCode!==229;case"keypress":case"mousedown":case"focusout":return!0;default:return!1}}function el(e){return e=e.detail,typeof e=="object"&&"data"in e?e.data:null}var pa=!1;function Hd(e,t){switch(e){case"compositionend":return el(t);case"keypress":return t.which!==32?null:(Ji=!0,Xi);case"textInput":return e=t.data,e===Xi&&Ji?null:e;default:return null}}function Gd(e,t){if(pa)return e==="compositionend"||!ws&&Zi(e,t)?(e=Fr(),Br=ia=wn=null,pa=!1,e):null;switch(e){case"paste":return null;case"keypress":if(!(t.ctrlKey||t.altKey||t.metaKey)||t.ctrlKey&&t.altKey){if(t.char&&1<t.char.length)return t.char;if(t.which)return String.fromCharCode(t.which)}return null;case"compositionend":return $i&&t.locale!=="ko"?null:t.data;default:return null}}var Wd={color:!0,date:!0,datetime:!0,"datetime-local":!0,email:!0,month:!0,number:!0,password:!0,range:!0,search:!0,tel:!0,text:!0,time:!0,url:!0,week:!0};function tl(e){var t=e&&e.nodeName&&e.nodeName.toLowerCase();return t==="input"?!!Wd[e.type]:t==="textarea"}function nl(e,t,n,r){bo(r),t=Ao(t,"onChange"),0<t.length&&(n=new ca("onChange","change",null,n,r),e.push({event:n,listeners:t}))}var Ua=null,Qa=null;function Ud(e){Sl(e,0)}function ko(e){var t=ya(e);if(Pa(t))return e}function Qd(e,t){if(e==="change")return t}var rl=!1;if(ye){var Cs;if(ye){var ks="oninput"in document;if(!ks){var al=document.createElement("div");al.setAttribute("oninput","return;"),ks=typeof al.oninput=="function"}Cs=ks}else Cs=!1;rl=Cs&&(!document.documentMode||9<document.documentMode)}function ol(){Ua&&(Ua.detachEvent("onpropertychange",sl),Qa=Ua=null)}function sl(e){if(e.propertyName==="value"&&ko(Qa)){var t=[];nl(t,Qa,e,Oa(e)),Ka(Ud,t)}}function zd(e,t,n){e==="focusin"?(ol(),Ua=t,Qa=n,Ua.attachEvent("onpropertychange",sl)):e==="focusout"&&ol()}function qd(e){if(e==="selectionchange"||e==="keyup"||e==="keydown")return ko(Qa)}function Yd(e,t){if(e==="click")return ko(t)}function Vd(e,t){if(e==="input"||e==="change")return ko(t)}function $d(e,t){return e===t&&(e!==0||1/e===1/t)||e!==e&&t!==t}var kn=typeof Object.is=="function"?Object.is:$d;function za(e,t){if(kn(e,t))return!0;if(typeof e!="object"||e===null||typeof t!="object"||t===null)return!1;var n=Object.keys(e),r=Object.keys(t);if(n.length!==r.length)return!1;for(r=0;r<n.length;r++){var a=n[r];if(!ne.call(t,a)||!kn(e[a],t[a]))return!1}return!0}function il(e){for(;e&&e.firstChild;)e=e.firstChild;return e}function ll(e,t){var n=il(e);e=0;for(var r;n;){if(n.nodeType===3){if(r=e+n.textContent.length,e<=t&&r>=t)return{node:n,offset:t-e};e=r}e:{for(;n;){if(n.nextSibling){n=n.nextSibling;break e}n=n.parentNode}n=void 0}n=il(n)}}function cl(e,t){return e&&t?e===t?!0:e&&e.nodeType===3?!1:t&&t.nodeType===3?cl(e,t.parentNode):"contains"in e?e.contains(t):e.compareDocumentPosition?!!(e.compareDocumentPosition(t)&16):!1:!1}function dl(){for(var e=window,t=Gn();t instanceof e.HTMLIFrameElement;){try{var n=typeof t.contentWindow.location.href=="string"}catch{n=!1}if(n)e=t.contentWindow;else break;t=Gn(e.document)}return t}function Es(e){var t=e&&e.nodeName&&e.nodeName.toLowerCase();return t&&(t==="input"&&(e.type==="text"||e.type==="search"||e.type==="tel"||e.type==="url"||e.type==="password")||t==="textarea"||e.contentEditable==="true")}function Xd(e){var t=dl(),n=e.focusedElem,r=e.selectionRange;if(t!==n&&n&&n.ownerDocument&&cl(n.ownerDocument.documentElement,n)){if(r!==null&&Es(n)){if(t=r.start,e=r.end,e===void 0&&(e=t),"selectionStart"in n)n.selectionStart=t,n.selectionEnd=Math.min(e,n.value.length);else if(e=(t=n.ownerDocument||document)&&t.defaultView||window,e.getSelection){e=e.getSelection();var a=n.textContent.length,o=Math.min(r.start,a);r=r.end===void 0?o:Math.min(r.end,a),!e.extend&&o>r&&(a=r,r=o,o=a),a=ll(n,o);var i=ll(n,r);a&&i&&(e.rangeCount!==1||e.anchorNode!==a.node||e.anchorOffset!==a.offset||e.focusNode!==i.node||e.focusOffset!==i.offset)&&(t=t.createRange(),t.setStart(a.node,a.offset),e.removeAllRanges(),o>r?(e.addRange(t),e.extend(i.node,i.offset)):(t.setEnd(i.node,i.offset),e.addRange(t)))}}for(t=[],e=n;e=e.parentNode;)e.nodeType===1&&t.push({element:e,left:e.scrollLeft,top:e.scrollTop});for(typeof n.focus=="function"&&n.focus(),n=0;n<t.length;n++)e=t[n],e.element.scrollLeft=e.left,e.element.scrollTop=e.top}}var Jd=ye&&"documentMode"in document&&11>=document.documentMode,ua=null,Ts=null,qa=null,_s=!1;function pl(e,t,n){var r=n.window===n?n.document:n.nodeType===9?n:n.ownerDocument;_s||ua==null||ua!==Gn(r)||(r=ua,"selectionStart"in r&&Es(r)?r={start:r.selectionStart,end:r.selectionEnd}:(r=(r.ownerDocument&&r.ownerDocument.defaultView||window).getSelection(),r={anchorNode:r.anchorNode,anchorOffset:r.anchorOffset,focusNode:r.focusNode,focusOffset:r.focusOffset}),qa&&za(qa,r)||(qa=r,r=Ao(Ts,"onSelect"),0<r.length&&(t=new ca("onSelect","select",null,t,n),e.push({event:t,listeners:r}),t.target=ua)))}function Eo(e,t){var n={};return n[e.toLowerCase()]=t.toLowerCase(),n["Webkit"+e]="webkit"+t,n["Moz"+e]="moz"+t,n}var ma={animationend:Eo("Animation","AnimationEnd"),animationiteration:Eo("Animation","AnimationIteration"),animationstart:Eo("Animation","AnimationStart"),transitionend:Eo("Transition","TransitionEnd")},As={},ul={};ye&&(ul=document.createElement("div").style,"AnimationEvent"in window||(delete ma.animationend.animation,delete ma.animationiteration.animation,delete ma.animationstart.animation),"TransitionEvent"in window||delete ma.transitionend.transition);function To(e){if(As[e])return As[e];if(!ma[e])return e;var t=ma[e],n;for(n in t)if(t.hasOwnProperty(n)&&n in ul)return As[e]=t[n];return e}var ml=To("animationend"),hl=To("animationiteration"),gl=To("animationstart"),yl=To("transitionend"),fl=new Map,vl="abort auxClick cancel canPlay canPlayThrough click close contextMenu copy cut drag dragEnd dragEnter dragExit dragLeave dragOver dragStart drop durationChange emptied encrypted ended error gotPointerCapture input invalid keyDown keyPress keyUp load loadedData loadedMetadata loadStart lostPointerCapture mouseDown mouseMove mouseOut mouseOver mouseUp paste pause play playing pointerCancel pointerDown pointerMove pointerOut pointerOver pointerUp progress rateChange reset resize seeked seeking stalled submit suspend timeUpdate touchCancel touchEnd touchStart volumeChange scroll toggle touchMove waiting wheel".split(" ");function pr(e,t){fl.set(e,t),ae(t,[e])}for(var Ls=0;Ls<vl.length;Ls++){var Is=vl[Ls],Zd=Is.toLowerCase(),ep=Is[0].toUpperCase()+Is.slice(1);pr(Zd,"on"+ep)}pr(ml,"onAnimationEnd"),pr(hl,"onAnimationIteration"),pr(gl,"onAnimationStart"),pr("dblclick","onDoubleClick"),pr("focusin","onFocus"),pr("focusout","onBlur"),pr(yl,"onTransitionEnd"),fe("onMouseEnter",["mouseout","mouseover"]),fe("onMouseLeave",["mouseout","mouseover"]),fe("onPointerEnter",["pointerout","pointerover"]),fe("onPointerLeave",["pointerout","pointerover"]),ae("onChange","change click focusin focusout input keydown keyup selectionchange".split(" ")),ae("onSelect","focusout contextmenu dragend focusin keydown keyup mousedown mouseup selectionchange".split(" ")),ae("onBeforeInput",["compositionend","keypress","textInput","paste"]),ae("onCompositionEnd","compositionend focusout keydown keypress keyup mousedown".split(" ")),ae("onCompositionStart","compositionstart focusout keydown keypress keyup mousedown".split(" ")),ae("onCompositionUpdate","compositionupdate focusout keydown keypress keyup mousedown".split(" "));var Ya="abort canplay canplaythrough durationchange emptied encrypted ended error loadeddata loadedmetadata loadstart pause play playing progress ratechange resize seeked seeking stalled suspend timeupdate volumechange waiting".split(" "),tp=new Set("cancel close invalid load scroll toggle".split(" ").concat(Ya));function bl(e,t,n){var r=e.type||"unknown-event";e.currentTarget=n,Mr(r,t,void 0,e),e.currentTarget=null}function Sl(e,t){t=(t&4)!==0;for(var n=0;n<e.length;n++){var r=e[n],a=r.event;r=r.listeners;e:{var o=void 0;if(t)for(var i=r.length-1;0<=i;i--){var l=r[i],d=l.instance,f=l.currentTarget;if(l=l.listener,d!==o&&a.isPropagationStopped())break e;bl(a,l,f),o=d}else for(i=0;i<r.length;i++){if(l=r[i],d=l.instance,f=l.currentTarget,l=l.listener,d!==o&&a.isPropagationStopped())break e;bl(a,l,f),o=d}}}if(Pr)throw e=Rr,Pr=!1,Rr=null,e}function $e(e,t){var n=t[Bs];n===void 0&&(n=t[Bs]=new Set);var r=e+"__bubble";n.has(r)||(wl(t,e,2,!1),n.add(r))}function Ns(e,t,n){var r=0;t&&(r|=4),wl(n,e,r,t)}var _o="_reactListening"+Math.random().toString(36).slice(2);function Va(e){if(!e[_o]){e[_o]=!0,z.forEach(function(n){n!=="selectionchange"&&(tp.has(n)||Ns(n,!1,e),Ns(n,!0,e))});var t=e.nodeType===9?e:e.ownerDocument;t===null||t[_o]||(t[_o]=!0,Ns("selectionchange",!1,t))}}function wl(e,t,n,r){switch(wo(t)){case 1:var a=oa;break;case 4:a=sa;break;default:a=Or}n=a.bind(null,t,n,e),a=void 0,!ra||t!=="touchstart"&&t!=="touchmove"&&t!=="wheel"||(a=!0),r?a!==void 0?e.addEventListener(t,n,{capture:!0,passive:a}):e.addEventListener(t,n,!0):a!==void 0?e.addEventListener(t,n,{passive:a}):e.addEventListener(t,n,!1)}function Ps(e,t,n,r,a){var o=r;if((t&1)===0&&(t&2)===0&&r!==null)e:for(;;){if(r===null)return;var i=r.tag;if(i===3||i===4){var l=r.stateNode.containerInfo;if(l===a||l.nodeType===8&&l.parentNode===a)break;if(i===4)for(i=r.return;i!==null;){var d=i.tag;if((d===3||d===4)&&(d=i.stateNode.containerInfo,d===a||d.nodeType===8&&d.parentNode===a))return;i=i.return}for(;l!==null;){if(i=jr(l),i===null)return;if(d=i.tag,d===5||d===6){r=o=i;continue e}l=l.parentNode}}r=r.return}Ka(function(){var f=o,_=Oa(n),I=[];e:{var E=fl.get(e);if(E!==void 0){var W=ca,Y=e;switch(e){case"keypress":if(Kr(n)===0)break e;case"keydown":case"keyup":W=Pd;break;case"focusin":Y="focus",W=Ga;break;case"focusout":Y="blur",W=Ga;break;case"beforeblur":case"afterblur":W=Ga;break;case"click":if(n.button===2)break e;case"auxclick":case"dblclick":case"mousedown":case"mousemove":case"mouseup":case"mouseout":case"mouseover":case"contextmenu":W=dr;break;case"drag":case"dragend":case"dragenter":case"dragexit":case"dragleave":case"dragover":case"dragstart":case"drop":W=Ha;break;case"touchcancel":case"touchend":case"touchmove":case"touchstart":W=Md;break;case ml:case hl:case gl:W=Cd;break;case yl:W=Od;break;case"scroll":W=Cn;break;case"wheel":W=Fd;break;case"copy":case"cut":case"paste":W=Ed;break;case"gotpointercapture":case"lostpointercapture":case"pointercancel":case"pointerdown":case"pointermove":case"pointerout":case"pointerover":case"pointerup":W=Vi}var V=(t&4)!==0,st=!V&&e==="scroll",h=V?E!==null?E+"Capture":null:E;V=[];for(var p=f,y;p!==null;){y=p;var R=y.stateNode;if(y.tag===5&&R!==null&&(y=R,h!==null&&(R=or(p,h),R!=null&&V.push($a(p,R,y)))),st)break;p=p.return}0<V.length&&(E=new W(E,Y,null,n,_),I.push({event:E,listeners:V}))}}if((t&7)===0){e:{if(E=e==="mouseover"||e==="pointerover",W=e==="mouseout"||e==="pointerout",E&&n!==bt&&(Y=n.relatedTarget||n.fromElement)&&(jr(Y)||Y[Vn]))break e;if((W||E)&&(E=_.window===_?_:(E=_.ownerDocument)?E.defaultView||E.parentWindow:window,W?(Y=n.relatedTarget||n.toElement,W=f,Y=Y?jr(Y):null,Y!==null&&(st=dn(Y),Y!==st||Y.tag!==5&&Y.tag!==6)&&(Y=null)):(W=null,Y=f),W!==Y)){if(V=dr,R="onMouseLeave",h="onMouseEnter",p="mouse",(e==="pointerout"||e==="pointerover")&&(V=Vi,R="onPointerLeave",h="onPointerEnter",p="pointer"),st=W==null?E:ya(W),y=Y==null?E:ya(Y),E=new V(R,p+"leave",W,n,_),E.target=st,E.relatedTarget=y,R=null,jr(_)===f&&(V=new V(h,p+"enter",Y,n,_),V.target=y,V.relatedTarget=st,R=V),st=R,W&&Y)t:{for(V=W,h=Y,p=0,y=V;y;y=ha(y))p++;for(y=0,R=h;R;R=ha(R))y++;for(;0<p-y;)V=ha(V),p--;for(;0<y-p;)h=ha(h),y--;for(;p--;){if(V===h||h!==null&&V===h.alternate)break t;V=ha(V),h=ha(h)}V=null}else V=null;W!==null&&Cl(I,E,W,V,!1),Y!==null&&st!==null&&Cl(I,st,Y,V,!0)}}e:{if(E=f?ya(f):window,W=E.nodeName&&E.nodeName.toLowerCase(),W==="select"||W==="input"&&E.type==="file")var X=Qd;else if(tl(E))if(rl)X=Vd;else{X=qd;var ie=zd}else(W=E.nodeName)&&W.toLowerCase()==="input"&&(E.type==="checkbox"||E.type==="radio")&&(X=Yd);if(X&&(X=X(e,f))){nl(I,X,n,_);break e}ie&&ie(e,E,f),e==="focusout"&&(ie=E._wrapperState)&&ie.controlled&&E.type==="number"&&xa(E,"number",E.value)}switch(ie=f?ya(f):window,e){case"focusin":(tl(ie)||ie.contentEditable==="true")&&(ua=ie,Ts=f,qa=null);break;case"focusout":qa=Ts=ua=null;break;case"mousedown":_s=!0;break;case"contextmenu":case"mouseup":case"dragend":_s=!1,pl(I,n,_);break;case"selectionchange":if(Jd)break;case"keydown":case"keyup":pl(I,n,_)}var le;if(ws)e:{switch(e){case"compositionstart":var he="onCompositionStart";break e;case"compositionend":he="onCompositionEnd";break e;case"compositionupdate":he="onCompositionUpdate";break e}he=void 0}else pa?Zi(e,n)&&(he="onCompositionEnd"):e==="keydown"&&n.keyCode===229&&(he="onCompositionStart");he&&($i&&n.locale!=="ko"&&(pa||he!=="onCompositionStart"?he==="onCompositionEnd"&&pa&&(le=Fr()):(wn=_,ia="value"in wn?wn.value:wn.textContent,pa=!0)),ie=Ao(f,he),0<ie.length&&(he=new Yi(he,e,null,n,_),I.push({event:he,listeners:ie}),le?he.data=le:(le=el(n),le!==null&&(he.data=le)))),(le=jd?Hd(e,n):Gd(e,n))&&(f=Ao(f,"onBeforeInput"),0<f.length&&(_=new Yi("onBeforeInput","beforeinput",null,n,_),I.push({event:_,listeners:f}),_.data=le))}Sl(I,t)})}function $a(e,t,n){return{instance:e,listener:t,currentTarget:n}}function Ao(e,t){for(var n=t+"Capture",r=[];e!==null;){var a=e,o=a.stateNode;a.tag===5&&o!==null&&(a=o,o=or(e,n),o!=null&&r.unshift($a(e,o,a)),o=or(e,t),o!=null&&r.push($a(e,o,a))),e=e.return}return r}function ha(e){if(e===null)return null;do e=e.return;while(e&&e.tag!==5);return e||null}function Cl(e,t,n,r,a){for(var o=t._reactName,i=[];n!==null&&n!==r;){var l=n,d=l.alternate,f=l.stateNode;if(d!==null&&d===r)break;l.tag===5&&f!==null&&(l=f,a?(d=or(n,o),d!=null&&i.unshift($a(n,d,l))):a||(d=or(n,o),d!=null&&i.push($a(n,d,l)))),n=n.return}i.length!==0&&e.push({event:t,listeners:i})}var np=/\r\n?/g,rp=/\u0000|\uFFFD/g;function kl(e){return(typeof e=="string"?e:""+e).replace(np,`
`).replace(rp,"")}function Lo(e,t,n){if(t=kl(t),kl(e)!==t&&n)throw Error(g(425))}function Io(){}var Rs=null,xs=null;function Ms(e,t){return e==="textarea"||e==="noscript"||typeof t.children=="string"||typeof t.children=="number"||typeof t.dangerouslySetInnerHTML=="object"&&t.dangerouslySetInnerHTML!==null&&t.dangerouslySetInnerHTML.__html!=null}var Ds=typeof setTimeout=="function"?setTimeout:void 0,ap=typeof clearTimeout=="function"?clearTimeout:void 0,El=typeof Promise=="function"?Promise:void 0,op=typeof queueMicrotask=="function"?queueMicrotask:typeof El<"u"?function(e){return El.resolve(null).then(e).catch(sp)}:Ds;function sp(e){setTimeout(function(){throw e})}function Os(e,t){var n=t,r=0;do{var a=n.nextSibling;if(e.removeChild(n),a&&a.nodeType===8)if(n=a.data,n==="/$"){if(r===0){e.removeChild(a),Sn(t);return}r--}else n!=="$"&&n!=="$?"&&n!=="$!"||r++;n=a}while(n);Sn(t)}function ur(e){for(;e!=null;e=e.nextSibling){var t=e.nodeType;if(t===1||t===3)break;if(t===8){if(t=e.data,t==="$"||t==="$!"||t==="$?")break;if(t==="/$")return null}}return e}function Tl(e){e=e.previousSibling;for(var t=0;e;){if(e.nodeType===8){var n=e.data;if(n==="$"||n==="$!"||n==="$?"){if(t===0)return e;t--}else n==="/$"&&t++}e=e.previousSibling}return null}var ga=Math.random().toString(36).slice(2),On="__reactFiber$"+ga,Xa="__reactProps$"+ga,Vn="__reactContainer$"+ga,Bs="__reactEvents$"+ga,ip="__reactListeners$"+ga,lp="__reactHandles$"+ga;function jr(e){var t=e[On];if(t)return t;for(var n=e.parentNode;n;){if(t=n[Vn]||n[On]){if(n=t.alternate,t.child!==null||n!==null&&n.child!==null)for(e=Tl(e);e!==null;){if(n=e[On])return n;e=Tl(e)}return t}e=n,n=e.parentNode}return null}function Ja(e){return e=e[On]||e[Vn],!e||e.tag!==5&&e.tag!==6&&e.tag!==13&&e.tag!==3?null:e}function ya(e){if(e.tag===5||e.tag===6)return e.stateNode;throw Error(g(33))}function No(e){return e[Xa]||null}var Fs=[],fa=-1;function mr(e){return{current:e}}function Xe(e){0>fa||(e.current=Fs[fa],Fs[fa]=null,fa--)}function Ve(e,t){fa++,Fs[fa]=e.current,e.current=t}var hr={},Ot=mr(hr),Vt=mr(!1),Hr=hr;function va(e,t){var n=e.type.contextTypes;if(!n)return hr;var r=e.stateNode;if(r&&r.__reactInternalMemoizedUnmaskedChildContext===t)return r.__reactInternalMemoizedMaskedChildContext;var a={},o;for(o in n)a[o]=t[o];return r&&(e=e.stateNode,e.__reactInternalMemoizedUnmaskedChildContext=t,e.__reactInternalMemoizedMaskedChildContext=a),a}function $t(e){return e=e.childContextTypes,e!=null}function Po(){Xe(Vt),Xe(Ot)}function _l(e,t,n){if(Ot.current!==hr)throw Error(g(168));Ve(Ot,t),Ve(Vt,n)}function Al(e,t,n){var r=e.stateNode;if(t=t.childContextTypes,typeof r.getChildContext!="function")return n;r=r.getChildContext();for(var a in r)if(!(a in t))throw Error(g(108,ve(e)||"Unknown",a));return x({},n,r)}function Ro(e){return e=(e=e.stateNode)&&e.__reactInternalMemoizedMergedChildContext||hr,Hr=Ot.current,Ve(Ot,e),Ve(Vt,Vt.current),!0}function Ll(e,t,n){var r=e.stateNode;if(!r)throw Error(g(169));n?(e=Al(e,t,Hr),r.__reactInternalMemoizedMergedChildContext=e,Xe(Vt),Xe(Ot),Ve(Ot,e)):Xe(Vt),Ve(Vt,n)}var $n=null,xo=!1,Ks=!1;function Il(e){$n===null?$n=[e]:$n.push(e)}function cp(e){xo=!0,Il(e)}function gr(){if(!Ks&&$n!==null){Ks=!0;var e=0,t=me;try{var n=$n;for(me=1;e<n.length;e++){var r=n[e];do r=r(!0);while(r!==null)}$n=null,xo=!1}catch(a){throw $n!==null&&($n=$n.slice(e+1)),u(P,gr),a}finally{me=t,Ks=!1}}return null}var ba=[],Sa=0,Mo=null,Do=0,pn=[],un=0,Gr=null,Xn=1,Jn="";function Wr(e,t){ba[Sa++]=Do,ba[Sa++]=Mo,Mo=e,Do=t}function Nl(e,t,n){pn[un++]=Xn,pn[un++]=Jn,pn[un++]=Gr,Gr=e;var r=Xn;e=Jn;var a=32-de(r)-1;r&=~(1<<a),n+=1;var o=32-de(t)+a;if(30<o){var i=a-a%5;o=(r&(1<<i)-1).toString(32),r>>=i,a-=i,Xn=1<<32-de(t)+a|n<<a|r,Jn=o+e}else Xn=1<<o|n<<a|r,Jn=e}function js(e){e.return!==null&&(Wr(e,1),Nl(e,1,0))}function Hs(e){for(;e===Mo;)Mo=ba[--Sa],ba[Sa]=null,Do=ba[--Sa],ba[Sa]=null;for(;e===Gr;)Gr=pn[--un],pn[un]=null,Jn=pn[--un],pn[un]=null,Xn=pn[--un],pn[un]=null}var an=null,on=null,et=!1,En=null;function Pl(e,t){var n=yn(5,null,null,0);n.elementType="DELETED",n.stateNode=t,n.return=e,t=e.deletions,t===null?(e.deletions=[n],e.flags|=16):t.push(n)}function Rl(e,t){switch(e.tag){case 5:var n=e.type;return t=t.nodeType!==1||n.toLowerCase()!==t.nodeName.toLowerCase()?null:t,t!==null?(e.stateNode=t,an=e,on=ur(t.firstChild),!0):!1;case 6:return t=e.pendingProps===""||t.nodeType!==3?null:t,t!==null?(e.stateNode=t,an=e,on=null,!0):!1;case 13:return t=t.nodeType!==8?null:t,t!==null?(n=Gr!==null?{id:Xn,overflow:Jn}:null,e.memoizedState={dehydrated:t,treeContext:n,retryLane:1073741824},n=yn(18,null,null,0),n.stateNode=t,n.return=e,e.child=n,an=e,on=null,!0):!1;default:return!1}}function Gs(e){return(e.mode&1)!==0&&(e.flags&128)===0}function Ws(e){if(et){var t=on;if(t){var n=t;if(!Rl(e,t)){if(Gs(e))throw Error(g(418));t=ur(n.nextSibling);var r=an;t&&Rl(e,t)?Pl(r,n):(e.flags=e.flags&-4097|2,et=!1,an=e)}}else{if(Gs(e))throw Error(g(418));e.flags=e.flags&-4097|2,et=!1,an=e}}}function xl(e){for(e=e.return;e!==null&&e.tag!==5&&e.tag!==3&&e.tag!==13;)e=e.return;an=e}function Oo(e){if(e!==an)return!1;if(!et)return xl(e),et=!0,!1;var t;if((t=e.tag!==3)&&!(t=e.tag!==5)&&(t=e.type,t=t!=="head"&&t!=="body"&&!Ms(e.type,e.memoizedProps)),t&&(t=on)){if(Gs(e))throw Ml(),Error(g(418));for(;t;)Pl(e,t),t=ur(t.nextSibling)}if(xl(e),e.tag===13){if(e=e.memoizedState,e=e!==null?e.dehydrated:null,!e)throw Error(g(317));e:{for(e=e.nextSibling,t=0;e;){if(e.nodeType===8){var n=e.data;if(n==="/$"){if(t===0){on=ur(e.nextSibling);break e}t--}else n!=="$"&&n!=="$!"&&n!=="$?"||t++}e=e.nextSibling}on=null}}else on=an?ur(e.stateNode.nextSibling):null;return!0}function Ml(){for(var e=on;e;)e=ur(e.nextSibling)}function wa(){on=an=null,et=!1}function Us(e){En===null?En=[e]:En.push(e)}var dp=L.ReactCurrentBatchConfig;function Za(e,t,n){if(e=n.ref,e!==null&&typeof e!="function"&&typeof e!="object"){if(n._owner){if(n=n._owner,n){if(n.tag!==1)throw Error(g(309));var r=n.stateNode}if(!r)throw Error(g(147,e));var a=r,o=""+e;return t!==null&&t.ref!==null&&typeof t.ref=="function"&&t.ref._stringRef===o?t.ref:(t=function(i){var l=a.refs;i===null?delete l[o]:l[o]=i},t._stringRef=o,t)}if(typeof e!="string")throw Error(g(284));if(!n._owner)throw Error(g(290,e))}return e}function Bo(e,t){throw e=Object.prototype.toString.call(t),Error(g(31,e==="[object Object]"?"object with keys {"+Object.keys(t).join(", ")+"}":e))}function Dl(e){var t=e._init;return t(e._payload)}function Ol(e){function t(h,p){if(e){var y=h.deletions;y===null?(h.deletions=[p],h.flags|=16):y.push(p)}}function n(h,p){if(!e)return null;for(;p!==null;)t(h,p),p=p.sibling;return null}function r(h,p){for(h=new Map;p!==null;)p.key!==null?h.set(p.key,p):h.set(p.index,p),p=p.sibling;return h}function a(h,p){return h=kr(h,p),h.index=0,h.sibling=null,h}function o(h,p,y){return h.index=y,e?(y=h.alternate,y!==null?(y=y.index,y<p?(h.flags|=2,p):y):(h.flags|=2,p)):(h.flags|=1048576,p)}function i(h){return e&&h.alternate===null&&(h.flags|=2),h}function l(h,p,y,R){return p===null||p.tag!==6?(p=Di(y,h.mode,R),p.return=h,p):(p=a(p,y),p.return=h,p)}function d(h,p,y,R){var X=y.type;return X===Te?_(h,p,y.props.children,R,y.key):p!==null&&(p.elementType===X||typeof X=="object"&&X!==null&&X.$$typeof===tt&&Dl(X)===p.type)?(R=a(p,y.props),R.ref=Za(h,p,y),R.return=h,R):(R=is(y.type,y.key,y.props,null,h.mode,R),R.ref=Za(h,p,y),R.return=h,R)}function f(h,p,y,R){return p===null||p.tag!==4||p.stateNode.containerInfo!==y.containerInfo||p.stateNode.implementation!==y.implementation?(p=Oi(y,h.mode,R),p.return=h,p):(p=a(p,y.children||[]),p.return=h,p)}function _(h,p,y,R,X){return p===null||p.tag!==7?(p=Xr(y,h.mode,R,X),p.return=h,p):(p=a(p,y),p.return=h,p)}function I(h,p,y){if(typeof p=="string"&&p!==""||typeof p=="number")return p=Di(""+p,h.mode,y),p.return=h,p;if(typeof p=="object"&&p!==null){switch(p.$$typeof){case te:return y=is(p.type,p.key,p.props,null,h.mode,y),y.ref=Za(h,null,p),y.return=h,y;case $:return p=Oi(p,h.mode,y),p.return=h,p;case tt:var R=p._init;return I(h,R(p._payload),y)}if(Lr(p)||oe(p))return p=Xr(p,h.mode,y,null),p.return=h,p;Bo(h,p)}return null}function E(h,p,y,R){var X=p!==null?p.key:null;if(typeof y=="string"&&y!==""||typeof y=="number")return X!==null?null:l(h,p,""+y,R);if(typeof y=="object"&&y!==null){switch(y.$$typeof){case te:return y.key===X?d(h,p,y,R):null;case $:return y.key===X?f(h,p,y,R):null;case tt:return X=y._init,E(h,p,X(y._payload),R)}if(Lr(y)||oe(y))return X!==null?null:_(h,p,y,R,null);Bo(h,y)}return null}function W(h,p,y,R,X){if(typeof R=="string"&&R!==""||typeof R=="number")return h=h.get(y)||null,l(p,h,""+R,X);if(typeof R=="object"&&R!==null){switch(R.$$typeof){case te:return h=h.get(R.key===null?y:R.key)||null,d(p,h,R,X);case $:return h=h.get(R.key===null?y:R.key)||null,f(p,h,R,X);case tt:var ie=R._init;return W(h,p,y,ie(R._payload),X)}if(Lr(R)||oe(R))return h=h.get(y)||null,_(p,h,R,X,null);Bo(p,R)}return null}function Y(h,p,y,R){for(var X=null,ie=null,le=p,he=p=0,Et=null;le!==null&&he<y.length;he++){le.index>he?(Et=le,le=null):Et=le.sibling;var xe=E(h,le,y[he],R);if(xe===null){le===null&&(le=Et);break}e&&le&&xe.alternate===null&&t(h,le),p=o(xe,p,he),ie===null?X=xe:ie.sibling=xe,ie=xe,le=Et}if(he===y.length)return n(h,le),et&&Wr(h,he),X;if(le===null){for(;he<y.length;he++)le=I(h,y[he],R),le!==null&&(p=o(le,p,he),ie===null?X=le:ie.sibling=le,ie=le);return et&&Wr(h,he),X}for(le=r(h,le);he<y.length;he++)Et=W(le,h,he,y[he],R),Et!==null&&(e&&Et.alternate!==null&&le.delete(Et.key===null?he:Et.key),p=o(Et,p,he),ie===null?X=Et:ie.sibling=Et,ie=Et);return e&&le.forEach(function(Er){return t(h,Er)}),et&&Wr(h,he),X}function V(h,p,y,R){var X=oe(y);if(typeof X!="function")throw Error(g(150));if(y=X.call(y),y==null)throw Error(g(151));for(var ie=X=null,le=p,he=p=0,Et=null,xe=y.next();le!==null&&!xe.done;he++,xe=y.next()){le.index>he?(Et=le,le=null):Et=le.sibling;var Er=E(h,le,xe.value,R);if(Er===null){le===null&&(le=Et);break}e&&le&&Er.alternate===null&&t(h,le),p=o(Er,p,he),ie===null?X=Er:ie.sibling=Er,ie=Er,le=Et}if(xe.done)return n(h,le),et&&Wr(h,he),X;if(le===null){for(;!xe.done;he++,xe=y.next())xe=I(h,xe.value,R),xe!==null&&(p=o(xe,p,he),ie===null?X=xe:ie.sibling=xe,ie=xe);return et&&Wr(h,he),X}for(le=r(h,le);!xe.done;he++,xe=y.next())xe=W(le,h,he,xe.value,R),xe!==null&&(e&&xe.alternate!==null&&le.delete(xe.key===null?he:xe.key),p=o(xe,p,he),ie===null?X=xe:ie.sibling=xe,ie=xe);return e&&le.forEach(function(Wp){return t(h,Wp)}),et&&Wr(h,he),X}function st(h,p,y,R){if(typeof y=="object"&&y!==null&&y.type===Te&&y.key===null&&(y=y.props.children),typeof y=="object"&&y!==null){switch(y.$$typeof){case te:e:{for(var X=y.key,ie=p;ie!==null;){if(ie.key===X){if(X=y.type,X===Te){if(ie.tag===7){n(h,ie.sibling),p=a(ie,y.props.children),p.return=h,h=p;break e}}else if(ie.elementType===X||typeof X=="object"&&X!==null&&X.$$typeof===tt&&Dl(X)===ie.type){n(h,ie.sibling),p=a(ie,y.props),p.ref=Za(h,ie,y),p.return=h,h=p;break e}n(h,ie);break}else t(h,ie);ie=ie.sibling}y.type===Te?(p=Xr(y.props.children,h.mode,R,y.key),p.return=h,h=p):(R=is(y.type,y.key,y.props,null,h.mode,R),R.ref=Za(h,p,y),R.return=h,h=R)}return i(h);case $:e:{for(ie=y.key;p!==null;){if(p.key===ie)if(p.tag===4&&p.stateNode.containerInfo===y.containerInfo&&p.stateNode.implementation===y.implementation){n(h,p.sibling),p=a(p,y.children||[]),p.return=h,h=p;break e}else{n(h,p);break}else t(h,p);p=p.sibling}p=Oi(y,h.mode,R),p.return=h,h=p}return i(h);case tt:return ie=y._init,st(h,p,ie(y._payload),R)}if(Lr(y))return Y(h,p,y,R);if(oe(y))return V(h,p,y,R);Bo(h,y)}return typeof y=="string"&&y!==""||typeof y=="number"?(y=""+y,p!==null&&p.tag===6?(n(h,p.sibling),p=a(p,y),p.return=h,h=p):(n(h,p),p=Di(y,h.mode,R),p.return=h,h=p),i(h)):n(h,p)}return st}var Ca=Ol(!0),Bl=Ol(!1),Fo=mr(null),Ko=null,ka=null,Qs=null;function zs(){Qs=ka=Ko=null}function qs(e){var t=Fo.current;Xe(Fo),e._currentValue=t}function Ys(e,t,n){for(;e!==null;){var r=e.alternate;if((e.childLanes&t)!==t?(e.childLanes|=t,r!==null&&(r.childLanes|=t)):r!==null&&(r.childLanes&t)!==t&&(r.childLanes|=t),e===n)break;e=e.return}}function Ea(e,t){Ko=e,Qs=ka=null,e=e.dependencies,e!==null&&e.firstContext!==null&&((e.lanes&t)!==0&&(Xt=!0),e.firstContext=null)}function mn(e){var t=e._currentValue;if(Qs!==e)if(e={context:e,memoizedValue:t,next:null},ka===null){if(Ko===null)throw Error(g(308));ka=e,Ko.dependencies={lanes:0,firstContext:e}}else ka=ka.next=e;return t}var Ur=null;function Vs(e){Ur===null?Ur=[e]:Ur.push(e)}function Fl(e,t,n,r){var a=t.interleaved;return a===null?(n.next=n,Vs(t)):(n.next=a.next,a.next=n),t.interleaved=n,Zn(e,r)}function Zn(e,t){e.lanes|=t;var n=e.alternate;for(n!==null&&(n.lanes|=t),n=e,e=e.return;e!==null;)e.childLanes|=t,n=e.alternate,n!==null&&(n.childLanes|=t),n=e,e=e.return;return n.tag===3?n.stateNode:null}var yr=!1;function $s(e){e.updateQueue={baseState:e.memoizedState,firstBaseUpdate:null,lastBaseUpdate:null,shared:{pending:null,interleaved:null,lanes:0},effects:null}}function Kl(e,t){e=e.updateQueue,t.updateQueue===e&&(t.updateQueue={baseState:e.baseState,firstBaseUpdate:e.firstBaseUpdate,lastBaseUpdate:e.lastBaseUpdate,shared:e.shared,effects:e.effects})}function er(e,t){return{eventTime:e,lane:t,tag:0,payload:null,callback:null,next:null}}function fr(e,t,n){var r=e.updateQueue;if(r===null)return null;if(r=r.shared,(Pe&2)!==0){var a=r.pending;return a===null?t.next=t:(t.next=a.next,a.next=t),r.pending=t,Zn(e,n)}return a=r.interleaved,a===null?(t.next=t,Vs(r)):(t.next=a.next,a.next=t),r.interleaved=t,Zn(e,n)}function jo(e,t,n){if(t=t.updateQueue,t!==null&&(t=t.shared,(n&4194240)!==0)){var r=t.lanes;r&=e.pendingLanes,n|=r,t.lanes=n,Ze(e,n)}}function jl(e,t){var n=e.updateQueue,r=e.alternate;if(r!==null&&(r=r.updateQueue,n===r)){var a=null,o=null;if(n=n.firstBaseUpdate,n!==null){do{var i={eventTime:n.eventTime,lane:n.lane,tag:n.tag,payload:n.payload,callback:n.callback,next:null};o===null?a=o=i:o=o.next=i,n=n.next}while(n!==null);o===null?a=o=t:o=o.next=t}else a=o=t;n={baseState:r.baseState,firstBaseUpdate:a,lastBaseUpdate:o,shared:r.shared,effects:r.effects},e.updateQueue=n;return}e=n.lastBaseUpdate,e===null?n.firstBaseUpdate=t:e.next=t,n.lastBaseUpdate=t}function Ho(e,t,n,r){var a=e.updateQueue;yr=!1;var o=a.firstBaseUpdate,i=a.lastBaseUpdate,l=a.shared.pending;if(l!==null){a.shared.pending=null;var d=l,f=d.next;d.next=null,i===null?o=f:i.next=f,i=d;var _=e.alternate;_!==null&&(_=_.updateQueue,l=_.lastBaseUpdate,l!==i&&(l===null?_.firstBaseUpdate=f:l.next=f,_.lastBaseUpdate=d))}if(o!==null){var I=a.baseState;i=0,_=f=d=null,l=o;do{var E=l.lane,W=l.eventTime;if((r&E)===E){_!==null&&(_=_.next={eventTime:W,lane:0,tag:l.tag,payload:l.payload,callback:l.callback,next:null});e:{var Y=e,V=l;switch(E=t,W=n,V.tag){case 1:if(Y=V.payload,typeof Y=="function"){I=Y.call(W,I,E);break e}I=Y;break e;case 3:Y.flags=Y.flags&-65537|128;case 0:if(Y=V.payload,E=typeof Y=="function"?Y.call(W,I,E):Y,E==null)break e;I=x({},I,E);break e;case 2:yr=!0}}l.callback!==null&&l.lane!==0&&(e.flags|=64,E=a.effects,E===null?a.effects=[l]:E.push(l))}else W={eventTime:W,lane:E,tag:l.tag,payload:l.payload,callback:l.callback,next:null},_===null?(f=_=W,d=I):_=_.next=W,i|=E;if(l=l.next,l===null){if(l=a.shared.pending,l===null)break;E=l,l=E.next,E.next=null,a.lastBaseUpdate=E,a.shared.pending=null}}while(!0);if(_===null&&(d=I),a.baseState=d,a.firstBaseUpdate=f,a.lastBaseUpdate=_,t=a.shared.interleaved,t!==null){a=t;do i|=a.lane,a=a.next;while(a!==t)}else o===null&&(a.shared.lanes=0);qr|=i,e.lanes=i,e.memoizedState=I}}function Hl(e,t,n){if(e=t.effects,t.effects=null,e!==null)for(t=0;t<e.length;t++){var r=e[t],a=r.callback;if(a!==null){if(r.callback=null,r=n,typeof a!="function")throw Error(g(191,a));a.call(r)}}}var eo={},Bn=mr(eo),to=mr(eo),no=mr(eo);function Qr(e){if(e===eo)throw Error(g(174));return e}function Xs(e,t){switch(Ve(no,t),Ve(to,e),Ve(Bn,eo),e=t.nodeType,e){case 9:case 11:t=(t=t.documentElement)?t.namespaceURI:nn(null,"");break;default:e=e===8?t.parentNode:t,t=e.namespaceURI||null,e=e.tagName,t=nn(t,e)}Xe(Bn),Ve(Bn,t)}function Ta(){Xe(Bn),Xe(to),Xe(no)}function Gl(e){Qr(no.current);var t=Qr(Bn.current),n=nn(t,e.type);t!==n&&(Ve(to,e),Ve(Bn,n))}function Js(e){to.current===e&&(Xe(Bn),Xe(to))}var nt=mr(0);function Go(e){for(var t=e;t!==null;){if(t.tag===13){var n=t.memoizedState;if(n!==null&&(n=n.dehydrated,n===null||n.data==="$?"||n.data==="$!"))return t}else if(t.tag===19&&t.memoizedProps.revealOrder!==void 0){if((t.flags&128)!==0)return t}else if(t.child!==null){t.child.return=t,t=t.child;continue}if(t===e)break;for(;t.sibling===null;){if(t.return===null||t.return===e)return null;t=t.return}t.sibling.return=t.return,t=t.sibling}return null}var Zs=[];function ei(){for(var e=0;e<Zs.length;e++)Zs[e]._workInProgressVersionPrimary=null;Zs.length=0}var Wo=L.ReactCurrentDispatcher,ti=L.ReactCurrentBatchConfig,zr=0,rt=null,ht=null,Ct=null,Uo=!1,ro=!1,ao=0,pp=0;function Bt(){throw Error(g(321))}function ni(e,t){if(t===null)return!1;for(var n=0;n<t.length&&n<e.length;n++)if(!kn(e[n],t[n]))return!1;return!0}function ri(e,t,n,r,a,o){if(zr=o,rt=t,t.memoizedState=null,t.updateQueue=null,t.lanes=0,Wo.current=e===null||e.memoizedState===null?gp:yp,e=n(r,a),ro){o=0;do{if(ro=!1,ao=0,25<=o)throw Error(g(301));o+=1,Ct=ht=null,t.updateQueue=null,Wo.current=fp,e=n(r,a)}while(ro)}if(Wo.current=qo,t=ht!==null&&ht.next!==null,zr=0,Ct=ht=rt=null,Uo=!1,t)throw Error(g(300));return e}function ai(){var e=ao!==0;return ao=0,e}function Fn(){var e={memoizedState:null,baseState:null,baseQueue:null,queue:null,next:null};return Ct===null?rt.memoizedState=Ct=e:Ct=Ct.next=e,Ct}function hn(){if(ht===null){var e=rt.alternate;e=e!==null?e.memoizedState:null}else e=ht.next;var t=Ct===null?rt.memoizedState:Ct.next;if(t!==null)Ct=t,ht=e;else{if(e===null)throw Error(g(310));ht=e,e={memoizedState:ht.memoizedState,baseState:ht.baseState,baseQueue:ht.baseQueue,queue:ht.queue,next:null},Ct===null?rt.memoizedState=Ct=e:Ct=Ct.next=e}return Ct}function oo(e,t){return typeof t=="function"?t(e):t}function oi(e){var t=hn(),n=t.queue;if(n===null)throw Error(g(311));n.lastRenderedReducer=e;var r=ht,a=r.baseQueue,o=n.pending;if(o!==null){if(a!==null){var i=a.next;a.next=o.next,o.next=i}r.baseQueue=a=o,n.pending=null}if(a!==null){o=a.next,r=r.baseState;var l=i=null,d=null,f=o;do{var _=f.lane;if((zr&_)===_)d!==null&&(d=d.next={lane:0,action:f.action,hasEagerState:f.hasEagerState,eagerState:f.eagerState,next:null}),r=f.hasEagerState?f.eagerState:e(r,f.action);else{var I={lane:_,action:f.action,hasEagerState:f.hasEagerState,eagerState:f.eagerState,next:null};d===null?(l=d=I,i=r):d=d.next=I,rt.lanes|=_,qr|=_}f=f.next}while(f!==null&&f!==o);d===null?i=r:d.next=l,kn(r,t.memoizedState)||(Xt=!0),t.memoizedState=r,t.baseState=i,t.baseQueue=d,n.lastRenderedState=r}if(e=n.interleaved,e!==null){a=e;do o=a.lane,rt.lanes|=o,qr|=o,a=a.next;while(a!==e)}else a===null&&(n.lanes=0);return[t.memoizedState,n.dispatch]}function si(e){var t=hn(),n=t.queue;if(n===null)throw Error(g(311));n.lastRenderedReducer=e;var r=n.dispatch,a=n.pending,o=t.memoizedState;if(a!==null){n.pending=null;var i=a=a.next;do o=e(o,i.action),i=i.next;while(i!==a);kn(o,t.memoizedState)||(Xt=!0),t.memoizedState=o,t.baseQueue===null&&(t.baseState=o),n.lastRenderedState=o}return[o,r]}function Wl(){}function Ul(e,t){var n=rt,r=hn(),a=t(),o=!kn(r.memoizedState,a);if(o&&(r.memoizedState=a,Xt=!0),r=r.queue,ii(ql.bind(null,n,r,e),[e]),r.getSnapshot!==t||o||Ct!==null&&Ct.memoizedState.tag&1){if(n.flags|=2048,so(9,zl.bind(null,n,r,a,t),void 0,null),kt===null)throw Error(g(349));(zr&30)!==0||Ql(n,t,a)}return a}function Ql(e,t,n){e.flags|=16384,e={getSnapshot:t,value:n},t=rt.updateQueue,t===null?(t={lastEffect:null,stores:null},rt.updateQueue=t,t.stores=[e]):(n=t.stores,n===null?t.stores=[e]:n.push(e))}function zl(e,t,n,r){t.value=n,t.getSnapshot=r,Yl(t)&&Vl(e)}function ql(e,t,n){return n(function(){Yl(t)&&Vl(e)})}function Yl(e){var t=e.getSnapshot;e=e.value;try{var n=t();return!kn(e,n)}catch{return!0}}function Vl(e){var t=Zn(e,1);t!==null&&Ln(t,e,1,-1)}function $l(e){var t=Fn();return typeof e=="function"&&(e=e()),t.memoizedState=t.baseState=e,e={pending:null,interleaved:null,lanes:0,dispatch:null,lastRenderedReducer:oo,lastRenderedState:e},t.queue=e,e=e.dispatch=hp.bind(null,rt,e),[t.memoizedState,e]}function so(e,t,n,r){return e={tag:e,create:t,destroy:n,deps:r,next:null},t=rt.updateQueue,t===null?(t={lastEffect:null,stores:null},rt.updateQueue=t,t.lastEffect=e.next=e):(n=t.lastEffect,n===null?t.lastEffect=e.next=e:(r=n.next,n.next=e,e.next=r,t.lastEffect=e)),e}function Xl(){return hn().memoizedState}function Qo(e,t,n,r){var a=Fn();rt.flags|=e,a.memoizedState=so(1|t,n,void 0,r===void 0?null:r)}function zo(e,t,n,r){var a=hn();r=r===void 0?null:r;var o=void 0;if(ht!==null){var i=ht.memoizedState;if(o=i.destroy,r!==null&&ni(r,i.deps)){a.memoizedState=so(t,n,o,r);return}}rt.flags|=e,a.memoizedState=so(1|t,n,o,r)}function Jl(e,t){return Qo(8390656,8,e,t)}function ii(e,t){return zo(2048,8,e,t)}function Zl(e,t){return zo(4,2,e,t)}function ec(e,t){return zo(4,4,e,t)}function tc(e,t){if(typeof t=="function")return e=e(),t(e),function(){t(null)};if(t!=null)return e=e(),t.current=e,function(){t.current=null}}function nc(e,t,n){return n=n!=null?n.concat([e]):null,zo(4,4,tc.bind(null,t,e),n)}function li(){}function rc(e,t){var n=hn();t=t===void 0?null:t;var r=n.memoizedState;return r!==null&&t!==null&&ni(t,r[1])?r[0]:(n.memoizedState=[e,t],e)}function ac(e,t){var n=hn();t=t===void 0?null:t;var r=n.memoizedState;return r!==null&&t!==null&&ni(t,r[1])?r[0]:(e=e(),n.memoizedState=[e,t],e)}function oc(e,t,n){return(zr&21)===0?(e.baseState&&(e.baseState=!1,Xt=!0),e.memoizedState=n):(kn(n,t)||(n=Oe(),rt.lanes|=n,qr|=n,e.baseState=!0),t)}function up(e,t){var n=me;me=n!==0&&4>n?n:4,e(!0);var r=ti.transition;ti.transition={};try{e(!1),t()}finally{me=n,ti.transition=r}}function sc(){return hn().memoizedState}function mp(e,t,n){var r=wr(e);if(n={lane:r,action:n,hasEagerState:!1,eagerState:null,next:null},ic(e))lc(t,n);else if(n=Fl(e,t,n,r),n!==null){var a=qt();Ln(n,e,r,a),cc(n,t,r)}}function hp(e,t,n){var r=wr(e),a={lane:r,action:n,hasEagerState:!1,eagerState:null,next:null};if(ic(e))lc(t,a);else{var o=e.alternate;if(e.lanes===0&&(o===null||o.lanes===0)&&(o=t.lastRenderedReducer,o!==null))try{var i=t.lastRenderedState,l=o(i,n);if(a.hasEagerState=!0,a.eagerState=l,kn(l,i)){var d=t.interleaved;d===null?(a.next=a,Vs(t)):(a.next=d.next,d.next=a),t.interleaved=a;return}}catch{}finally{}n=Fl(e,t,a,r),n!==null&&(a=qt(),Ln(n,e,r,a),cc(n,t,r))}}function ic(e){var t=e.alternate;return e===rt||t!==null&&t===rt}function lc(e,t){ro=Uo=!0;var n=e.pending;n===null?t.next=t:(t.next=n.next,n.next=t),e.pending=t}function cc(e,t,n){if((n&4194240)!==0){var r=t.lanes;r&=e.pendingLanes,n|=r,t.lanes=n,Ze(e,n)}}var qo={readContext:mn,useCallback:Bt,useContext:Bt,useEffect:Bt,useImperativeHandle:Bt,useInsertionEffect:Bt,useLayoutEffect:Bt,useMemo:Bt,useReducer:Bt,useRef:Bt,useState:Bt,useDebugValue:Bt,useDeferredValue:Bt,useTransition:Bt,useMutableSource:Bt,useSyncExternalStore:Bt,useId:Bt,unstable_isNewReconciler:!1},gp={readContext:mn,useCallback:function(e,t){return Fn().memoizedState=[e,t===void 0?null:t],e},useContext:mn,useEffect:Jl,useImperativeHandle:function(e,t,n){return n=n!=null?n.concat([e]):null,Qo(4194308,4,tc.bind(null,t,e),n)},useLayoutEffect:function(e,t){return Qo(4194308,4,e,t)},useInsertionEffect:function(e,t){return Qo(4,2,e,t)},useMemo:function(e,t){var n=Fn();return t=t===void 0?null:t,e=e(),n.memoizedState=[e,t],e},useReducer:function(e,t,n){var r=Fn();return t=n!==void 0?n(t):t,r.memoizedState=r.baseState=t,e={pending:null,interleaved:null,lanes:0,dispatch:null,lastRenderedReducer:e,lastRenderedState:t},r.queue=e,e=e.dispatch=mp.bind(null,rt,e),[r.memoizedState,e]},useRef:function(e){var t=Fn();return e={current:e},t.memoizedState=e},useState:$l,useDebugValue:li,useDeferredValue:function(e){return Fn().memoizedState=e},useTransition:function(){var e=$l(!1),t=e[0];return e=up.bind(null,e[1]),Fn().memoizedState=e,[t,e]},useMutableSource:function(){},useSyncExternalStore:function(e,t,n){var r=rt,a=Fn();if(et){if(n===void 0)throw Error(g(407));n=n()}else{if(n=t(),kt===null)throw Error(g(349));(zr&30)!==0||Ql(r,t,n)}a.memoizedState=n;var o={value:n,getSnapshot:t};return a.queue=o,Jl(ql.bind(null,r,o,e),[e]),r.flags|=2048,so(9,zl.bind(null,r,o,n,t),void 0,null),n},useId:function(){var e=Fn(),t=kt.identifierPrefix;if(et){var n=Jn,r=Xn;n=(r&~(1<<32-de(r)-1)).toString(32)+n,t=":"+t+"R"+n,n=ao++,0<n&&(t+="H"+n.toString(32)),t+=":"}else n=pp++,t=":"+t+"r"+n.toString(32)+":";return e.memoizedState=t},unstable_isNewReconciler:!1},yp={readContext:mn,useCallback:rc,useContext:mn,useEffect:ii,useImperativeHandle:nc,useInsertionEffect:Zl,useLayoutEffect:ec,useMemo:ac,useReducer:oi,useRef:Xl,useState:function(){return oi(oo)},useDebugValue:li,useDeferredValue:function(e){var t=hn();return oc(t,ht.memoizedState,e)},useTransition:function(){var e=oi(oo)[0],t=hn().memoizedState;return[e,t]},useMutableSource:Wl,useSyncExternalStore:Ul,useId:sc,unstable_isNewReconciler:!1},fp={readContext:mn,useCallback:rc,useContext:mn,useEffect:ii,useImperativeHandle:nc,useInsertionEffect:Zl,useLayoutEffect:ec,useMemo:ac,useReducer:si,useRef:Xl,useState:function(){return si(oo)},useDebugValue:li,useDeferredValue:function(e){var t=hn();return ht===null?t.memoizedState=e:oc(t,ht.memoizedState,e)},useTransition:function(){var e=si(oo)[0],t=hn().memoizedState;return[e,t]},useMutableSource:Wl,useSyncExternalStore:Ul,useId:sc,unstable_isNewReconciler:!1};function Tn(e,t){if(e&&e.defaultProps){t=x({},t),e=e.defaultProps;for(var n in e)t[n]===void 0&&(t[n]=e[n]);return t}return t}function ci(e,t,n,r){t=e.memoizedState,n=n(r,t),n=n==null?t:x({},t,n),e.memoizedState=n,e.lanes===0&&(e.updateQueue.baseState=n)}var Yo={isMounted:function(e){return(e=e._reactInternals)?dn(e)===e:!1},enqueueSetState:function(e,t,n){e=e._reactInternals;var r=qt(),a=wr(e),o=er(r,a);o.payload=t,n!=null&&(o.callback=n),t=fr(e,o,a),t!==null&&(Ln(t,e,a,r),jo(t,e,a))},enqueueReplaceState:function(e,t,n){e=e._reactInternals;var r=qt(),a=wr(e),o=er(r,a);o.tag=1,o.payload=t,n!=null&&(o.callback=n),t=fr(e,o,a),t!==null&&(Ln(t,e,a,r),jo(t,e,a))},enqueueForceUpdate:function(e,t){e=e._reactInternals;var n=qt(),r=wr(e),a=er(n,r);a.tag=2,t!=null&&(a.callback=t),t=fr(e,a,r),t!==null&&(Ln(t,e,r,n),jo(t,e,r))}};function dc(e,t,n,r,a,o,i){return e=e.stateNode,typeof e.shouldComponentUpdate=="function"?e.shouldComponentUpdate(r,o,i):t.prototype&&t.prototype.isPureReactComponent?!za(n,r)||!za(a,o):!0}function pc(e,t,n){var r=!1,a=hr,o=t.contextType;return typeof o=="object"&&o!==null?o=mn(o):(a=$t(t)?Hr:Ot.current,r=t.contextTypes,o=(r=r!=null)?va(e,a):hr),t=new t(n,o),e.memoizedState=t.state!==null&&t.state!==void 0?t.state:null,t.updater=Yo,e.stateNode=t,t._reactInternals=e,r&&(e=e.stateNode,e.__reactInternalMemoizedUnmaskedChildContext=a,e.__reactInternalMemoizedMaskedChildContext=o),t}function uc(e,t,n,r){e=t.state,typeof t.componentWillReceiveProps=="function"&&t.componentWillReceiveProps(n,r),typeof t.UNSAFE_componentWillReceiveProps=="function"&&t.UNSAFE_componentWillReceiveProps(n,r),t.state!==e&&Yo.enqueueReplaceState(t,t.state,null)}function di(e,t,n,r){var a=e.stateNode;a.props=n,a.state=e.memoizedState,a.refs={},$s(e);var o=t.contextType;typeof o=="object"&&o!==null?a.context=mn(o):(o=$t(t)?Hr:Ot.current,a.context=va(e,o)),a.state=e.memoizedState,o=t.getDerivedStateFromProps,typeof o=="function"&&(ci(e,t,o,n),a.state=e.memoizedState),typeof t.getDerivedStateFromProps=="function"||typeof a.getSnapshotBeforeUpdate=="function"||typeof a.UNSAFE_componentWillMount!="function"&&typeof a.componentWillMount!="function"||(t=a.state,typeof a.componentWillMount=="function"&&a.componentWillMount(),typeof a.UNSAFE_componentWillMount=="function"&&a.UNSAFE_componentWillMount(),t!==a.state&&Yo.enqueueReplaceState(a,a.state,null),Ho(e,n,a,r),a.state=e.memoizedState),typeof a.componentDidMount=="function"&&(e.flags|=4194308)}function _a(e,t){try{var n="",r=t;do n+=ee(r),r=r.return;while(r);var a=n}catch(o){a=`
Error generating stack: `+o.message+`
`+o.stack}return{value:e,source:t,stack:a,digest:null}}function pi(e,t,n){return{value:e,source:null,stack:n??null,digest:t??null}}function ui(e,t){try{console.error(t.value)}catch(n){setTimeout(function(){throw n})}}var vp=typeof WeakMap=="function"?WeakMap:Map;function mc(e,t,n){n=er(-1,n),n.tag=3,n.payload={element:null};var r=t.value;return n.callback=function(){ts||(ts=!0,Ai=r),ui(e,t)},n}function hc(e,t,n){n=er(-1,n),n.tag=3;var r=e.type.getDerivedStateFromError;if(typeof r=="function"){var a=t.value;n.payload=function(){return r(a)},n.callback=function(){ui(e,t)}}var o=e.stateNode;return o!==null&&typeof o.componentDidCatch=="function"&&(n.callback=function(){ui(e,t),typeof r!="function"&&(br===null?br=new Set([this]):br.add(this));var i=t.stack;this.componentDidCatch(t.value,{componentStack:i!==null?i:""})}),n}function gc(e,t,n){var r=e.pingCache;if(r===null){r=e.pingCache=new vp;var a=new Set;r.set(t,a)}else a=r.get(t),a===void 0&&(a=new Set,r.set(t,a));a.has(n)||(a.add(n),e=Rp.bind(null,e,t,n),t.then(e,e))}function yc(e){do{var t;if((t=e.tag===13)&&(t=e.memoizedState,t=t!==null?t.dehydrated!==null:!0),t)return e;e=e.return}while(e!==null);return null}function fc(e,t,n,r,a){return(e.mode&1)===0?(e===t?e.flags|=65536:(e.flags|=128,n.flags|=131072,n.flags&=-52805,n.tag===1&&(n.alternate===null?n.tag=17:(t=er(-1,1),t.tag=2,fr(n,t,1))),n.lanes|=1),e):(e.flags|=65536,e.lanes=a,e)}var bp=L.ReactCurrentOwner,Xt=!1;function zt(e,t,n,r){t.child=e===null?Bl(t,null,n,r):Ca(t,e.child,n,r)}function vc(e,t,n,r,a){n=n.render;var o=t.ref;return Ea(t,a),r=ri(e,t,n,r,o,a),n=ai(),e!==null&&!Xt?(t.updateQueue=e.updateQueue,t.flags&=-2053,e.lanes&=~a,tr(e,t,a)):(et&&n&&js(t),t.flags|=1,zt(e,t,r,a),t.child)}function bc(e,t,n,r,a){if(e===null){var o=n.type;return typeof o=="function"&&!Mi(o)&&o.defaultProps===void 0&&n.compare===null&&n.defaultProps===void 0?(t.tag=15,t.type=o,Sc(e,t,o,r,a)):(e=is(n.type,null,r,t,t.mode,a),e.ref=t.ref,e.return=t,t.child=e)}if(o=e.child,(e.lanes&a)===0){var i=o.memoizedProps;if(n=n.compare,n=n!==null?n:za,n(i,r)&&e.ref===t.ref)return tr(e,t,a)}return t.flags|=1,e=kr(o,r),e.ref=t.ref,e.return=t,t.child=e}function Sc(e,t,n,r,a){if(e!==null){var o=e.memoizedProps;if(za(o,r)&&e.ref===t.ref)if(Xt=!1,t.pendingProps=r=o,(e.lanes&a)!==0)(e.flags&131072)!==0&&(Xt=!0);else return t.lanes=e.lanes,tr(e,t,a)}return mi(e,t,n,r,a)}function wc(e,t,n){var r=t.pendingProps,a=r.children,o=e!==null?e.memoizedState:null;if(r.mode==="hidden")if((t.mode&1)===0)t.memoizedState={baseLanes:0,cachePool:null,transitions:null},Ve(La,sn),sn|=n;else{if((n&1073741824)===0)return e=o!==null?o.baseLanes|n:n,t.lanes=t.childLanes=1073741824,t.memoizedState={baseLanes:e,cachePool:null,transitions:null},t.updateQueue=null,Ve(La,sn),sn|=e,null;t.memoizedState={baseLanes:0,cachePool:null,transitions:null},r=o!==null?o.baseLanes:n,Ve(La,sn),sn|=r}else o!==null?(r=o.baseLanes|n,t.memoizedState=null):r=n,Ve(La,sn),sn|=r;return zt(e,t,a,n),t.child}function Cc(e,t){var n=t.ref;(e===null&&n!==null||e!==null&&e.ref!==n)&&(t.flags|=512,t.flags|=2097152)}function mi(e,t,n,r,a){var o=$t(n)?Hr:Ot.current;return o=va(t,o),Ea(t,a),n=ri(e,t,n,r,o,a),r=ai(),e!==null&&!Xt?(t.updateQueue=e.updateQueue,t.flags&=-2053,e.lanes&=~a,tr(e,t,a)):(et&&r&&js(t),t.flags|=1,zt(e,t,n,a),t.child)}function kc(e,t,n,r,a){if($t(n)){var o=!0;Ro(t)}else o=!1;if(Ea(t,a),t.stateNode===null)$o(e,t),pc(t,n,r),di(t,n,r,a),r=!0;else if(e===null){var i=t.stateNode,l=t.memoizedProps;i.props=l;var d=i.context,f=n.contextType;typeof f=="object"&&f!==null?f=mn(f):(f=$t(n)?Hr:Ot.current,f=va(t,f));var _=n.getDerivedStateFromProps,I=typeof _=="function"||typeof i.getSnapshotBeforeUpdate=="function";I||typeof i.UNSAFE_componentWillReceiveProps!="function"&&typeof i.componentWillReceiveProps!="function"||(l!==r||d!==f)&&uc(t,i,r,f),yr=!1;var E=t.memoizedState;i.state=E,Ho(t,r,i,a),d=t.memoizedState,l!==r||E!==d||Vt.current||yr?(typeof _=="function"&&(ci(t,n,_,r),d=t.memoizedState),(l=yr||dc(t,n,l,r,E,d,f))?(I||typeof i.UNSAFE_componentWillMount!="function"&&typeof i.componentWillMount!="function"||(typeof i.componentWillMount=="function"&&i.componentWillMount(),typeof i.UNSAFE_componentWillMount=="function"&&i.UNSAFE_componentWillMount()),typeof i.componentDidMount=="function"&&(t.flags|=4194308)):(typeof i.componentDidMount=="function"&&(t.flags|=4194308),t.memoizedProps=r,t.memoizedState=d),i.props=r,i.state=d,i.context=f,r=l):(typeof i.componentDidMount=="function"&&(t.flags|=4194308),r=!1)}else{i=t.stateNode,Kl(e,t),l=t.memoizedProps,f=t.type===t.elementType?l:Tn(t.type,l),i.props=f,I=t.pendingProps,E=i.context,d=n.contextType,typeof d=="object"&&d!==null?d=mn(d):(d=$t(n)?Hr:Ot.current,d=va(t,d));var W=n.getDerivedStateFromProps;(_=typeof W=="function"||typeof i.getSnapshotBeforeUpdate=="function")||typeof i.UNSAFE_componentWillReceiveProps!="function"&&typeof i.componentWillReceiveProps!="function"||(l!==I||E!==d)&&uc(t,i,r,d),yr=!1,E=t.memoizedState,i.state=E,Ho(t,r,i,a);var Y=t.memoizedState;l!==I||E!==Y||Vt.current||yr?(typeof W=="function"&&(ci(t,n,W,r),Y=t.memoizedState),(f=yr||dc(t,n,f,r,E,Y,d)||!1)?(_||typeof i.UNSAFE_componentWillUpdate!="function"&&typeof i.componentWillUpdate!="function"||(typeof i.componentWillUpdate=="function"&&i.componentWillUpdate(r,Y,d),typeof i.UNSAFE_componentWillUpdate=="function"&&i.UNSAFE_componentWillUpdate(r,Y,d)),typeof i.componentDidUpdate=="function"&&(t.flags|=4),typeof i.getSnapshotBeforeUpdate=="function"&&(t.flags|=1024)):(typeof i.componentDidUpdate!="function"||l===e.memoizedProps&&E===e.memoizedState||(t.flags|=4),typeof i.getSnapshotBeforeUpdate!="function"||l===e.memoizedProps&&E===e.memoizedState||(t.flags|=1024),t.memoizedProps=r,t.memoizedState=Y),i.props=r,i.state=Y,i.context=d,r=f):(typeof i.componentDidUpdate!="function"||l===e.memoizedProps&&E===e.memoizedState||(t.flags|=4),typeof i.getSnapshotBeforeUpdate!="function"||l===e.memoizedProps&&E===e.memoizedState||(t.flags|=1024),r=!1)}return hi(e,t,n,r,o,a)}function hi(e,t,n,r,a,o){Cc(e,t);var i=(t.flags&128)!==0;if(!r&&!i)return a&&Ll(t,n,!1),tr(e,t,o);r=t.stateNode,bp.current=t;var l=i&&typeof n.getDerivedStateFromError!="function"?null:r.render();return t.flags|=1,e!==null&&i?(t.child=Ca(t,e.child,null,o),t.child=Ca(t,null,l,o)):zt(e,t,l,o),t.memoizedState=r.state,a&&Ll(t,n,!0),t.child}function Ec(e){var t=e.stateNode;t.pendingContext?_l(e,t.pendingContext,t.pendingContext!==t.context):t.context&&_l(e,t.context,!1),Xs(e,t.containerInfo)}function Tc(e,t,n,r,a){return wa(),Us(a),t.flags|=256,zt(e,t,n,r),t.child}var gi={dehydrated:null,treeContext:null,retryLane:0};function yi(e){return{baseLanes:e,cachePool:null,transitions:null}}function _c(e,t,n){var r=t.pendingProps,a=nt.current,o=!1,i=(t.flags&128)!==0,l;if((l=i)||(l=e!==null&&e.memoizedState===null?!1:(a&2)!==0),l?(o=!0,t.flags&=-129):(e===null||e.memoizedState!==null)&&(a|=1),Ve(nt,a&1),e===null)return Ws(t),e=t.memoizedState,e!==null&&(e=e.dehydrated,e!==null)?((t.mode&1)===0?t.lanes=1:e.data==="$!"?t.lanes=8:t.lanes=1073741824,null):(i=r.children,e=r.fallback,o?(r=t.mode,o=t.child,i={mode:"hidden",children:i},(r&1)===0&&o!==null?(o.childLanes=0,o.pendingProps=i):o=ls(i,r,0,null),e=Xr(e,r,n,null),o.return=t,e.return=t,o.sibling=e,t.child=o,t.child.memoizedState=yi(n),t.memoizedState=gi,e):fi(t,i));if(a=e.memoizedState,a!==null&&(l=a.dehydrated,l!==null))return Sp(e,t,i,r,l,a,n);if(o){o=r.fallback,i=t.mode,a=e.child,l=a.sibling;var d={mode:"hidden",children:r.children};return(i&1)===0&&t.child!==a?(r=t.child,r.childLanes=0,r.pendingProps=d,t.deletions=null):(r=kr(a,d),r.subtreeFlags=a.subtreeFlags&14680064),l!==null?o=kr(l,o):(o=Xr(o,i,n,null),o.flags|=2),o.return=t,r.return=t,r.sibling=o,t.child=r,r=o,o=t.child,i=e.child.memoizedState,i=i===null?yi(n):{baseLanes:i.baseLanes|n,cachePool:null,transitions:i.transitions},o.memoizedState=i,o.childLanes=e.childLanes&~n,t.memoizedState=gi,r}return o=e.child,e=o.sibling,r=kr(o,{mode:"visible",children:r.children}),(t.mode&1)===0&&(r.lanes=n),r.return=t,r.sibling=null,e!==null&&(n=t.deletions,n===null?(t.deletions=[e],t.flags|=16):n.push(e)),t.child=r,t.memoizedState=null,r}function fi(e,t){return t=ls({mode:"visible",children:t},e.mode,0,null),t.return=e,e.child=t}function Vo(e,t,n,r){return r!==null&&Us(r),Ca(t,e.child,null,n),e=fi(t,t.pendingProps.children),e.flags|=2,t.memoizedState=null,e}function Sp(e,t,n,r,a,o,i){if(n)return t.flags&256?(t.flags&=-257,r=pi(Error(g(422))),Vo(e,t,i,r)):t.memoizedState!==null?(t.child=e.child,t.flags|=128,null):(o=r.fallback,a=t.mode,r=ls({mode:"visible",children:r.children},a,0,null),o=Xr(o,a,i,null),o.flags|=2,r.return=t,o.return=t,r.sibling=o,t.child=r,(t.mode&1)!==0&&Ca(t,e.child,null,i),t.child.memoizedState=yi(i),t.memoizedState=gi,o);if((t.mode&1)===0)return Vo(e,t,i,null);if(a.data==="$!"){if(r=a.nextSibling&&a.nextSibling.dataset,r)var l=r.dgst;return r=l,o=Error(g(419)),r=pi(o,r,void 0),Vo(e,t,i,r)}if(l=(i&e.childLanes)!==0,Xt||l){if(r=kt,r!==null){switch(i&-i){case 4:a=2;break;case 16:a=8;break;case 64:case 128:case 256:case 512:case 1024:case 2048:case 4096:case 8192:case 16384:case 32768:case 65536:case 131072:case 262144:case 524288:case 1048576:case 2097152:case 4194304:case 8388608:case 16777216:case 33554432:case 67108864:a=32;break;case 536870912:a=268435456;break;default:a=0}a=(a&(r.suspendedLanes|i))!==0?0:a,a!==0&&a!==o.retryLane&&(o.retryLane=a,Zn(e,a),Ln(r,e,a,-1))}return xi(),r=pi(Error(g(421))),Vo(e,t,i,r)}return a.data==="$?"?(t.flags|=128,t.child=e.child,t=xp.bind(null,e),a._reactRetry=t,null):(e=o.treeContext,on=ur(a.nextSibling),an=t,et=!0,En=null,e!==null&&(pn[un++]=Xn,pn[un++]=Jn,pn[un++]=Gr,Xn=e.id,Jn=e.overflow,Gr=t),t=fi(t,r.children),t.flags|=4096,t)}function Ac(e,t,n){e.lanes|=t;var r=e.alternate;r!==null&&(r.lanes|=t),Ys(e.return,t,n)}function vi(e,t,n,r,a){var o=e.memoizedState;o===null?e.memoizedState={isBackwards:t,rendering:null,renderingStartTime:0,last:r,tail:n,tailMode:a}:(o.isBackwards=t,o.rendering=null,o.renderingStartTime=0,o.last=r,o.tail=n,o.tailMode=a)}function Lc(e,t,n){var r=t.pendingProps,a=r.revealOrder,o=r.tail;if(zt(e,t,r.children,n),r=nt.current,(r&2)!==0)r=r&1|2,t.flags|=128;else{if(e!==null&&(e.flags&128)!==0)e:for(e=t.child;e!==null;){if(e.tag===13)e.memoizedState!==null&&Ac(e,n,t);else if(e.tag===19)Ac(e,n,t);else if(e.child!==null){e.child.return=e,e=e.child;continue}if(e===t)break e;for(;e.sibling===null;){if(e.return===null||e.return===t)break e;e=e.return}e.sibling.return=e.return,e=e.sibling}r&=1}if(Ve(nt,r),(t.mode&1)===0)t.memoizedState=null;else switch(a){case"forwards":for(n=t.child,a=null;n!==null;)e=n.alternate,e!==null&&Go(e)===null&&(a=n),n=n.sibling;n=a,n===null?(a=t.child,t.child=null):(a=n.sibling,n.sibling=null),vi(t,!1,a,n,o);break;case"backwards":for(n=null,a=t.child,t.child=null;a!==null;){if(e=a.alternate,e!==null&&Go(e)===null){t.child=a;break}e=a.sibling,a.sibling=n,n=a,a=e}vi(t,!0,n,null,o);break;case"together":vi(t,!1,null,null,void 0);break;default:t.memoizedState=null}return t.child}function $o(e,t){(t.mode&1)===0&&e!==null&&(e.alternate=null,t.alternate=null,t.flags|=2)}function tr(e,t,n){if(e!==null&&(t.dependencies=e.dependencies),qr|=t.lanes,(n&t.childLanes)===0)return null;if(e!==null&&t.child!==e.child)throw Error(g(153));if(t.child!==null){for(e=t.child,n=kr(e,e.pendingProps),t.child=n,n.return=t;e.sibling!==null;)e=e.sibling,n=n.sibling=kr(e,e.pendingProps),n.return=t;n.sibling=null}return t.child}function wp(e,t,n){switch(t.tag){case 3:Ec(t),wa();break;case 5:Gl(t);break;case 1:$t(t.type)&&Ro(t);break;case 4:Xs(t,t.stateNode.containerInfo);break;case 10:var r=t.type._context,a=t.memoizedProps.value;Ve(Fo,r._currentValue),r._currentValue=a;break;case 13:if(r=t.memoizedState,r!==null)return r.dehydrated!==null?(Ve(nt,nt.current&1),t.flags|=128,null):(n&t.child.childLanes)!==0?_c(e,t,n):(Ve(nt,nt.current&1),e=tr(e,t,n),e!==null?e.sibling:null);Ve(nt,nt.current&1);break;case 19:if(r=(n&t.childLanes)!==0,(e.flags&128)!==0){if(r)return Lc(e,t,n);t.flags|=128}if(a=t.memoizedState,a!==null&&(a.rendering=null,a.tail=null,a.lastEffect=null),Ve(nt,nt.current),r)break;return null;case 22:case 23:return t.lanes=0,wc(e,t,n)}return tr(e,t,n)}var Ic,bi,Nc,Pc;Ic=function(e,t){for(var n=t.child;n!==null;){if(n.tag===5||n.tag===6)e.appendChild(n.stateNode);else if(n.tag!==4&&n.child!==null){n.child.return=n,n=n.child;continue}if(n===t)break;for(;n.sibling===null;){if(n.return===null||n.return===t)return;n=n.return}n.sibling.return=n.return,n=n.sibling}},bi=function(){},Nc=function(e,t,n,r){var a=e.memoizedProps;if(a!==r){e=t.stateNode,Qr(Bn.current);var o=null;switch(n){case"input":a=Ra(e,a),r=Ra(e,r),o=[];break;case"select":a=x({},a,{value:void 0}),r=x({},r,{value:void 0}),o=[];break;case"textarea":a=Tt(e,a),r=Tt(e,r),o=[];break;default:typeof a.onClick!="function"&&typeof r.onClick=="function"&&(e.onclick=Io)}ea(n,r);var i;n=null;for(f in a)if(!r.hasOwnProperty(f)&&a.hasOwnProperty(f)&&a[f]!=null)if(f==="style"){var l=a[f];for(i in l)l.hasOwnProperty(i)&&(n||(n={}),n[i]="")}else f!=="dangerouslySetInnerHTML"&&f!=="children"&&f!=="suppressContentEditableWarning"&&f!=="suppressHydrationWarning"&&f!=="autoFocus"&&(H.hasOwnProperty(f)?o||(o=[]):(o=o||[]).push(f,null));for(f in r){var d=r[f];if(l=a!=null?a[f]:void 0,r.hasOwnProperty(f)&&d!==l&&(d!=null||l!=null))if(f==="style")if(l){for(i in l)!l.hasOwnProperty(i)||d&&d.hasOwnProperty(i)||(n||(n={}),n[i]="");for(i in d)d.hasOwnProperty(i)&&l[i]!==d[i]&&(n||(n={}),n[i]=d[i])}else n||(o||(o=[]),o.push(f,n)),n=d;else f==="dangerouslySetInnerHTML"?(d=d?d.__html:void 0,l=l?l.__html:void 0,d!=null&&l!==d&&(o=o||[]).push(f,d)):f==="children"?typeof d!="string"&&typeof d!="number"||(o=o||[]).push(f,""+d):f!=="suppressContentEditableWarning"&&f!=="suppressHydrationWarning"&&(H.hasOwnProperty(f)?(d!=null&&f==="onScroll"&&$e("scroll",e),o||l===d||(o=[])):(o=o||[]).push(f,d))}n&&(o=o||[]).push("style",n);var f=o;(t.updateQueue=f)&&(t.flags|=4)}},Pc=function(e,t,n,r){n!==r&&(t.flags|=4)};function io(e,t){if(!et)switch(e.tailMode){case"hidden":t=e.tail;for(var n=null;t!==null;)t.alternate!==null&&(n=t),t=t.sibling;n===null?e.tail=null:n.sibling=null;break;case"collapsed":n=e.tail;for(var r=null;n!==null;)n.alternate!==null&&(r=n),n=n.sibling;r===null?t||e.tail===null?e.tail=null:e.tail.sibling=null:r.sibling=null}}function Ft(e){var t=e.alternate!==null&&e.alternate.child===e.child,n=0,r=0;if(t)for(var a=e.child;a!==null;)n|=a.lanes|a.childLanes,r|=a.subtreeFlags&14680064,r|=a.flags&14680064,a.return=e,a=a.sibling;else for(a=e.child;a!==null;)n|=a.lanes|a.childLanes,r|=a.subtreeFlags,r|=a.flags,a.return=e,a=a.sibling;return e.subtreeFlags|=r,e.childLanes=n,t}function Cp(e,t,n){var r=t.pendingProps;switch(Hs(t),t.tag){case 2:case 16:case 15:case 0:case 11:case 7:case 8:case 12:case 9:case 14:return Ft(t),null;case 1:return $t(t.type)&&Po(),Ft(t),null;case 3:return r=t.stateNode,Ta(),Xe(Vt),Xe(Ot),ei(),r.pendingContext&&(r.context=r.pendingContext,r.pendingContext=null),(e===null||e.child===null)&&(Oo(t)?t.flags|=4:e===null||e.memoizedState.isDehydrated&&(t.flags&256)===0||(t.flags|=1024,En!==null&&(Ni(En),En=null))),bi(e,t),Ft(t),null;case 5:Js(t);var a=Qr(no.current);if(n=t.type,e!==null&&t.stateNode!=null)Nc(e,t,n,r,a),e.ref!==t.ref&&(t.flags|=512,t.flags|=2097152);else{if(!r){if(t.stateNode===null)throw Error(g(166));return Ft(t),null}if(e=Qr(Bn.current),Oo(t)){r=t.stateNode,n=t.type;var o=t.memoizedProps;switch(r[On]=t,r[Xa]=o,e=(t.mode&1)!==0,n){case"dialog":$e("cancel",r),$e("close",r);break;case"iframe":case"object":case"embed":$e("load",r);break;case"video":case"audio":for(a=0;a<Ya.length;a++)$e(Ya[a],r);break;case"source":$e("error",r);break;case"img":case"image":case"link":$e("error",r),$e("load",r);break;case"details":$e("toggle",r);break;case"input":fo(r,o),$e("invalid",r);break;case"select":r._wrapperState={wasMultiple:!!o.multiple},$e("invalid",r);break;case"textarea":cn(r,o),$e("invalid",r)}ea(n,o),a=null;for(var i in o)if(o.hasOwnProperty(i)){var l=o[i];i==="children"?typeof l=="string"?r.textContent!==l&&(o.suppressHydrationWarning!==!0&&Lo(r.textContent,l,e),a=["children",l]):typeof l=="number"&&r.textContent!==""+l&&(o.suppressHydrationWarning!==!0&&Lo(r.textContent,l,e),a=["children",""+l]):H.hasOwnProperty(i)&&l!=null&&i==="onScroll"&&$e("scroll",r)}switch(n){case"input":Hn(r),Ar(r,o,!0);break;case"textarea":Hn(r),ce(r);break;case"select":case"option":break;default:typeof o.onClick=="function"&&(r.onclick=Io)}r=a,t.updateQueue=r,r!==null&&(t.flags|=4)}else{i=a.nodeType===9?a:a.ownerDocument,e==="http://www.w3.org/1999/xhtml"&&(e=Zr(n)),e==="http://www.w3.org/1999/xhtml"?n==="script"?(e=i.createElement("div"),e.innerHTML="<script><\/script>",e=e.removeChild(e.firstChild)):typeof r.is=="string"?e=i.createElement(n,{is:r.is}):(e=i.createElement(n),n==="select"&&(i=e,r.multiple?i.multiple=!0:r.size&&(i.size=r.size))):e=i.createElementNS(e,n),e[On]=t,e[Xa]=r,Ic(e,t,!1,!1),t.stateNode=e;e:{switch(i=ta(n,r),n){case"dialog":$e("cancel",e),$e("close",e),a=r;break;case"iframe":case"object":case"embed":$e("load",e),a=r;break;case"video":case"audio":for(a=0;a<Ya.length;a++)$e(Ya[a],e);a=r;break;case"source":$e("error",e),a=r;break;case"img":case"image":case"link":$e("error",e),$e("load",e),a=r;break;case"details":$e("toggle",e),a=r;break;case"input":fo(e,r),a=Ra(e,r),$e("invalid",e);break;case"option":a=r;break;case"select":e._wrapperState={wasMultiple:!!r.multiple},a=x({},r,{value:void 0}),$e("invalid",e);break;case"textarea":cn(e,r),a=Tt(e,r),$e("invalid",e);break;default:a=r}ea(n,a),l=a;for(o in l)if(l.hasOwnProperty(o)){var d=l[o];o==="style"?vo(e,d):o==="dangerouslySetInnerHTML"?(d=d?d.__html:void 0,d!=null&&Ma(e,d)):o==="children"?typeof d=="string"?(n!=="textarea"||d!=="")&&Fe(e,d):typeof d=="number"&&Fe(e,""+d):o!=="suppressContentEditableWarning"&&o!=="suppressHydrationWarning"&&o!=="autoFocus"&&(H.hasOwnProperty(o)?d!=null&&o==="onScroll"&&$e("scroll",e):d!=null&&ft(e,o,d,i))}switch(n){case"input":Hn(e),Ar(e,r,!1);break;case"textarea":Hn(e),ce(e);break;case"option":r.value!=null&&e.setAttribute("value",""+_e(r.value));break;case"select":e.multiple=!!r.multiple,o=r.value,o!=null?rr(e,!!r.multiple,o,!1):r.defaultValue!=null&&rr(e,!!r.multiple,r.defaultValue,!0);break;default:typeof a.onClick=="function"&&(e.onclick=Io)}switch(n){case"button":case"input":case"select":case"textarea":r=!!r.autoFocus;break e;case"img":r=!0;break e;default:r=!1}}r&&(t.flags|=4)}t.ref!==null&&(t.flags|=512,t.flags|=2097152)}return Ft(t),null;case 6:if(e&&t.stateNode!=null)Pc(e,t,e.memoizedProps,r);else{if(typeof r!="string"&&t.stateNode===null)throw Error(g(166));if(n=Qr(no.current),Qr(Bn.current),Oo(t)){if(r=t.stateNode,n=t.memoizedProps,r[On]=t,(o=r.nodeValue!==n)&&(e=an,e!==null))switch(e.tag){case 3:Lo(r.nodeValue,n,(e.mode&1)!==0);break;case 5:e.memoizedProps.suppressHydrationWarning!==!0&&Lo(r.nodeValue,n,(e.mode&1)!==0)}o&&(t.flags|=4)}else r=(n.nodeType===9?n:n.ownerDocument).createTextNode(r),r[On]=t,t.stateNode=r}return Ft(t),null;case 13:if(Xe(nt),r=t.memoizedState,e===null||e.memoizedState!==null&&e.memoizedState.dehydrated!==null){if(et&&on!==null&&(t.mode&1)!==0&&(t.flags&128)===0)Ml(),wa(),t.flags|=98560,o=!1;else if(o=Oo(t),r!==null&&r.dehydrated!==null){if(e===null){if(!o)throw Error(g(318));if(o=t.memoizedState,o=o!==null?o.dehydrated:null,!o)throw Error(g(317));o[On]=t}else wa(),(t.flags&128)===0&&(t.memoizedState=null),t.flags|=4;Ft(t),o=!1}else En!==null&&(Ni(En),En=null),o=!0;if(!o)return t.flags&65536?t:null}return(t.flags&128)!==0?(t.lanes=n,t):(r=r!==null,r!==(e!==null&&e.memoizedState!==null)&&r&&(t.child.flags|=8192,(t.mode&1)!==0&&(e===null||(nt.current&1)!==0?gt===0&&(gt=3):xi())),t.updateQueue!==null&&(t.flags|=4),Ft(t),null);case 4:return Ta(),bi(e,t),e===null&&Va(t.stateNode.containerInfo),Ft(t),null;case 10:return qs(t.type._context),Ft(t),null;case 17:return $t(t.type)&&Po(),Ft(t),null;case 19:if(Xe(nt),o=t.memoizedState,o===null)return Ft(t),null;if(r=(t.flags&128)!==0,i=o.rendering,i===null)if(r)io(o,!1);else{if(gt!==0||e!==null&&(e.flags&128)!==0)for(e=t.child;e!==null;){if(i=Go(e),i!==null){for(t.flags|=128,io(o,!1),r=i.updateQueue,r!==null&&(t.updateQueue=r,t.flags|=4),t.subtreeFlags=0,r=n,n=t.child;n!==null;)o=n,e=r,o.flags&=14680066,i=o.alternate,i===null?(o.childLanes=0,o.lanes=e,o.child=null,o.subtreeFlags=0,o.memoizedProps=null,o.memoizedState=null,o.updateQueue=null,o.dependencies=null,o.stateNode=null):(o.childLanes=i.childLanes,o.lanes=i.lanes,o.child=i.child,o.subtreeFlags=0,o.deletions=null,o.memoizedProps=i.memoizedProps,o.memoizedState=i.memoizedState,o.updateQueue=i.updateQueue,o.type=i.type,e=i.dependencies,o.dependencies=e===null?null:{lanes:e.lanes,firstContext:e.firstContext}),n=n.sibling;return Ve(nt,nt.current&1|2),t.child}e=e.sibling}o.tail!==null&&c()>Ia&&(t.flags|=128,r=!0,io(o,!1),t.lanes=4194304)}else{if(!r)if(e=Go(i),e!==null){if(t.flags|=128,r=!0,n=e.updateQueue,n!==null&&(t.updateQueue=n,t.flags|=4),io(o,!0),o.tail===null&&o.tailMode==="hidden"&&!i.alternate&&!et)return Ft(t),null}else 2*c()-o.renderingStartTime>Ia&&n!==1073741824&&(t.flags|=128,r=!0,io(o,!1),t.lanes=4194304);o.isBackwards?(i.sibling=t.child,t.child=i):(n=o.last,n!==null?n.sibling=i:t.child=i,o.last=i)}return o.tail!==null?(t=o.tail,o.rendering=t,o.tail=t.sibling,o.renderingStartTime=c(),t.sibling=null,n=nt.current,Ve(nt,r?n&1|2:n&1),t):(Ft(t),null);case 22:case 23:return Ri(),r=t.memoizedState!==null,e!==null&&e.memoizedState!==null!==r&&(t.flags|=8192),r&&(t.mode&1)!==0?(sn&1073741824)!==0&&(Ft(t),t.subtreeFlags&6&&(t.flags|=8192)):Ft(t),null;case 24:return null;case 25:return null}throw Error(g(156,t.tag))}function kp(e,t){switch(Hs(t),t.tag){case 1:return $t(t.type)&&Po(),e=t.flags,e&65536?(t.flags=e&-65537|128,t):null;case 3:return Ta(),Xe(Vt),Xe(Ot),ei(),e=t.flags,(e&65536)!==0&&(e&128)===0?(t.flags=e&-65537|128,t):null;case 5:return Js(t),null;case 13:if(Xe(nt),e=t.memoizedState,e!==null&&e.dehydrated!==null){if(t.alternate===null)throw Error(g(340));wa()}return e=t.flags,e&65536?(t.flags=e&-65537|128,t):null;case 19:return Xe(nt),null;case 4:return Ta(),null;case 10:return qs(t.type._context),null;case 22:case 23:return Ri(),null;case 24:return null;default:return null}}var Xo=!1,Kt=!1,Ep=typeof WeakSet=="function"?WeakSet:Set,Q=null;function Aa(e,t){var n=e.ref;if(n!==null)if(typeof n=="function")try{n(null)}catch(r){at(e,t,r)}else n.current=null}function Si(e,t,n){try{n()}catch(r){at(e,t,r)}}var Rc=!1;function Tp(e,t){if(Rs=Qn,e=dl(),Es(e)){if("selectionStart"in e)var n={start:e.selectionStart,end:e.selectionEnd};else e:{n=(n=e.ownerDocument)&&n.defaultView||window;var r=n.getSelection&&n.getSelection();if(r&&r.rangeCount!==0){n=r.anchorNode;var a=r.anchorOffset,o=r.focusNode;r=r.focusOffset;try{n.nodeType,o.nodeType}catch{n=null;break e}var i=0,l=-1,d=-1,f=0,_=0,I=e,E=null;t:for(;;){for(var W;I!==n||a!==0&&I.nodeType!==3||(l=i+a),I!==o||r!==0&&I.nodeType!==3||(d=i+r),I.nodeType===3&&(i+=I.nodeValue.length),(W=I.firstChild)!==null;)E=I,I=W;for(;;){if(I===e)break t;if(E===n&&++f===a&&(l=i),E===o&&++_===r&&(d=i),(W=I.nextSibling)!==null)break;I=E,E=I.parentNode}I=W}n=l===-1||d===-1?null:{start:l,end:d}}else n=null}n=n||{start:0,end:0}}else n=null;for(xs={focusedElem:e,selectionRange:n},Qn=!1,Q=t;Q!==null;)if(t=Q,e=t.child,(t.subtreeFlags&1028)!==0&&e!==null)e.return=t,Q=e;else for(;Q!==null;){t=Q;try{var Y=t.alternate;if((t.flags&1024)!==0)switch(t.tag){case 0:case 11:case 15:break;case 1:if(Y!==null){var V=Y.memoizedProps,st=Y.memoizedState,h=t.stateNode,p=h.getSnapshotBeforeUpdate(t.elementType===t.type?V:Tn(t.type,V),st);h.__reactInternalSnapshotBeforeUpdate=p}break;case 3:var y=t.stateNode.containerInfo;y.nodeType===1?y.textContent="":y.nodeType===9&&y.documentElement&&y.removeChild(y.documentElement);break;case 5:case 6:case 4:case 17:break;default:throw Error(g(163))}}catch(R){at(t,t.return,R)}if(e=t.sibling,e!==null){e.return=t.return,Q=e;break}Q=t.return}return Y=Rc,Rc=!1,Y}function lo(e,t,n){var r=t.updateQueue;if(r=r!==null?r.lastEffect:null,r!==null){var a=r=r.next;do{if((a.tag&e)===e){var o=a.destroy;a.destroy=void 0,o!==void 0&&Si(t,n,o)}a=a.next}while(a!==r)}}function Jo(e,t){if(t=t.updateQueue,t=t!==null?t.lastEffect:null,t!==null){var n=t=t.next;do{if((n.tag&e)===e){var r=n.create;n.destroy=r()}n=n.next}while(n!==t)}}function wi(e){var t=e.ref;if(t!==null){var n=e.stateNode;switch(e.tag){case 5:e=n;break;default:e=n}typeof t=="function"?t(e):t.current=e}}function xc(e){var t=e.alternate;t!==null&&(e.alternate=null,xc(t)),e.child=null,e.deletions=null,e.sibling=null,e.tag===5&&(t=e.stateNode,t!==null&&(delete t[On],delete t[Xa],delete t[Bs],delete t[ip],delete t[lp])),e.stateNode=null,e.return=null,e.dependencies=null,e.memoizedProps=null,e.memoizedState=null,e.pendingProps=null,e.stateNode=null,e.updateQueue=null}function Mc(e){return e.tag===5||e.tag===3||e.tag===4}function Dc(e){e:for(;;){for(;e.sibling===null;){if(e.return===null||Mc(e.return))return null;e=e.return}for(e.sibling.return=e.return,e=e.sibling;e.tag!==5&&e.tag!==6&&e.tag!==18;){if(e.flags&2||e.child===null||e.tag===4)continue e;e.child.return=e,e=e.child}if(!(e.flags&2))return e.stateNode}}function Ci(e,t,n){var r=e.tag;if(r===5||r===6)e=e.stateNode,t?n.nodeType===8?n.parentNode.insertBefore(e,t):n.insertBefore(e,t):(n.nodeType===8?(t=n.parentNode,t.insertBefore(e,n)):(t=n,t.appendChild(e)),n=n._reactRootContainer,n!=null||t.onclick!==null||(t.onclick=Io));else if(r!==4&&(e=e.child,e!==null))for(Ci(e,t,n),e=e.sibling;e!==null;)Ci(e,t,n),e=e.sibling}function ki(e,t,n){var r=e.tag;if(r===5||r===6)e=e.stateNode,t?n.insertBefore(e,t):n.appendChild(e);else if(r!==4&&(e=e.child,e!==null))for(ki(e,t,n),e=e.sibling;e!==null;)ki(e,t,n),e=e.sibling}var At=null,_n=!1;function vr(e,t,n){for(n=n.child;n!==null;)Oc(e,t,n),n=n.sibling}function Oc(e,t,n){if(re&&typeof re.onCommitFiberUnmount=="function")try{re.onCommitFiberUnmount(K,n)}catch{}switch(n.tag){case 5:Kt||Aa(n,t);case 6:var r=At,a=_n;At=null,vr(e,t,n),At=r,_n=a,At!==null&&(_n?(e=At,n=n.stateNode,e.nodeType===8?e.parentNode.removeChild(n):e.removeChild(n)):At.removeChild(n.stateNode));break;case 18:At!==null&&(_n?(e=At,n=n.stateNode,e.nodeType===8?Os(e.parentNode,n):e.nodeType===1&&Os(e,n),Sn(e)):Os(At,n.stateNode));break;case 4:r=At,a=_n,At=n.stateNode.containerInfo,_n=!0,vr(e,t,n),At=r,_n=a;break;case 0:case 11:case 14:case 15:if(!Kt&&(r=n.updateQueue,r!==null&&(r=r.lastEffect,r!==null))){a=r=r.next;do{var o=a,i=o.destroy;o=o.tag,i!==void 0&&((o&2)!==0||(o&4)!==0)&&Si(n,t,i),a=a.next}while(a!==r)}vr(e,t,n);break;case 1:if(!Kt&&(Aa(n,t),r=n.stateNode,typeof r.componentWillUnmount=="function"))try{r.props=n.memoizedProps,r.state=n.memoizedState,r.componentWillUnmount()}catch(l){at(n,t,l)}vr(e,t,n);break;case 21:vr(e,t,n);break;case 22:n.mode&1?(Kt=(r=Kt)||n.memoizedState!==null,vr(e,t,n),Kt=r):vr(e,t,n);break;default:vr(e,t,n)}}function Bc(e){var t=e.updateQueue;if(t!==null){e.updateQueue=null;var n=e.stateNode;n===null&&(n=e.stateNode=new Ep),t.forEach(function(r){var a=Mp.bind(null,e,r);n.has(r)||(n.add(r),r.then(a,a))})}}function An(e,t){var n=t.deletions;if(n!==null)for(var r=0;r<n.length;r++){var a=n[r];try{var o=e,i=t,l=i;e:for(;l!==null;){switch(l.tag){case 5:At=l.stateNode,_n=!1;break e;case 3:At=l.stateNode.containerInfo,_n=!0;break e;case 4:At=l.stateNode.containerInfo,_n=!0;break e}l=l.return}if(At===null)throw Error(g(160));Oc(o,i,a),At=null,_n=!1;var d=a.alternate;d!==null&&(d.return=null),a.return=null}catch(f){at(a,t,f)}}if(t.subtreeFlags&12854)for(t=t.child;t!==null;)Fc(t,e),t=t.sibling}function Fc(e,t){var n=e.alternate,r=e.flags;switch(e.tag){case 0:case 11:case 14:case 15:if(An(t,e),Kn(e),r&4){try{lo(3,e,e.return),Jo(3,e)}catch(V){at(e,e.return,V)}try{lo(5,e,e.return)}catch(V){at(e,e.return,V)}}break;case 1:An(t,e),Kn(e),r&512&&n!==null&&Aa(n,n.return);break;case 5:if(An(t,e),Kn(e),r&512&&n!==null&&Aa(n,n.return),e.flags&32){var a=e.stateNode;try{Fe(a,"")}catch(V){at(e,e.return,V)}}if(r&4&&(a=e.stateNode,a!=null)){var o=e.memoizedProps,i=n!==null?n.memoizedProps:o,l=e.type,d=e.updateQueue;if(e.updateQueue=null,d!==null)try{l==="input"&&o.type==="radio"&&o.name!=null&&Jr(a,o),ta(l,i);var f=ta(l,o);for(i=0;i<d.length;i+=2){var _=d[i],I=d[i+1];_==="style"?vo(a,I):_==="dangerouslySetInnerHTML"?Ma(a,I):_==="children"?Fe(a,I):ft(a,_,I,f)}switch(l){case"input":_r(a,o);break;case"textarea":fn(a,o);break;case"select":var E=a._wrapperState.wasMultiple;a._wrapperState.wasMultiple=!!o.multiple;var W=o.value;W!=null?rr(a,!!o.multiple,W,!1):E!==!!o.multiple&&(o.defaultValue!=null?rr(a,!!o.multiple,o.defaultValue,!0):rr(a,!!o.multiple,o.multiple?[]:"",!1))}a[Xa]=o}catch(V){at(e,e.return,V)}}break;case 6:if(An(t,e),Kn(e),r&4){if(e.stateNode===null)throw Error(g(162));a=e.stateNode,o=e.memoizedProps;try{a.nodeValue=o}catch(V){at(e,e.return,V)}}break;case 3:if(An(t,e),Kn(e),r&4&&n!==null&&n.memoizedState.isDehydrated)try{Sn(t.containerInfo)}catch(V){at(e,e.return,V)}break;case 4:An(t,e),Kn(e);break;case 13:An(t,e),Kn(e),a=e.child,a.flags&8192&&(o=a.memoizedState!==null,a.stateNode.isHidden=o,!o||a.alternate!==null&&a.alternate.memoizedState!==null||(_i=c())),r&4&&Bc(e);break;case 22:if(_=n!==null&&n.memoizedState!==null,e.mode&1?(Kt=(f=Kt)||_,An(t,e),Kt=f):An(t,e),Kn(e),r&8192){if(f=e.memoizedState!==null,(e.stateNode.isHidden=f)&&!_&&(e.mode&1)!==0)for(Q=e,_=e.child;_!==null;){for(I=Q=_;Q!==null;){switch(E=Q,W=E.child,E.tag){case 0:case 11:case 14:case 15:lo(4,E,E.return);break;case 1:Aa(E,E.return);var Y=E.stateNode;if(typeof Y.componentWillUnmount=="function"){r=E,n=E.return;try{t=r,Y.props=t.memoizedProps,Y.state=t.memoizedState,Y.componentWillUnmount()}catch(V){at(r,n,V)}}break;case 5:Aa(E,E.return);break;case 22:if(E.memoizedState!==null){Hc(I);continue}}W!==null?(W.return=E,Q=W):Hc(I)}_=_.sibling}e:for(_=null,I=e;;){if(I.tag===5){if(_===null){_=I;try{a=I.stateNode,f?(o=a.style,typeof o.setProperty=="function"?o.setProperty("display","none","important"):o.display="none"):(l=I.stateNode,d=I.memoizedProps.style,i=d!=null&&d.hasOwnProperty("display")?d.display:null,l.style.display=Da("display",i))}catch(V){at(e,e.return,V)}}}else if(I.tag===6){if(_===null)try{I.stateNode.nodeValue=f?"":I.memoizedProps}catch(V){at(e,e.return,V)}}else if((I.tag!==22&&I.tag!==23||I.memoizedState===null||I===e)&&I.child!==null){I.child.return=I,I=I.child;continue}if(I===e)break e;for(;I.sibling===null;){if(I.return===null||I.return===e)break e;_===I&&(_=null),I=I.return}_===I&&(_=null),I.sibling.return=I.return,I=I.sibling}}break;case 19:An(t,e),Kn(e),r&4&&Bc(e);break;case 21:break;default:An(t,e),Kn(e)}}function Kn(e){var t=e.flags;if(t&2){try{e:{for(var n=e.return;n!==null;){if(Mc(n)){var r=n;break e}n=n.return}throw Error(g(160))}switch(r.tag){case 5:var a=r.stateNode;r.flags&32&&(Fe(a,""),r.flags&=-33);var o=Dc(e);ki(e,o,a);break;case 3:case 4:var i=r.stateNode.containerInfo,l=Dc(e);Ci(e,l,i);break;default:throw Error(g(161))}}catch(d){at(e,e.return,d)}e.flags&=-3}t&4096&&(e.flags&=-4097)}function _p(e,t,n){Q=e,Kc(e)}function Kc(e,t,n){for(var r=(e.mode&1)!==0;Q!==null;){var a=Q,o=a.child;if(a.tag===22&&r){var i=a.memoizedState!==null||Xo;if(!i){var l=a.alternate,d=l!==null&&l.memoizedState!==null||Kt;l=Xo;var f=Kt;if(Xo=i,(Kt=d)&&!f)for(Q=a;Q!==null;)i=Q,d=i.child,i.tag===22&&i.memoizedState!==null?Gc(a):d!==null?(d.return=i,Q=d):Gc(a);for(;o!==null;)Q=o,Kc(o),o=o.sibling;Q=a,Xo=l,Kt=f}jc(e)}else(a.subtreeFlags&8772)!==0&&o!==null?(o.return=a,Q=o):jc(e)}}function jc(e){for(;Q!==null;){var t=Q;if((t.flags&8772)!==0){var n=t.alternate;try{if((t.flags&8772)!==0)switch(t.tag){case 0:case 11:case 15:Kt||Jo(5,t);break;case 1:var r=t.stateNode;if(t.flags&4&&!Kt)if(n===null)r.componentDidMount();else{var a=t.elementType===t.type?n.memoizedProps:Tn(t.type,n.memoizedProps);r.componentDidUpdate(a,n.memoizedState,r.__reactInternalSnapshotBeforeUpdate)}var o=t.updateQueue;o!==null&&Hl(t,o,r);break;case 3:var i=t.updateQueue;if(i!==null){if(n=null,t.child!==null)switch(t.child.tag){case 5:n=t.child.stateNode;break;case 1:n=t.child.stateNode}Hl(t,i,n)}break;case 5:var l=t.stateNode;if(n===null&&t.flags&4){n=l;var d=t.memoizedProps;switch(t.type){case"button":case"input":case"select":case"textarea":d.autoFocus&&n.focus();break;case"img":d.src&&(n.src=d.src)}}break;case 6:break;case 4:break;case 12:break;case 13:if(t.memoizedState===null){var f=t.alternate;if(f!==null){var _=f.memoizedState;if(_!==null){var I=_.dehydrated;I!==null&&Sn(I)}}}break;case 19:case 17:case 21:case 22:case 23:case 25:break;default:throw Error(g(163))}Kt||t.flags&512&&wi(t)}catch(E){at(t,t.return,E)}}if(t===e){Q=null;break}if(n=t.sibling,n!==null){n.return=t.return,Q=n;break}Q=t.return}}function Hc(e){for(;Q!==null;){var t=Q;if(t===e){Q=null;break}var n=t.sibling;if(n!==null){n.return=t.return,Q=n;break}Q=t.return}}function Gc(e){for(;Q!==null;){var t=Q;try{switch(t.tag){case 0:case 11:case 15:var n=t.return;try{Jo(4,t)}catch(d){at(t,n,d)}break;case 1:var r=t.stateNode;if(typeof r.componentDidMount=="function"){var a=t.return;try{r.componentDidMount()}catch(d){at(t,a,d)}}var o=t.return;try{wi(t)}catch(d){at(t,o,d)}break;case 5:var i=t.return;try{wi(t)}catch(d){at(t,i,d)}}}catch(d){at(t,t.return,d)}if(t===e){Q=null;break}var l=t.sibling;if(l!==null){l.return=t.return,Q=l;break}Q=t.return}}var Ap=Math.ceil,Zo=L.ReactCurrentDispatcher,Ei=L.ReactCurrentOwner,gn=L.ReactCurrentBatchConfig,Pe=0,kt=null,pt=null,Lt=0,sn=0,La=mr(0),gt=0,co=null,qr=0,es=0,Ti=0,po=null,Jt=null,_i=0,Ia=1/0,nr=null,ts=!1,Ai=null,br=null,ns=!1,Sr=null,rs=0,uo=0,Li=null,as=-1,os=0;function qt(){return(Pe&6)!==0?c():as!==-1?as:as=c()}function wr(e){return(e.mode&1)===0?1:(Pe&2)!==0&&Lt!==0?Lt&-Lt:dp.transition!==null?(os===0&&(os=Oe()),os):(e=me,e!==0||(e=window.event,e=e===void 0?16:wo(e.type)),e)}function Ln(e,t,n,r){if(50<uo)throw uo=0,Li=null,Error(g(185));Ge(e,n,r),((Pe&2)===0||e!==kt)&&(e===kt&&((Pe&2)===0&&(es|=n),gt===4&&Cr(e,Lt)),Zt(e,r),n===1&&Pe===0&&(t.mode&1)===0&&(Ia=c()+500,xo&&gr()))}function Zt(e,t){var n=e.callbackNode;ze(e,t);var r=J(e,e===kt?Lt:0);if(r===0)n!==null&&v(n),e.callbackNode=null,e.callbackPriority=0;else if(t=r&-r,e.callbackPriority!==t){if(n!=null&&v(n),t===1)e.tag===0?cp(Uc.bind(null,e)):Il(Uc.bind(null,e)),op(function(){(Pe&6)===0&&gr()}),n=null;else{switch(Ke(r)){case 1:n=P;break;case 4:n=G;break;case 16:n=O;break;case 536870912:n=F;break;default:n=O}n=Jc(n,Wc.bind(null,e))}e.callbackPriority=t,e.callbackNode=n}}function Wc(e,t){if(as=-1,os=0,(Pe&6)!==0)throw Error(g(327));var n=e.callbackNode;if(Na()&&e.callbackNode!==n)return null;var r=J(e,e===kt?Lt:0);if(r===0)return null;if((r&30)!==0||(r&e.expiredLanes)!==0||t)t=ss(e,r);else{t=r;var a=Pe;Pe|=2;var o=zc();(kt!==e||Lt!==t)&&(nr=null,Ia=c()+500,Vr(e,t));do try{Np();break}catch(l){Qc(e,l)}while(!0);zs(),Zo.current=o,Pe=a,pt!==null?t=0:(kt=null,Lt=0,t=gt)}if(t!==0){if(t===2&&(a=qe(e),a!==0&&(r=a,t=Ii(e,a))),t===1)throw n=co,Vr(e,0),Cr(e,r),Zt(e,c()),n;if(t===6)Cr(e,r);else{if(a=e.current.alternate,(r&30)===0&&!Lp(a)&&(t=ss(e,r),t===2&&(o=qe(e),o!==0&&(r=o,t=Ii(e,o))),t===1))throw n=co,Vr(e,0),Cr(e,r),Zt(e,c()),n;switch(e.finishedWork=a,e.finishedLanes=r,t){case 0:case 1:throw Error(g(345));case 2:$r(e,Jt,nr);break;case 3:if(Cr(e,r),(r&130023424)===r&&(t=_i+500-c(),10<t)){if(J(e,0)!==0)break;if(a=e.suspendedLanes,(a&r)!==r){qt(),e.pingedLanes|=e.suspendedLanes&a;break}e.timeoutHandle=Ds($r.bind(null,e,Jt,nr),t);break}$r(e,Jt,nr);break;case 4:if(Cr(e,r),(r&4194240)===r)break;for(t=e.eventTimes,a=-1;0<r;){var i=31-de(r);o=1<<i,i=t[i],i>a&&(a=i),r&=~o}if(r=a,r=c()-r,r=(120>r?120:480>r?480:1080>r?1080:1920>r?1920:3e3>r?3e3:4320>r?4320:1960*Ap(r/1960))-r,10<r){e.timeoutHandle=Ds($r.bind(null,e,Jt,nr),r);break}$r(e,Jt,nr);break;case 5:$r(e,Jt,nr);break;default:throw Error(g(329))}}}return Zt(e,c()),e.callbackNode===n?Wc.bind(null,e):null}function Ii(e,t){var n=po;return e.current.memoizedState.isDehydrated&&(Vr(e,t).flags|=256),e=ss(e,t),e!==2&&(t=Jt,Jt=n,t!==null&&Ni(t)),e}function Ni(e){Jt===null?Jt=e:Jt.push.apply(Jt,e)}function Lp(e){for(var t=e;;){if(t.flags&16384){var n=t.updateQueue;if(n!==null&&(n=n.stores,n!==null))for(var r=0;r<n.length;r++){var a=n[r],o=a.getSnapshot;a=a.value;try{if(!kn(o(),a))return!1}catch{return!1}}}if(n=t.child,t.subtreeFlags&16384&&n!==null)n.return=t,t=n;else{if(t===e)break;for(;t.sibling===null;){if(t.return===null||t.return===e)return!0;t=t.return}t.sibling.return=t.return,t=t.sibling}}return!0}function Cr(e,t){for(t&=~Ti,t&=~es,e.suspendedLanes|=t,e.pingedLanes&=~t,e=e.expirationTimes;0<t;){var n=31-de(t),r=1<<n;e[n]=-1,t&=~r}}function Uc(e){if((Pe&6)!==0)throw Error(g(327));Na();var t=J(e,0);if((t&1)===0)return Zt(e,c()),null;var n=ss(e,t);if(e.tag!==0&&n===2){var r=qe(e);r!==0&&(t=r,n=Ii(e,r))}if(n===1)throw n=co,Vr(e,0),Cr(e,t),Zt(e,c()),n;if(n===6)throw Error(g(345));return e.finishedWork=e.current.alternate,e.finishedLanes=t,$r(e,Jt,nr),Zt(e,c()),null}function Pi(e,t){var n=Pe;Pe|=1;try{return e(t)}finally{Pe=n,Pe===0&&(Ia=c()+500,xo&&gr())}}function Yr(e){Sr!==null&&Sr.tag===0&&(Pe&6)===0&&Na();var t=Pe;Pe|=1;var n=gn.transition,r=me;try{if(gn.transition=null,me=1,e)return e()}finally{me=r,gn.transition=n,Pe=t,(Pe&6)===0&&gr()}}function Ri(){sn=La.current,Xe(La)}function Vr(e,t){e.finishedWork=null,e.finishedLanes=0;var n=e.timeoutHandle;if(n!==-1&&(e.timeoutHandle=-1,ap(n)),pt!==null)for(n=pt.return;n!==null;){var r=n;switch(Hs(r),r.tag){case 1:r=r.type.childContextTypes,r!=null&&Po();break;case 3:Ta(),Xe(Vt),Xe(Ot),ei();break;case 5:Js(r);break;case 4:Ta();break;case 13:Xe(nt);break;case 19:Xe(nt);break;case 10:qs(r.type._context);break;case 22:case 23:Ri()}n=n.return}if(kt=e,pt=e=kr(e.current,null),Lt=sn=t,gt=0,co=null,Ti=es=qr=0,Jt=po=null,Ur!==null){for(t=0;t<Ur.length;t++)if(n=Ur[t],r=n.interleaved,r!==null){n.interleaved=null;var a=r.next,o=n.pending;if(o!==null){var i=o.next;o.next=a,r.next=i}n.pending=r}Ur=null}return e}function Qc(e,t){do{var n=pt;try{if(zs(),Wo.current=qo,Uo){for(var r=rt.memoizedState;r!==null;){var a=r.queue;a!==null&&(a.pending=null),r=r.next}Uo=!1}if(zr=0,Ct=ht=rt=null,ro=!1,ao=0,Ei.current=null,n===null||n.return===null){gt=1,co=t,pt=null;break}e:{var o=e,i=n.return,l=n,d=t;if(t=Lt,l.flags|=32768,d!==null&&typeof d=="object"&&typeof d.then=="function"){var f=d,_=l,I=_.tag;if((_.mode&1)===0&&(I===0||I===11||I===15)){var E=_.alternate;E?(_.updateQueue=E.updateQueue,_.memoizedState=E.memoizedState,_.lanes=E.lanes):(_.updateQueue=null,_.memoizedState=null)}var W=yc(i);if(W!==null){W.flags&=-257,fc(W,i,l,o,t),W.mode&1&&gc(o,f,t),t=W,d=f;var Y=t.updateQueue;if(Y===null){var V=new Set;V.add(d),t.updateQueue=V}else Y.add(d);break e}else{if((t&1)===0){gc(o,f,t),xi();break e}d=Error(g(426))}}else if(et&&l.mode&1){var st=yc(i);if(st!==null){(st.flags&65536)===0&&(st.flags|=256),fc(st,i,l,o,t),Us(_a(d,l));break e}}o=d=_a(d,l),gt!==4&&(gt=2),po===null?po=[o]:po.push(o),o=i;do{switch(o.tag){case 3:o.flags|=65536,t&=-t,o.lanes|=t;var h=mc(o,d,t);jl(o,h);break e;case 1:l=d;var p=o.type,y=o.stateNode;if((o.flags&128)===0&&(typeof p.getDerivedStateFromError=="function"||y!==null&&typeof y.componentDidCatch=="function"&&(br===null||!br.has(y)))){o.flags|=65536,t&=-t,o.lanes|=t;var R=hc(o,l,t);jl(o,R);break e}}o=o.return}while(o!==null)}Yc(n)}catch(X){t=X,pt===n&&n!==null&&(pt=n=n.return);continue}break}while(!0)}function zc(){var e=Zo.current;return Zo.current=qo,e===null?qo:e}function xi(){(gt===0||gt===3||gt===2)&&(gt=4),kt===null||(qr&268435455)===0&&(es&268435455)===0||Cr(kt,Lt)}function ss(e,t){var n=Pe;Pe|=2;var r=zc();(kt!==e||Lt!==t)&&(nr=null,Vr(e,t));do try{Ip();break}catch(a){Qc(e,a)}while(!0);if(zs(),Pe=n,Zo.current=r,pt!==null)throw Error(g(261));return kt=null,Lt=0,gt}function Ip(){for(;pt!==null;)qc(pt)}function Np(){for(;pt!==null&&!w();)qc(pt)}function qc(e){var t=Xc(e.alternate,e,sn);e.memoizedProps=e.pendingProps,t===null?Yc(e):pt=t,Ei.current=null}function Yc(e){var t=e;do{var n=t.alternate;if(e=t.return,(t.flags&32768)===0){if(n=Cp(n,t,sn),n!==null){pt=n;return}}else{if(n=kp(n,t),n!==null){n.flags&=32767,pt=n;return}if(e!==null)e.flags|=32768,e.subtreeFlags=0,e.deletions=null;else{gt=6,pt=null;return}}if(t=t.sibling,t!==null){pt=t;return}pt=t=e}while(t!==null);gt===0&&(gt=5)}function $r(e,t,n){var r=me,a=gn.transition;try{gn.transition=null,me=1,Pp(e,t,n,r)}finally{gn.transition=a,me=r}return null}function Pp(e,t,n,r){do Na();while(Sr!==null);if((Pe&6)!==0)throw Error(g(327));n=e.finishedWork;var a=e.finishedLanes;if(n===null)return null;if(e.finishedWork=null,e.finishedLanes=0,n===e.current)throw Error(g(177));e.callbackNode=null,e.callbackPriority=0;var o=n.lanes|n.childLanes;if(Ye(e,o),e===kt&&(pt=kt=null,Lt=0),(n.subtreeFlags&2064)===0&&(n.flags&2064)===0||ns||(ns=!0,Jc(O,function(){return Na(),null})),o=(n.flags&15990)!==0,(n.subtreeFlags&15990)!==0||o){o=gn.transition,gn.transition=null;var i=me;me=1;var l=Pe;Pe|=4,Ei.current=null,Tp(e,n),Fc(n,e),Xd(xs),Qn=!!Rs,xs=Rs=null,e.current=n,_p(n),C(),Pe=l,me=i,gn.transition=o}else e.current=n;if(ns&&(ns=!1,Sr=e,rs=a),o=e.pendingLanes,o===0&&(br=null),Ie(n.stateNode),Zt(e,c()),t!==null)for(r=e.onRecoverableError,n=0;n<t.length;n++)a=t[n],r(a.value,{componentStack:a.stack,digest:a.digest});if(ts)throw ts=!1,e=Ai,Ai=null,e;return(rs&1)!==0&&e.tag!==0&&Na(),o=e.pendingLanes,(o&1)!==0?e===Li?uo++:(uo=0,Li=e):uo=0,gr(),null}function Na(){if(Sr!==null){var e=Ke(rs),t=gn.transition,n=me;try{if(gn.transition=null,me=16>e?16:e,Sr===null)var r=!1;else{if(e=Sr,Sr=null,rs=0,(Pe&6)!==0)throw Error(g(331));var a=Pe;for(Pe|=4,Q=e.current;Q!==null;){var o=Q,i=o.child;if((Q.flags&16)!==0){var l=o.deletions;if(l!==null){for(var d=0;d<l.length;d++){var f=l[d];for(Q=f;Q!==null;){var _=Q;switch(_.tag){case 0:case 11:case 15:lo(8,_,o)}var I=_.child;if(I!==null)I.return=_,Q=I;else for(;Q!==null;){_=Q;var E=_.sibling,W=_.return;if(xc(_),_===f){Q=null;break}if(E!==null){E.return=W,Q=E;break}Q=W}}}var Y=o.alternate;if(Y!==null){var V=Y.child;if(V!==null){Y.child=null;do{var st=V.sibling;V.sibling=null,V=st}while(V!==null)}}Q=o}}if((o.subtreeFlags&2064)!==0&&i!==null)i.return=o,Q=i;else e:for(;Q!==null;){if(o=Q,(o.flags&2048)!==0)switch(o.tag){case 0:case 11:case 15:lo(9,o,o.return)}var h=o.sibling;if(h!==null){h.return=o.return,Q=h;break e}Q=o.return}}var p=e.current;for(Q=p;Q!==null;){i=Q;var y=i.child;if((i.subtreeFlags&2064)!==0&&y!==null)y.return=i,Q=y;else e:for(i=p;Q!==null;){if(l=Q,(l.flags&2048)!==0)try{switch(l.tag){case 0:case 11:case 15:Jo(9,l)}}catch(X){at(l,l.return,X)}if(l===i){Q=null;break e}var R=l.sibling;if(R!==null){R.return=l.return,Q=R;break e}Q=l.return}}if(Pe=a,gr(),re&&typeof re.onPostCommitFiberRoot=="function")try{re.onPostCommitFiberRoot(K,e)}catch{}r=!0}return r}finally{me=n,gn.transition=t}}return!1}function Vc(e,t,n){t=_a(n,t),t=mc(e,t,1),e=fr(e,t,1),t=qt(),e!==null&&(Ge(e,1,t),Zt(e,t))}function at(e,t,n){if(e.tag===3)Vc(e,e,n);else for(;t!==null;){if(t.tag===3){Vc(t,e,n);break}else if(t.tag===1){var r=t.stateNode;if(typeof t.type.getDerivedStateFromError=="function"||typeof r.componentDidCatch=="function"&&(br===null||!br.has(r))){e=_a(n,e),e=hc(t,e,1),t=fr(t,e,1),e=qt(),t!==null&&(Ge(t,1,e),Zt(t,e));break}}t=t.return}}function Rp(e,t,n){var r=e.pingCache;r!==null&&r.delete(t),t=qt(),e.pingedLanes|=e.suspendedLanes&n,kt===e&&(Lt&n)===n&&(gt===4||gt===3&&(Lt&130023424)===Lt&&500>c()-_i?Vr(e,0):Ti|=n),Zt(e,t)}function $c(e,t){t===0&&((e.mode&1)===0?t=1:(t=se,se<<=1,(se&130023424)===0&&(se=4194304)));var n=qt();e=Zn(e,t),e!==null&&(Ge(e,t,n),Zt(e,n))}function xp(e){var t=e.memoizedState,n=0;t!==null&&(n=t.retryLane),$c(e,n)}function Mp(e,t){var n=0;switch(e.tag){case 13:var r=e.stateNode,a=e.memoizedState;a!==null&&(n=a.retryLane);break;case 19:r=e.stateNode;break;default:throw Error(g(314))}r!==null&&r.delete(t),$c(e,n)}var Xc;Xc=function(e,t,n){if(e!==null)if(e.memoizedProps!==t.pendingProps||Vt.current)Xt=!0;else{if((e.lanes&n)===0&&(t.flags&128)===0)return Xt=!1,wp(e,t,n);Xt=(e.flags&131072)!==0}else Xt=!1,et&&(t.flags&1048576)!==0&&Nl(t,Do,t.index);switch(t.lanes=0,t.tag){case 2:var r=t.type;$o(e,t),e=t.pendingProps;var a=va(t,Ot.current);Ea(t,n),a=ri(null,t,r,e,a,n);var o=ai();return t.flags|=1,typeof a=="object"&&a!==null&&typeof a.render=="function"&&a.$$typeof===void 0?(t.tag=1,t.memoizedState=null,t.updateQueue=null,$t(r)?(o=!0,Ro(t)):o=!1,t.memoizedState=a.state!==null&&a.state!==void 0?a.state:null,$s(t),a.updater=Yo,t.stateNode=a,a._reactInternals=t,di(t,r,e,n),t=hi(null,t,r,!0,o,n)):(t.tag=0,et&&o&&js(t),zt(null,t,a,n),t=t.child),t;case 16:r=t.elementType;e:{switch($o(e,t),e=t.pendingProps,a=r._init,r=a(r._payload),t.type=r,a=t.tag=Op(r),e=Tn(r,e),a){case 0:t=mi(null,t,r,e,n);break e;case 1:t=kc(null,t,r,e,n);break e;case 11:t=vc(null,t,r,e,n);break e;case 14:t=bc(null,t,r,Tn(r.type,e),n);break e}throw Error(g(306,r,""))}return t;case 0:return r=t.type,a=t.pendingProps,a=t.elementType===r?a:Tn(r,a),mi(e,t,r,a,n);case 1:return r=t.type,a=t.pendingProps,a=t.elementType===r?a:Tn(r,a),kc(e,t,r,a,n);case 3:e:{if(Ec(t),e===null)throw Error(g(387));r=t.pendingProps,o=t.memoizedState,a=o.element,Kl(e,t),Ho(t,r,null,n);var i=t.memoizedState;if(r=i.element,o.isDehydrated)if(o={element:r,isDehydrated:!1,cache:i.cache,pendingSuspenseBoundaries:i.pendingSuspenseBoundaries,transitions:i.transitions},t.updateQueue.baseState=o,t.memoizedState=o,t.flags&256){a=_a(Error(g(423)),t),t=Tc(e,t,r,n,a);break e}else if(r!==a){a=_a(Error(g(424)),t),t=Tc(e,t,r,n,a);break e}else for(on=ur(t.stateNode.containerInfo.firstChild),an=t,et=!0,En=null,n=Bl(t,null,r,n),t.child=n;n;)n.flags=n.flags&-3|4096,n=n.sibling;else{if(wa(),r===a){t=tr(e,t,n);break e}zt(e,t,r,n)}t=t.child}return t;case 5:return Gl(t),e===null&&Ws(t),r=t.type,a=t.pendingProps,o=e!==null?e.memoizedProps:null,i=a.children,Ms(r,a)?i=null:o!==null&&Ms(r,o)&&(t.flags|=32),Cc(e,t),zt(e,t,i,n),t.child;case 6:return e===null&&Ws(t),null;case 13:return _c(e,t,n);case 4:return Xs(t,t.stateNode.containerInfo),r=t.pendingProps,e===null?t.child=Ca(t,null,r,n):zt(e,t,r,n),t.child;case 11:return r=t.type,a=t.pendingProps,a=t.elementType===r?a:Tn(r,a),vc(e,t,r,a,n);case 7:return zt(e,t,t.pendingProps,n),t.child;case 8:return zt(e,t,t.pendingProps.children,n),t.child;case 12:return zt(e,t,t.pendingProps.children,n),t.child;case 10:e:{if(r=t.type._context,a=t.pendingProps,o=t.memoizedProps,i=a.value,Ve(Fo,r._currentValue),r._currentValue=i,o!==null)if(kn(o.value,i)){if(o.children===a.children&&!Vt.current){t=tr(e,t,n);break e}}else for(o=t.child,o!==null&&(o.return=t);o!==null;){var l=o.dependencies;if(l!==null){i=o.child;for(var d=l.firstContext;d!==null;){if(d.context===r){if(o.tag===1){d=er(-1,n&-n),d.tag=2;var f=o.updateQueue;if(f!==null){f=f.shared;var _=f.pending;_===null?d.next=d:(d.next=_.next,_.next=d),f.pending=d}}o.lanes|=n,d=o.alternate,d!==null&&(d.lanes|=n),Ys(o.return,n,t),l.lanes|=n;break}d=d.next}}else if(o.tag===10)i=o.type===t.type?null:o.child;else if(o.tag===18){if(i=o.return,i===null)throw Error(g(341));i.lanes|=n,l=i.alternate,l!==null&&(l.lanes|=n),Ys(i,n,t),i=o.sibling}else i=o.child;if(i!==null)i.return=o;else for(i=o;i!==null;){if(i===t){i=null;break}if(o=i.sibling,o!==null){o.return=i.return,i=o;break}i=i.return}o=i}zt(e,t,a.children,n),t=t.child}return t;case 9:return a=t.type,r=t.pendingProps.children,Ea(t,n),a=mn(a),r=r(a),t.flags|=1,zt(e,t,r,n),t.child;case 14:return r=t.type,a=Tn(r,t.pendingProps),a=Tn(r.type,a),bc(e,t,r,a,n);case 15:return Sc(e,t,t.type,t.pendingProps,n);case 17:return r=t.type,a=t.pendingProps,a=t.elementType===r?a:Tn(r,a),$o(e,t),t.tag=1,$t(r)?(e=!0,Ro(t)):e=!1,Ea(t,n),pc(t,r,a),di(t,r,a,n),hi(null,t,r,!0,e,n);case 19:return Lc(e,t,n);case 22:return wc(e,t,n)}throw Error(g(156,t.tag))};function Jc(e,t){return u(e,t)}function Dp(e,t,n,r){this.tag=e,this.key=n,this.sibling=this.child=this.return=this.stateNode=this.type=this.elementType=null,this.index=0,this.ref=null,this.pendingProps=t,this.dependencies=this.memoizedState=this.updateQueue=this.memoizedProps=null,this.mode=r,this.subtreeFlags=this.flags=0,this.deletions=null,this.childLanes=this.lanes=0,this.alternate=null}function yn(e,t,n,r){return new Dp(e,t,n,r)}function Mi(e){return e=e.prototype,!(!e||!e.isReactComponent)}function Op(e){if(typeof e=="function")return Mi(e)?1:0;if(e!=null){if(e=e.$$typeof,e===ln)return 11;if(e===ut)return 14}return 2}function kr(e,t){var n=e.alternate;return n===null?(n=yn(e.tag,t,e.key,e.mode),n.elementType=e.elementType,n.type=e.type,n.stateNode=e.stateNode,n.alternate=e,e.alternate=n):(n.pendingProps=t,n.type=e.type,n.flags=0,n.subtreeFlags=0,n.deletions=null),n.flags=e.flags&14680064,n.childLanes=e.childLanes,n.lanes=e.lanes,n.child=e.child,n.memoizedProps=e.memoizedProps,n.memoizedState=e.memoizedState,n.updateQueue=e.updateQueue,t=e.dependencies,n.dependencies=t===null?null:{lanes:t.lanes,firstContext:t.firstContext},n.sibling=e.sibling,n.index=e.index,n.ref=e.ref,n}function is(e,t,n,r,a,o){var i=2;if(r=e,typeof e=="function")Mi(e)&&(i=1);else if(typeof e=="string")i=5;else e:switch(e){case Te:return Xr(n.children,a,o,t);case Je:i=8,a|=8;break;case Me:return e=yn(12,n,t,a|2),e.elementType=Me,e.lanes=o,e;case pe:return e=yn(13,n,t,a),e.elementType=pe,e.lanes=o,e;case tn:return e=yn(19,n,t,a),e.elementType=tn,e.lanes=o,e;case Qe:return ls(n,a,o,t);default:if(typeof e=="object"&&e!==null)switch(e.$$typeof){case vt:i=10;break e;case It:i=9;break e;case ln:i=11;break e;case ut:i=14;break e;case tt:i=16,r=null;break e}throw Error(g(130,e==null?e:typeof e,""))}return t=yn(i,n,t,a),t.elementType=e,t.type=r,t.lanes=o,t}function Xr(e,t,n,r){return e=yn(7,e,r,t),e.lanes=n,e}function ls(e,t,n,r){return e=yn(22,e,r,t),e.elementType=Qe,e.lanes=n,e.stateNode={isHidden:!1},e}function Di(e,t,n){return e=yn(6,e,null,t),e.lanes=n,e}function Oi(e,t,n){return t=yn(4,e.children!==null?e.children:[],e.key,t),t.lanes=n,t.stateNode={containerInfo:e.containerInfo,pendingChildren:null,implementation:e.implementation},t}function Bp(e,t,n,r,a){this.tag=t,this.containerInfo=e,this.finishedWork=this.pingCache=this.current=this.pendingChildren=null,this.timeoutHandle=-1,this.callbackNode=this.pendingContext=this.context=null,this.callbackPriority=0,this.eventTimes=Be(0),this.expirationTimes=Be(-1),this.entangledLanes=this.finishedLanes=this.mutableReadLanes=this.expiredLanes=this.pingedLanes=this.suspendedLanes=this.pendingLanes=0,this.entanglements=Be(0),this.identifierPrefix=r,this.onRecoverableError=a,this.mutableSourceEagerHydrationData=null}function Bi(e,t,n,r,a,o,i,l,d){return e=new Bp(e,t,n,l,d),t===1?(t=1,o===!0&&(t|=8)):t=0,o=yn(3,null,null,t),e.current=o,o.stateNode=e,o.memoizedState={element:r,isDehydrated:n,cache:null,transitions:null,pendingSuspenseBoundaries:null},$s(o),e}function Fp(e,t,n){var r=3<arguments.length&&arguments[3]!==void 0?arguments[3]:null;return{$$typeof:$,key:r==null?null:""+r,children:e,containerInfo:t,implementation:n}}function Zc(e){if(!e)return hr;e=e._reactInternals;e:{if(dn(e)!==e||e.tag!==1)throw Error(g(170));var t=e;do{switch(t.tag){case 3:t=t.stateNode.context;break e;case 1:if($t(t.type)){t=t.stateNode.__reactInternalMemoizedMergedChildContext;break e}}t=t.return}while(t!==null);throw Error(g(171))}if(e.tag===1){var n=e.type;if($t(n))return Al(e,n,t)}return t}function ed(e,t,n,r,a,o,i,l,d){return e=Bi(n,r,!0,e,a,o,i,l,d),e.context=Zc(null),n=e.current,r=qt(),a=wr(n),o=er(r,a),o.callback=t??null,fr(n,o,a),e.current.lanes=a,Ge(e,a,r),Zt(e,r),e}function cs(e,t,n,r){var a=t.current,o=qt(),i=wr(a);return n=Zc(n),t.context===null?t.context=n:t.pendingContext=n,t=er(o,i),t.payload={element:e},r=r===void 0?null:r,r!==null&&(t.callback=r),e=fr(a,t,i),e!==null&&(Ln(e,a,i,o),jo(e,a,i)),i}function ds(e){if(e=e.current,!e.child)return null;switch(e.child.tag){case 5:return e.child.stateNode;default:return e.child.stateNode}}function td(e,t){if(e=e.memoizedState,e!==null&&e.dehydrated!==null){var n=e.retryLane;e.retryLane=n!==0&&n<t?n:t}}function Fi(e,t){td(e,t),(e=e.alternate)&&td(e,t)}function Kp(){return null}var nd=typeof reportError=="function"?reportError:function(e){console.error(e)};function Ki(e){this._internalRoot=e}ps.prototype.render=Ki.prototype.render=function(e){var t=this._internalRoot;if(t===null)throw Error(g(409));cs(e,t,null,null)},ps.prototype.unmount=Ki.prototype.unmount=function(){var e=this._internalRoot;if(e!==null){this._internalRoot=null;var t=e.containerInfo;Yr(function(){cs(null,e,null,null)}),t[Vn]=null}};function ps(e){this._internalRoot=e}ps.prototype.unstable_scheduleHydration=function(e){if(e){var t=Rt();e={blockedOn:null,target:e,priority:t};for(var n=0;n<dt.length&&t!==0&&t<dt[n].priority;n++);dt.splice(n,0,e),n===0&&Wt(e)}};function ji(e){return!(!e||e.nodeType!==1&&e.nodeType!==9&&e.nodeType!==11)}function us(e){return!(!e||e.nodeType!==1&&e.nodeType!==9&&e.nodeType!==11&&(e.nodeType!==8||e.nodeValue!==" react-mount-point-unstable "))}function rd(){}function jp(e,t,n,r,a){if(a){if(typeof r=="function"){var o=r;r=function(){var f=ds(i);o.call(f)}}var i=ed(t,r,e,0,null,!1,!1,"",rd);return e._reactRootContainer=i,e[Vn]=i.current,Va(e.nodeType===8?e.parentNode:e),Yr(),i}for(;a=e.lastChild;)e.removeChild(a);if(typeof r=="function"){var l=r;r=function(){var f=ds(d);l.call(f)}}var d=Bi(e,0,!1,null,null,!1,!1,"",rd);return e._reactRootContainer=d,e[Vn]=d.current,Va(e.nodeType===8?e.parentNode:e),Yr(function(){cs(t,d,n,r)}),d}function ms(e,t,n,r,a){var o=n._reactRootContainer;if(o){var i=o;if(typeof a=="function"){var l=a;a=function(){var d=ds(i);l.call(d)}}cs(t,i,e,a)}else i=jp(n,t,e,a,r);return ds(i)}mt=function(e){switch(e.tag){case 3:var t=e.stateNode;if(t.current.memoizedState.isDehydrated){var n=U(t.pendingLanes);n!==0&&(Ze(t,n|1),Zt(t,c()),(Pe&6)===0&&(Ia=c()+500,gr()))}break;case 13:Yr(function(){var r=Zn(e,1);if(r!==null){var a=qt();Ln(r,e,1,a)}}),Fi(e,1)}},Pt=function(e){if(e.tag===13){var t=Zn(e,134217728);if(t!==null){var n=qt();Ln(t,e,134217728,n)}Fi(e,134217728)}},rn=function(e){if(e.tag===13){var t=wr(e),n=Zn(e,t);if(n!==null){var r=qt();Ln(n,e,t,r)}Fi(e,t)}},Rt=function(){return me},ct=function(e,t){var n=me;try{return me=e,t()}finally{me=n}},Ba=function(e,t,n){switch(t){case"input":if(_r(e,n),t=n.name,n.type==="radio"&&t!=null){for(n=e;n.parentNode;)n=n.parentNode;for(n=n.querySelectorAll("input[name="+JSON.stringify(""+t)+'][type="radio"]'),t=0;t<n.length;t++){var r=n[t];if(r!==e&&r.form===e.form){var a=No(r);if(!a)throw Error(g(90));Pa(r),_r(r,a)}}}break;case"textarea":fn(e,n);break;case"select":t=n.value,t!=null&&rr(e,!!n.multiple,t,!1)}},Fa=Pi,lt=Yr;var Hp={usingClientEntryPoint:!1,Events:[Ja,ya,No,bo,St,Pi]},mo={findFiberByHostInstance:jr,bundleType:0,version:"18.3.1",rendererPackageName:"react-dom"},Gp={bundleType:mo.bundleType,version:mo.version,rendererPackageName:mo.rendererPackageName,rendererConfig:mo.rendererConfig,overrideHookState:null,overrideHookStateDeletePath:null,overrideHookStateRenamePath:null,overrideProps:null,overridePropsDeletePath:null,overridePropsRenamePath:null,setErrorHandler:null,setSuspenseHandler:null,scheduleUpdate:null,currentDispatcherRef:L.ReactCurrentDispatcher,findHostInstanceByFiber:function(e){return e=ir(e),e===null?null:e.stateNode},findFiberByHostInstance:mo.findFiberByHostInstance||Kp,findHostInstancesForRefresh:null,scheduleRefresh:null,scheduleRoot:null,setRefreshHandler:null,getCurrentFiber:null,reconcilerVersion:"18.3.1-next-f1338f8080-20240426"};if(typeof __REACT_DEVTOOLS_GLOBAL_HOOK__<"u"){var hs=__REACT_DEVTOOLS_GLOBAL_HOOK__;if(!hs.isDisabled&&hs.supportsFiber)try{K=hs.inject(Gp),re=hs}catch{}}return en.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED=Hp,en.createPortal=function(e,t){var n=2<arguments.length&&arguments[2]!==void 0?arguments[2]:null;if(!ji(t))throw Error(g(200));return Fp(e,t,null,n)},en.createRoot=function(e,t){if(!ji(e))throw Error(g(299));var n=!1,r="",a=nd;return t!=null&&(t.unstable_strictMode===!0&&(n=!0),t.identifierPrefix!==void 0&&(r=t.identifierPrefix),t.onRecoverableError!==void 0&&(a=t.onRecoverableError)),t=Bi(e,1,!1,null,null,n,!1,r,a),e[Vn]=t.current,Va(e.nodeType===8?e.parentNode:e),new Ki(t)},en.findDOMNode=function(e){if(e==null)return null;if(e.nodeType===1)return e;var t=e._reactInternals;if(t===void 0)throw typeof e.render=="function"?Error(g(188)):(e=Object.keys(e).join(","),Error(g(268,e)));return e=ir(t),e=e===null?null:e.stateNode,e},en.flushSync=function(e){return Yr(e)},en.hydrate=function(e,t,n){if(!us(t))throw Error(g(200));return ms(null,e,t,!0,n)},en.hydrateRoot=function(e,t,n){if(!ji(e))throw Error(g(405));var r=n!=null&&n.hydratedSources||null,a=!1,o="",i=nd;if(n!=null&&(n.unstable_strictMode===!0&&(a=!0),n.identifierPrefix!==void 0&&(o=n.identifierPrefix),n.onRecoverableError!==void 0&&(i=n.onRecoverableError)),t=ed(t,null,e,1,n??null,a,!1,o,i),e[Vn]=t.current,Va(e),r)for(e=0;e<r.length;e++)n=r[e],a=n._getVersion,a=a(n._source),t.mutableSourceEagerHydrationData==null?t.mutableSourceEagerHydrationData=[n,a]:t.mutableSourceEagerHydrationData.push(n,a);return new ps(t)},en.render=function(e,t,n){if(!us(t))throw Error(g(200));return ms(null,e,t,!1,n)},en.unmountComponentAtNode=function(e){if(!us(e))throw Error(g(40));return e._reactRootContainer?(Yr(function(){ms(null,null,e,!1,function(){e._reactRootContainer=null,e[Vn]=null})}),!0):!1},en.unstable_batchedUpdates=Pi,en.unstable_renderSubtreeIntoContainer=function(e,t,n,r){if(!us(n))throw Error(g(200));if(e==null||e._reactInternals===void 0)throw Error(g(38));return ms(e,t,n,!1,r)},en.version="18.3.1-next-f1338f8080-20240426",en}var pd;function $p(){if(pd)return Wi.exports;pd=1;function S(){if(!(typeof __REACT_DEVTOOLS_GLOBAL_HOOK__>"u"||typeof __REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE!="function"))try{__REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE(S)}catch(D){console.error(D)}}return S(),Wi.exports=Vp(),Wi.exports}var ud;function Xp(){if(ud)return gs;ud=1;var S=$p();return gs.createRoot=S.createRoot,gs.hydrateRoot=S.hydrateRoot,gs}var md=Xp(),Tr=qi();const Jp="/",Zp="/architectures",eu="/ontology",tu="/benchmarks",nu="/benchmarks/",ru="v1.83.0",au="/version.json",hd="v",ou="footerCeVersion",su={cache:"no-cache"},iu=({currentRoute:S})=>{const[D,g]=Tr.useState(ru);Tr.useEffect(()=>{fetch(au,su).then(b=>b.ok?b.json():null).then(b=>{const M=b&&(b.tag_name||b.version);if(M){const j=M.startsWith(hd)?M:`${hd}${M}`;g(j);const T=document.getElementById(ou);T&&(T.textContent=`CodeExplorer CLI & Extension ${j}`)}}).catch(()=>{})},[S]);const z=S===Jp,H=S===Zp,ae=S===eu,fe=S===tu||S.startsWith(nu),ye=z?"#playground":"/#playground",ne=z?"#ai-mcp":"/#ai-mcp",ge=z?"#gates":"/#gates",Ce=z?"#install":"/#install";return A.jsx("header",{className:"slab-header",children:A.jsxs("nav",{className:"wrap slab-bar","aria-label":"Primary",children:[A.jsxs("a",{href:"/",className:"brand-lockup","aria-label":"CodeExplorer home",children:[A.jsx("span",{className:"brand-mark",children:"ce"}),A.jsx("span",{children:"CodeExplorer"})]}),A.jsxs("ul",{className:"slab-links",children:[A.jsx("li",{className:"slab-anchor-item",children:A.jsx("a",{href:ye,children:"Query"})}),A.jsx("li",{className:"slab-anchor-item",children:A.jsx("a",{href:ne,children:"MCP"})}),A.jsx("li",{className:"slab-anchor-item",children:A.jsx("a",{href:ge,children:"Gates"})}),A.jsx("li",{className:"slab-anchor-item",children:A.jsx("a",{href:Ce,children:"Install"})}),A.jsx("li",{className:"slab-sep","aria-hidden":"true"}),A.jsx("li",{children:A.jsx("a",{href:"/architectures","aria-current":H?"page":void 0,children:"Architectures"})}),A.jsx("li",{children:A.jsx("a",{href:"/ontology","aria-current":ae?"page":void 0,children:"Ontology"})}),A.jsx("li",{children:A.jsx("a",{href:"/benchmarks","aria-current":fe?"page":void 0,children:"Benchmarks"})})]}),A.jsxs("div",{className:"slab-actions",children:[A.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/releases/latest",target:"_blank",rel:"noopener",className:"btn",id:"heroReleaseBadge","aria-label":"Latest CodeExplorer release",children:D}),A.jsx("a",{href:"https://github.com/vmikhailov/code-explorer",target:"_blank",rel:"noopener",className:"btn",children:"GitHub"})]})]})})},lu="CodeExplorer CLI & Extension v1.83.0",cu=({versionLabel:S=lu})=>A.jsx("footer",{className:"mast-footer",children:A.jsxs("div",{className:"wrap",children:[A.jsxs("div",{className:"mast-top",children:[A.jsxs("div",{className:"mast-col",children:[A.jsxs("a",{href:"/",className:"brand-lockup",children:[A.jsx("span",{className:"brand-mark",children:"ce"}),A.jsx("span",{children:"CodeExplorer"})]}),A.jsx("p",{className:"panel-copy",children:"Make AI coding agents production-safe. Live 5-layer polyglot architecture intelligence in VS Code, CLI, and MCP."})]}),A.jsxs("div",{className:"mast-col",children:[A.jsx("span",{className:"mast-heading",children:"Distribution"}),A.jsxs("ul",{className:"mast-list",children:[A.jsx("li",{children:A.jsx("a",{href:"https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode",target:"_blank",rel:"noopener",children:"VS Code Marketplace"})}),A.jsx("li",{children:A.jsx("a",{href:"https://open-vsx.org/extension/vmikhailov/code-explorer-vscode",target:"_blank",rel:"noopener",children:"Open VSX Registry"})}),A.jsx("li",{children:A.jsx("a",{href:"https://github.com/vmikhailov/homebrew-tap",target:"_blank",rel:"noopener",children:"Homebrew Tap"})}),A.jsx("li",{children:A.jsx("a",{href:"https://www.nuget.org/packages/codeexplorer.cli",target:"_blank",rel:"noopener",children:"NuGet Global Tool"})})]})]}),A.jsxs("div",{className:"mast-col",children:[A.jsx("span",{className:"mast-heading",children:"Architecture & Docs"}),A.jsxs("ul",{className:"mast-list",children:[A.jsx("li",{children:A.jsx("a",{href:"/architectures",children:"Architecture Encyclopedia"})}),A.jsx("li",{children:A.jsx("a",{href:"/ontology",children:"Graph Ontology Map"})}),A.jsx("li",{children:A.jsx("a",{href:"/benchmarks",children:"Competitor Benchmarks"})}),A.jsx("li",{children:A.jsx("a",{href:"https://passport.code-explorer.app",target:"_blank",rel:"noopener",className:"tone-accent",children:"CodePassport"})}),A.jsx("li",{children:A.jsx("a",{href:"/presentation.html",target:"_blank",rel:"noopener",children:"Interactive Presentation"})}),A.jsx("li",{children:A.jsx("a",{href:"/llms.txt",target:"_blank",rel:"noopener",children:"llms.txt for Agents"})})]})]}),A.jsxs("div",{className:"mast-col",children:[A.jsx("span",{className:"mast-heading",children:"Project & Legal"}),A.jsxs("ul",{className:"mast-list",children:[A.jsx("li",{children:A.jsx("a",{href:"https://github.com/vmikhailov/code-explorer",target:"_blank",rel:"noopener",children:"GitHub Repository"})}),A.jsx("li",{children:A.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/releases",target:"_blank",rel:"noopener",children:"Release Notes"})}),A.jsx("li",{children:A.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/issues",target:"_blank",rel:"noopener",children:"Issue Tracker"})}),A.jsx("li",{children:A.jsx("a",{href:"https://github.com/vmikhailov/code-explorer/blob/main/LICENSE",target:"_blank",rel:"noopener",children:"MIT License"})}),A.jsx("li",{children:A.jsx("a",{href:"/privacy",children:"Privacy Policy"})})]})]})]}),A.jsxs("div",{className:"mast-bottom",children:[A.jsx("span",{children:"Built by Viacheslav Mikhailov, 100% local execution"}),A.jsx("span",{className:"measured",id:"footerCeVersion",children:S})]})]})}),du=({currentRoute:S,children:D,afterFooter:g})=>A.jsxs(A.Fragment,{children:[A.jsx(iu,{currentRoute:S}),D,A.jsx(cu,{}),g]}),jn={"/":{routePath:"/",pageKind:"home",title:"CodeExplorer (ce): Understand, Query &amp; Guard Your Codebase Architecture",description:"Compile your polyglot codebase into a fast local SQLite graph. Get always-accurate architecture maps in VS Code, instant architectural memory for AI agents (MCP), and automated architectural guardrails in CI.",canonical:"https://code-explorer.app/",ogUrl:"https://code-explorer.app/",ogTitle:"CodeExplorer (ce): Understand, Query &amp; Guard Your Codebase Architecture",ogDescription:"Always-accurate architecture diagrams from real code, instant MCP memory for AI coding agents, and automated Semantic Invariant Gates in CI, powered by a local SQLite graph.",mainHtml:`<!-- Stage / Workbench Hero -->
    <section class="hero-stage" id="cockpit">
      <div class="wrap">
        <div class="hero-top">
          <div>
            <h1 class="hero-title">Understand, query, and guard your architecture. <span class="tone-accent">For you and your agent.</span></h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              CodeExplorer compiles your repositories (C#, Java, Kotlin, TS/JS, Python, Go, Rust, SQL) into one fast local SQLite graph: giving you live architecture diagrams that never go stale, deterministic memory for your AI coding agents (MCP), and automated architectural rules in CI.
            </p>
            <div class="hero-controls">
              <a href="#install" class="btn btn--framed">Installation</a>
              <a href="/benchmarks" class="btn">Benchmarks</a>
              <a href="/presentation" target="_blank" rel="noopener" class="btn">Presentation</a>
            </div>
          </div>
        </div>

        <!-- 3 Clear Questions / Pillars Strip -->
        <div class="panels panels--3" style="margin-bottom: var(--space-xl);">
          <div class="panel">
            <div class="panel-head">
              <h2 class="panel-title">1. Understand It Yourself</h2>
              <code class="tone-accent">VS Code</code>
            </div>
            <p class="panel-copy">
              Always-real diagrams of microservices, REST/gRPC APIs, message queues, and SQL tables built straight from code. Never redraw stale schemas by hand when code changes.
            </p>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h2 class="panel-title">2. Give Memory to Your AI</h2>
              <code class="tone-cool">MCP Server</code>
            </div>
            <p class="panel-copy">
              Attach <code>ce mcp</code> to Claude Code or Cursor. Your AI agent queries exact call chains and blast radius in milliseconds instead of burning tokens on blind <code>grep</code>.
            </p>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h2 class="panel-title">3. Guard Architecture in CI</h2>
              <code class="tone-ok">SIG + Ratchet</code>
            </div>
            <p class="panel-copy">
              Enforce custom and built-in architectural rules on every PR. Freeze existing legacy tech debt as a baseline ratchet (<code>ce gate ratchet</code>) so CI stays green while blocking new violations.
            </p>
          </div>
        </div>

        <!-- Interactive VS Code Architecture Cockpit Workbench -->
        <div class="workbench">
          <div class="workbench-bar">
            <div class="workbench-tabs" role="tablist" aria-label="Architecture Cockpit views">
              <button type="button" class="tab-btn active" onclick="switchShowcase(0, this)">1. System Map</button>
              <button type="button" class="tab-btn" onclick="switchShowcase(1, this)">2. Blast Radius</button>
              <button type="button" class="tab-btn" onclick="switchShowcase(2, this)">3. Call Flows</button>
            </div>
            <span class="workbench-readout">VS Code Webview · <code id="showcasePathLabel">all_services.png</code></span>
          </div>

          <div class="cockpit-viewport" onclick="openLightbox()" role="button" tabindex="0" aria-label="Enlarge Architecture Cockpit screenshot" onkeydown="if(event.key==='Enter'||event.key===' '){event.preventDefault();openLightbox();}">
            <img id="showcaseImage" src="all_services.png" alt="CodeExplorer Interactive Architecture Cockpit in VS Code" fetchpriority="high" width="1920" height="1080">
          </div>

          <div class="cockpit-footer">
            <p class="cockpit-caption" id="showcaseDescription">
              <strong>Full System Architecture Map:</strong> Every microservice, REST/gRPC endpoint, RabbitMQ/Kafka topic, and database table rendered automatically from your code. Double-click any service to drill into internal call chains.
            </p>
            <div class="cockpit-metrics" id="showcasePills">
              <span class="metric-chip metric-chip--accent">Always Synced With Code</span>
              <span class="metric-chip metric-chip--ok">Sub-10ms SQLite Queries</span>
              <span class="metric-chip">Click image to enlarge</span>
            </div>
          </div>
        </div>

        <!-- 6 Core Capabilities Cut as Console Hairline Panels -->
        <div class="panels panels--3" id="features" style="margin-top: var(--space-2xl);">
          <article class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Cross-Service Topology</h3>
              <code class="tone-accent">L4 + L5</code>
            </div>
            <p class="panel-copy">
              Automatically links outgoing <code>HttpClient</code>, <code>Feign</code>, <code>axios</code>, and gRPC calls to target API controllers, and connects RabbitMQ/Kafka publishers to consumer handlers.
            </p>
            <code class="panel-code">CALLS_ENDPOINT, PUBLISHES, CONSUMES</code>
          </article>

          <article class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Transitive Graph Contraction</h3>
              <code class="tone-cool">Cockpit</code>
            </div>
            <p class="panel-copy">
              Select any subset of services in VS Code and collapse intermediate pass-through nodes while preserving indirect dependency paths and edge provenance.
            </p>
            <code class="panel-code">Interactive C1 to C4 drill-down</code>
          </article>

          <article class="panel">
            <div class="panel-head">
              <h3 class="panel-title">End-to-End SQL Data Lineage</h3>
              <code class="tone-ok">Lineage</code>
            </div>
            <p class="panel-copy">
              Extracts Dapper, Entity Framework, Hibernate, JDBC, and raw SQL queries to map exact <code>READS_FROM</code> and <code>WRITES_TO</code> edges down to database tables.
            </p>
            <code class="panel-code">ce lineage &lt;table-or-endpoint&gt;</code>
          </article>

          <article class="panel">
            <div class="panel-head">
              <h3 class="panel-title">OpenCypher Query Engine</h3>
              <code class="tone-accent">Cypher</code>
            </div>
            <p class="panel-copy">
              Query your entire codebase in milliseconds using standard OpenCypher pattern matching, variable-length paths (<code>*1..6</code>), aggregations, and comprehensions.
            </p>
            <code class="panel-code">ce query &quot;MATCH (m:Method)...&quot;</code>
          </article>

          <article class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Semantic Invariant Gates</h3>
              <code class="tone-bad">CI Gate</code>
            </div>
            <p class="panel-copy">
              Codify architectural boundaries as declarative YAML rules. Block forbidden layer couplings, god classes, and unauthenticated endpoints before merge.
            </p>
            <code class="panel-code">ce gate check --ratchet</code>
          </article>

          <article class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Zero-Config MCP Server</h3>
              <code class="tone-cool">stdio MCP</code>
            </div>
            <p class="panel-copy">
              Exposes 20+ specialized graph intelligence tools over <code>stdio</code> to Claude Code, Cursor, Windsurf, and GitHub Copilot with sub-10ms SQLite reads.
            </p>
            <code class="panel-code">ce mcp</code>
          </article>
        </div>
      </div>
    </section>

    <!-- Section 1: Interactive OpenCypher Query Playground -->
    <section class="band" id="playground">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">Query your architecture with OpenCypher.</h2>
          <p class="band-lede">
            Stop grepping through hundreds of files. Express structural, semantic, and cross-service questions in declarative OpenCypher and get deterministic tabular answers in milliseconds.
          </p>
        </div>

        <div class="workbench">
          <div class="workbench-bar">
            <div class="workbench-tabs" role="tablist" aria-label="OpenCypher query presets">
              <button type="button" class="preset-btn active" onclick="loadQueryPreset('blast_radius', this)">Blast Radius (*1..4)</button>
              <button type="button" class="preset-btn" onclick="loadQueryPreset('cqrs_pipeline', this)">CQRS Command to Handler</button>
              <button type="button" class="preset-btn" onclick="loadQueryPreset('api_to_db', this)">Endpoint to SQL Lineage</button>
              <button type="button" class="preset-btn" onclick="loadQueryPreset('cyclic_deps', this)">Cross-Module Cycles</button>
            </div>
            <button type="button" class="btn" id="runQueryBtn" onclick="runQueryAnimation()">Run Query</button>
          </div>

          <div class="split-bench">
            <div class="bench-pane">
              <div class="bench-pane-head">
                <span>OpenCypher Editor</span>
                <span class="measured" id="queryPresetTag">blast_radius.cypher</span>
              </div>
              <pre class="code-view" id="queryCodeArea"></pre>
              <div class="bench-meta-row">
                <span id="queryExplanation">Finds all upstream callers within 4 hops that can impact PaymentProcessor.ChargeAsync.</span>
                <button type="button" class="btn" onclick="copyCurrentQuery()">Copy CLI Command</button>
              </div>
            </div>

            <div class="bench-pane">
              <div class="bench-pane-head">
                <span>Execution Result (SQLite Graph Engine)</span>
                <span class="measured tone-ok" id="queryTimingBadge">4 rows · 3.8 ms</span>
              </div>
              <div class="data-table-wrap" id="queryResultsArea"></div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Section 2: Native CLI & Model Context Protocol (MCP) Server -->
    <section class="band" id="ai-mcp">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">One graph for engineers and AI agents.</h2>
          <p class="band-lede">
            CodeExplorer indexes your repository and stores the graph locally. Use the fast native CLI in your terminal, or attach the built-in MCP server to Claude Code, Cursor, Windsurf, or Antigravity.
          </p>
        </div>

        <div class="panels panels--2">
          <div class="panel panel--sunken">
            <div class="panel-head">
              <h3 class="panel-title">Terminal CLI Workflow</h3>
              <code class="tone-accent">ce scan, ce query, ce gate</code>
            </div>
            <p class="panel-copy">
              Incremental file-hash caching re-parses only modified files in milliseconds. Output clean tables for humans or compact JSON/SARIF for automation.
            </p>
            <pre class="code-view" style="margin-top: var(--space-sm);"><span class="syn-cmt"># 1. Initialize and index polyglot workspace</span>
<span class="tone-accent">$</span> ce init &amp;&amp; ce scan
<span class="tone-ok">Indexed 452 modules, 18,420 types, 142,810 edges in 3.4s</span>

<span class="syn-cmt"># 2. Inspect cross-service blast radius</span>
<span class="tone-accent">$</span> ce impact OrdersController.CreateOrder --depth 4

<span class="syn-cmt"># 3. Enforce Semantic Invariant Gates</span>
<span class="tone-accent">$</span> ce gate check --ratchet
<span class="tone-ok">PASS: 14 gates verified (0 net-new violations)</span></pre>
          </div>

          <div class="panel panel--sunken">
            <div class="panel-head">
              <h3 class="panel-title">Model Context Protocol (MCP) Server</h3>
              <button type="button" class="btn" onclick="copyMCPConfig()">Copy MCP JSON</button>
            </div>
            <p class="panel-copy">
              Add CodeExplorer to your agent’s <code>mcpServers</code> configuration. Gives agents <code>get_architecture_map</code>, <code>find_symbol</code>, <code>get_call_chain</code>, <code>analyze_code_impact</code>, and <code>inspect_data_lineage</code>.
            </p>
            <pre class="code-view" id="mcpConfigSnippet" style="margin-top: var(--space-sm);">{
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
    </section>

    <!-- Section 3: Semantic Invariant Gates (SIG) & Baseline Ratchet -->
    <section class="band" id="gates">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">Semantic Invariant Gates: deterministic guardrails for AI agents.</h2>
          <p class="band-lede">
            Linters check syntax inside a single file. Semantic Invariant Gates (SIG) verify architectural invariants across your entire 5-layer graph, accompanied by a cryptographic baseline ratchet so legacy debt only moves downward.
          </p>
        </div>

        <!-- Five Orthogonal Invariant Dimensions -->
        <div class="panels panels--4" style="margin-bottom: var(--space-xl);">
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Boundaries</h3>
              <code class="tone-accent">L2 / L3</code>
            </div>
            <p class="panel-copy">Forbid Domain types from referencing Infrastructure, ORM, or transport assemblies.</p>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Topology</h3>
              <code class="tone-cool">L4 / L5</code>
            </div>
            <p class="panel-copy">Prevent synchronous REST call chains deeper than N hops or shared-table coupling across services.</p>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">AST Visitor &amp; CFG</h3>
              <code class="tone-ok">L1 / L2</code>
            </div>
            <p class="panel-copy">Detect async-over-sync deadlocks (<code>.Result</code>), missing cancellation tokens, or high cyclomatic complexity.</p>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Baseline Ratchet</h3>
              <code class="tone-bad">CI Lock</code>
            </div>
            <p class="panel-copy">Fingerprint existing legacy violations so brownfield teams block new regressions on day one.</p>
          </div>
        </div>

        <!-- Interactive SIG Gate Workbench -->
        <div class="workbench">
          <div class="workbench-bar">
            <div class="workbench-tabs" role="tablist" aria-label="Semantic Invariant Gate presets">
              <button type="button" class="preset-btn active" onclick="loadGatePreset('boundaries', this)">1. Clean Architecture Boundary</button>
              <button type="button" class="preset-btn" onclick="loadGatePreset('topology', this)">2. Shared SQL Table Coupling</button>
              <button type="button" class="preset-btn" onclick="loadGatePreset('visitor', this)">3. Async Deadlock (.Result)</button>
              <button type="button" class="preset-btn" onclick="loadGatePreset('literal', this)">4. Hardcoded Secrets &amp; Literals</button>
              <button type="button" class="preset-btn" onclick="loadGatePreset('ratchet', this)">5. Legacy Baseline Ratchet</button>
            </div>
          </div>

          <div class="split-bench">
            <div class="bench-pane">
              <div class="bench-pane-head">
                <span id="gateFileLabel">.codeexplorer/gates/domain-purity.yaml</span>
                <span class="measured" id="gateDimensionBadge">Dimension: Boundaries</span>
              </div>
              <pre class="code-view" id="gateYamlArea"></pre>
              <div class="bench-meta-row">
                <span id="gateDescriptionText">Blocks any type inside a Domain module from depending on Infrastructure or EF Core types.</span>
                <button type="button" class="btn" onclick="copyCurrentGateYaml()">Copy Gate YAML</button>
              </div>
            </div>

            <div class="bench-pane">
              <div class="bench-pane-head">
                <span>Gate Evaluation Output</span>
                <span class="measured tone-bad" id="gateStatusBadge">Exit Code 1 · 2 Violations</span>
              </div>
              <div class="violation-list" id="gateViolationsArea"></div>
            </div>
          </div>
        </div>

        <!-- Baseline Ratchet Trail & GitHub Actions CI Integration -->
        <div class="diff-layout" style="margin-top: var(--space-xl);">
          <div class="trail">
            <div class="trail-step">
              <h3 class="panel-title">1. Author declarative YAML rules</h3>
              <p class="panel-copy">Place invariant rules in <code>.codeexplorer/gates/*.yaml</code> or scaffold defaults with <code>ce gate init</code>.</p>
            </div>
            <div class="trail-step">
              <h3 class="panel-title">2. Snapshot brownfield debt</h3>
              <p class="panel-copy">Run <code>ce gate baseline</code> to record structural hashes of existing legacy violations in <code>.codeexplorer/gate-baseline.json</code>.</p>
            </div>
            <div class="trail-step">
              <h3 class="panel-title">3. Block net-new violations in CI</h3>
              <p class="panel-copy"><code>ce gate check --ratchet</code> passes when only baselined debt exists, and fails with exit code 1 if an AI agent introduces a new violation.</p>
            </div>
            <div class="trail-step">
              <h3 class="panel-title">4. Auto-tighten as code heals</h3>
              <p class="panel-copy">Whenever a baselined violation is fixed, the ratchet lowers the ceiling automatically so debt can never return.</p>
            </div>
          </div>

          <div class="workbench">
            <div class="workbench-bar">
              <span>GitHub Actions CI Pipeline</span>
              <code class="tone-muted">.github/workflows/architecture-gates.yml</code>
            </div>
            <div class="bench-pane">
              <pre class="code-view"><span class="syn-kw">name:</span> Architecture Invariant Gates
<span class="syn-kw">on:</span> [pull_request]

<span class="syn-kw">jobs:</span>
  <span class="syn-fn">invariant-gates:</span>
    <span class="syn-kw">runs-on:</span> ubuntu-latest
    <span class="syn-kw">steps:</span>
      - <span class="syn-kw">uses:</span> actions/checkout@v4
      - <span class="syn-kw">name:</span> Install CodeExplorer CLI
        <span class="syn-kw">run:</span> dotnet tool install -g codeexplorer.cli
      - <span class="syn-kw">name:</span> Build Graph &amp; Verify Invariant Gates
        <span class="syn-kw">run:</span> |
          ce scan
          ce gate check --ratchet --format sarif &gt; results.sarif</pre>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Section 4: 5-Layer Graph Ontology Trail & Polyglot AST Matrix -->
    <section class="band" id="ontology">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">Five-layer graph ontology across eight languages.</h2>
          <p class="band-lede">
            Every node and relationship in <code>.codeexplorer/graph.db</code> follows a strict 5-layer schema, bridging low-level AST control flow with high-level distributed system topology.
          </p>
        </div>

        <div class="trail">
          <div class="trail-item">
            <code class="tone-accent">Layer 5</code>
            <h3 class="panel-title">Logical Structure</h3>
            <p class="panel-copy">Defines high-level business domains, DDD bounded contexts, and strategic cross-context boundaries.</p>
            <code class="tone-muted">Domain · BoundedContext · CONTAINS · DEPENDS_ON</code>
          </div>

          <div class="trail-item">
            <code class="tone-cool">Layer 4</code>
            <h3 class="panel-title">Semantic Architecture</h3>
            <p class="panel-copy">Maps polymorphic modules and workloads, endpoints, databases, schemas, SQL tables, topics, and cloud hosts.</p>
            <code class="tone-muted">Workload · Endpoint · Database · DbSchema · HOSTED_IN</code>
          </div>

          <div class="trail-item">
            <code class="tone-ok">Layer 3</code>
            <h3 class="panel-title">Syntactic UAST &amp; Dataflow</h3>
            <p class="panel-copy">Captures types, functions, members, catch blocks, call graphs, and sliced parameter-to-argument dataflow.</p>
            <code class="tone-muted">Type · Function · Parameter · CallArgument · CALLS</code>
          </div>

          <div class="trail-item">
            <code class="tone-ink-2">Layer 2</code>
            <h3 class="panel-title">Project &amp; Package Boundaries</h3>
            <p class="panel-copy">Models compilation projects, external package dependencies, and executable binary entrypoints.</p>
            <code class="tone-muted">Project · Package · EntryPoint · DEPENDS_ON · PRODUCES</code>
          </div>

          <div class="trail-item">
            <code class="tone-muted">Layer 1</code>
            <h3 class="panel-title">Physical File System</h3>
            <p class="panel-copy">Indexes workspace folders, source files, content hashes, and repository Git metadata.</p>
            <code class="tone-muted">Folder · File · GitSettings · CONTAINS · LOCATED_IN</code>
          </div>
        </div>

        <!-- 8-Language Polyglot Matrix -->
        <div class="lang-grid" aria-label="Supported programming languages">
          <div class="lang-cell">
            <span class="lang-code">CS</span>
            <span class="lang-name">C# / .NET</span>
            <span class="lang-detail">ASP.NET, gRPC, Wolverine, Hangfire, MassTransit, EF Core, Dapper</span>
          </div>
          <div class="lang-cell">
            <span class="lang-code">JV</span>
            <span class="lang-name">Java</span>
            <span class="lang-detail">Spring Boot, Quarkus, Micronaut, Dubbo, MyBatis, jOOQ, Hibernate</span>
          </div>
          <div class="lang-cell">
            <span class="lang-code">KT</span>
            <span class="lang-name">Kotlin</span>
            <span class="lang-detail">Spring, Ktor, Exposed, Room, SQLDelight, Coroutines, Retrofit</span>
          </div>
          <div class="lang-cell">
            <span class="lang-code">TS</span>
            <span class="lang-name">TypeScript &amp; JS</span>
            <span class="lang-detail">NestJS, Next.js, tRPC, Hono, Kysely, TypeORM, Prisma, BullMQ</span>
          </div>
          <div class="lang-cell">
            <span class="lang-code">GO</span>
            <span class="lang-name">Go</span>
            <span class="lang-detail">Gin, Fiber, Fasthttp, gRPC-Gateway, Watermill, GORM, sqlx, Ent</span>
          </div>
          <div class="lang-cell">
            <span class="lang-code">PY</span>
            <span class="lang-name">Python</span>
            <span class="lang-detail">FastAPI, Litestar, Django, Celery, Dramatiq, SQLAlchemy, Temporal</span>
          </div>
          <div class="lang-cell">
            <span class="lang-code">RS</span>
            <span class="lang-name">Rust</span>
            <span class="lang-detail">Axum, Actix Web, Tonic gRPC, SQLx, SeaORM, Diesel, Tokio</span>
          </div>
          <div class="lang-cell">
            <span class="lang-code">SQL</span>
            <span class="lang-name">Database Schemas</span>
            <span class="lang-detail">PostgreSQL, T-SQL, MySQL, SQLite, ClickHouse, BigQuery Lineage</span>
          </div>
        </div>
      </div>
    </section>

    <!-- Section 5: Installation & CLI Reference -->
    <section class="band--tight" id="install">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">Install in 30 seconds.</h2>
          <p class="band-lede">
            Run 100% locally with zero cloud telemetry. Choose the VS Code extension for visual exploration or the standalone <code>ce</code> binary for CLI and CI pipelines.
          </p>
        </div>

        <div class="workbench">
          <div class="workbench-bar">
            <div class="workbench-tabs" role="tablist" aria-label="Installation methods">
              <button type="button" class="tab-btn active" onclick="switchInstallTab('brew', this)">macOS / Linux (Homebrew)</button>
              <button type="button" class="tab-btn" onclick="switchInstallTab('vscode', this)">VS Code Extension</button>
              <button type="button" class="tab-btn" onclick="switchInstallTab('win', this)">Windows (PowerShell)</button>
              <button type="button" class="tab-btn" onclick="switchInstallTab('curl', this)">Linux / macOS (curl)</button>
              <button type="button" class="tab-btn" onclick="switchInstallTab('dotnet', this)">.NET Global Tool</button>
            </div>
            <span class="workbench-readout">MIT License · Standalone Native Binary</span>
          </div>

          <div class="install-pane active" id="tab-brew">
            <p class="panel-copy">Install the native <code>ce</code> binary via Homebrew on macOS (Apple Silicon &amp; Intel) or Linux:</p>
            <div class="install-cmd-row">
              <code>brew install vmikhailov/tap/ce</code>
              <button type="button" class="btn" onclick="copyInstallCmd('brew install vmikhailov/tap/ce')">Copy</button>
            </div>
          </div>

          <div class="install-pane" id="tab-vscode">
            <p class="panel-copy">Install the official extension from VS Code Marketplace or Open VSX (automatically manages the <code>ce</code> CLI):</p>
            <div class="install-cmd-row">
              <code>code --install-extension vmikhailov.code-explorer-vscode</code>
              <div class="slab-actions">
                <button type="button" class="btn" onclick="copyInstallCmd('code --install-extension vmikhailov.code-explorer-vscode')">Copy</button>
                <a href="https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode" target="_blank" rel="noopener" class="btn">Open Marketplace</a>
              </div>
            </div>
          </div>

          <div class="install-pane" id="tab-win">
            <p class="panel-copy">Download and execute the standalone Windows installer in PowerShell:</p>
            <div class="install-cmd-row">
              <code>Invoke-WebRequest -Uri https://code-explorer.app/install.ps1 -OutFile install.ps1; .\\install.ps1</code>
              <button type="button" class="btn" onclick="copyInstallCmd('Invoke-WebRequest -Uri https://code-explorer.app/install.ps1 -OutFile install.ps1; .\\\\install.ps1')">Copy</button>
            </div>
          </div>

          <div class="install-pane" id="tab-curl">
            <p class="panel-copy">Download and install the standalone binary directly to <code>~/.local/bin/ce</code>:</p>
            <div class="install-cmd-row">
              <code>curl -fsSL -O https://code-explorer.app/install.sh &amp;&amp; sh install.sh</code>
              <button type="button" class="btn" onclick="copyInstallCmd('curl -fsSL -O https://code-explorer.app/install.sh && sh install.sh')">Copy</button>
            </div>
          </div>

          <div class="install-pane" id="tab-dotnet">
            <p class="panel-copy">Install cross-platform via the NuGet global tool package (<code>linux-*</code>, <code>osx-*</code>, <code>win-*</code>):</p>
            <div class="install-cmd-row">
              <code>dotnet tool install -g codeexplorer.cli</code>
              <button type="button" class="btn" onclick="copyInstallCmd('dotnet tool install -g codeexplorer.cli')">Copy</button>
            </div>
          </div>
        </div>

        <!-- Essential CLI Commands Grid -->
        <div class="panels panels--3" style="margin-top: var(--space-xl);">
          <div class="panel">
            <code>ce init &amp;&amp; ce scan</code>
            <p class="panel-copy">Initialize workspace configuration and build the 5-layer SQLite architecture graph.</p>
          </div>
          <div class="panel">
            <code>ce map</code>
            <p class="panel-copy">Render a complete C1/C2 system architecture overview with endpoints, brokers, and databases.</p>
          </div>
          <div class="panel">
            <code>ce query &quot;&lt;cypher&gt;&quot;</code>
            <p class="panel-copy">Execute arbitrary OpenCypher queries against <code>.codeexplorer/graph.db</code>.</p>
          </div>
          <div class="panel">
            <code>ce impact &lt;symbol&gt;</code>
            <p class="panel-copy">Compute upstream and downstream transitive blast radius across modules and services.</p>
          </div>
          <div class="panel">
            <code>ce flow &lt;method&gt;</code>
            <p class="panel-copy">Trace end-to-end call chains from API controllers through handlers to SQL tables.</p>
          </div>
          <div class="panel">
            <code>ce lineage &lt;table&gt;</code>
            <p class="panel-copy">Inspect every service and method that reads from or writes to a database table.</p>
          </div>
          <div class="panel">
            <code>ce gate check --ratchet</code>
            <p class="panel-copy">Evaluate Semantic Invariant Gates against the graph and enforce the baseline ratchet.</p>
          </div>
          <div class="panel">
            <code>ce gate baseline</code>
            <p class="panel-copy">Snapshot existing legacy violations into <code>.codeexplorer/gate-baseline.json</code>.</p>
          </div>
          <div class="panel">
            <code>ce mcp</code>
            <p class="panel-copy">Start the Model Context Protocol server over <code>stdio</code> for AI coding agents.</p>
          </div>
        </div>
      </div>
    </section>`},"/architectures":{routePath:"/architectures",pageKind:"architectures",title:"CodeExplorer (ce) Architecture Encyclopedia &amp; Gate Packs",description:"Interactive encyclopedia of software architectures with downloadable OpenCypher Semantic Invariant Gate (SIG) packs for Clean Architecture, Hexagonal, Modular Monolith, Vertical Slice, Feature-Sliced Design, and Microservices.",canonical:"https://code-explorer.app/architectures",ogUrl:"https://code-explorer.app/architectures",ogTitle:"CodeExplorer Architecture Encyclopedia &amp; Gate Packs",ogDescription:"Pick an architectural style, inspect its dependency rules, and download executable OpenCypher gate packs.",mainHtml:`<section id="workbench" class="hero-stage">
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
            <span class="measured">Agnostic + .NET, TS, Go, Python, Java, Rust</span>
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
              Inspect 33 entity kinds, 252 properties, and 44 relationships across 5 layers.
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
                  <code class="ont-layer-pill ont-layer-pill--l4">18 kinds</code>
                </div>
                <p class="ont-layer-sub">Runtime workloads, API &amp; broker contracts, databases, SQL queries, tables, and external or cloud dependencies.</p>
                <div class="ont-node-grid">
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Module" title="Software Modules: Abstract base software module encompassing executable workloads, shared libraries, and automated test suites." aria-selected="false">
                    <code class="ont-node-kind">:Module</code>
                    <span class="ont-node-stats">5p / 5e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Workload" title="Runtime Workloads: Abstract executable runtime workload encompassing backend services, client applications, background workers, and CLI tools." aria-selected="false">
                    <code class="ont-node-kind">:Workload</code>
                    <span class="ont-node-stats">5p / 10e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="App" title="Applications: Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular, Electron)." aria-selected="false">
                    <code class="ont-node-kind">:App</code>
                    <span class="ont-node-stats">5p / 10e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--control_flow" data-kind="Service" title="Services: Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server)." aria-selected="false">
                    <code class="ont-node-kind">:Service</code>
                    <span class="ont-node-stats">5p / 14e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Worker" title="Workers: Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service)." aria-selected="false">
                    <code class="ont-node-kind">:Worker</code>
                    <span class="ont-node-stats">5p / 11e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="CliTool" title="CLI Tools: Represents a command-line tool, developer script, or administrative CLI utility." aria-selected="false">
                    <code class="ont-node-kind">:CliTool</code>
                    <span class="ont-node-stats">5p / 10e</span>
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
                    <span class="ont-node-stats">8p / 9e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="ExternalService" title="External Services &amp; Resources: Represents an external API, managed cloud resource, or remote service dependency outside the workspace (REST, gRPC, GraphQL, S3, Stripe, Auth0)." aria-selected="false">
                    <code class="ont-node-kind">:ExternalService</code>
                    <span class="ont-node-stats">15p / 11e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="Cloud" title="Cloud Platforms &amp; Vendors: Represents a cloud platform, managed infrastructure provider, or SaaS vendor hosting external services, databases, brokers, or workloads (e.g. AWS, GCP, Azure, Cloudflare, Stripe, Supabase)." aria-selected="false">
                    <code class="ont-node-kind">:Cloud</code>
                    <span class="ont-node-stats">5p / 5e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-dimmed" data-kind="PeerMesh" title="Peer Meshes: Represents a symmetric peer-to-peer cluster or collective topology." aria-selected="false">
                    <code class="ont-node-kind">:PeerMesh</code>
                    <span class="ont-node-stats">4p / 1e</span>
                  </button>
                  <button type="button" class="ont-node-btn is-neighbor is-neighbor--data_flow" data-kind="Database" title="Databases &amp; Storage: Represents a database server instance or catalog configured by a workload and containing one or more DbSchema namespaces." aria-selected="false">
                    <code class="ont-node-kind">:Database</code>
                    <span class="ont-node-stats">13p / 7e</span>
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
                    <span class="ont-node-stats">12p / 5e</span>
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
                    <span class="ont-node-stats">8p / 18e</span>
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
              <span class="ont-layer-count">11 relationship types</span>
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
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">BoundedContext -&gt; BoundedContext</code><code class="ont-pair-pill">Domain -&gt; Domain</code><code class="ont-pair-pill">Module -&gt; Library</code><code class="ont-pair-pill">Module -&gt; Package</code><code class="ont-pair-pill">Project -&gt; Package</code><code class="ont-pair-pill">Project -&gt; Project</code> <code class="ont-pair-pill">+1 more</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">HOSTED_IN</code></td>
                    <td>Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Database -&gt; Cloud</code><code class="ont-pair-pill">ExternalService -&gt; Cloud</code><code class="ont-pair-pill">Topic -&gt; Cloud</code><code class="ont-pair-pill">Workload -&gt; Cloud</code></div></td>
                  </tr>
                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--type_dependency">IMPLEMENTED_BY</code></td>
                    <td>Links a library package to the project that implements or encapsulates it.</td>
                    <td><code>line</code></td>
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">Module -&gt; Project</code><code class="ont-pair-pill">Package -&gt; Project</code></div></td>
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
                    <td><div class="ont-pairs-wrap"><code class="ont-pair-pill">CallArgument -&gt; File</code><code class="ont-pair-pill">CallLiteral -&gt; File</code><code class="ont-pair-pill">CatchClause -&gt; File</code><code class="ont-pair-pill">Cloud -&gt; File</code><code class="ont-pair-pill">Database -&gt; File</code><code class="ont-pair-pill">DbSchema -&gt; File</code> <code class="ont-pair-pill">+11 more</code></div></td>
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
    </section>`},"/benchmarks":{routePath:"/benchmarks",pageKind:"benchmarks",title:"CodeExplorer (ce) Benchmarks Catalog",description:"Catalog of reproducible benchmarks comparing CodeExplorer (ce) against codebase-memory-mcp (CBM), Graphify, and ripgrep across twenty (2.49M LOC), immich (461k LOC), OrchardCore (334 projects), and a 324-service multi-repo workspace.",canonical:"https://code-explorer.app/benchmarks",ogUrl:"https://code-explorer.app/benchmarks",ogTitle:"CodeExplorer (ce) Benchmarks Catalog",ogDescription:"Five benchmarks comparing CodeExplorer against codebase-memory-mcp, Graphify, and ripgrep.",mainHtml:`<section class="hero-stage">
      <div class="wrap">
        <div class="hero-top">
          <div>
            <h1 class="hero-title">Benchmarks: AI agents &amp; graph engines.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              We run two distinct types of benchmarks:<br>
              <strong>1. End-to-End AI Agent Workflows</strong> (measuring how much time, tokens, and missed dependencies a graph saves an LLM on real tasks).<br>
              <strong>2. Graph Engine Head-to-Head</strong> (measuring cold scan speed, RAM, query latency, and extraction recall without an LLM).
            </p>
          </div>
        </div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. AI agents in real tasks (time, tokens &amp; completeness).</h2>
          <p class="band-lede">
            Identical model (<code>Gemini 3.8 Flash</code>), identical prompt, same pinned repository commit. Measures how fast an autonomous agent finishes a multi-hop architecture audit, how many tokens it burns, and whether it finds every affected service and table.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Track 1: AI agent workflow benchmarks">
          <div class="table-caption-bar">
            <span>Track 1: End-to-End AI Agent &amp; Quality Gate Workflows (Cases 01, 02, 05 &amp; 06)</span>
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
                <td><code>CodeExplorer</code><br>vs<br><code>CBM</code></td>
                <td class="cell-win" data-bind-html="catalog.cs01_outcome"><strong>~6.0× faster</strong> (1m 33s vs 9m 15s)<br><strong>~7.0× fewer tokens</strong> (~242K vs ~1.7M)<br><strong>100% vs 3.4%</strong> native graph recall</td>
                <td><a href="/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty" class="btn btn--primary">Read Case 01</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/02-enterprise-microservices-deep-audit">Case Study 02</a></strong><br><span class="tone-muted">324 Microservices</span></td>
                <td>Full blast-radius audit across shared ORM tables, synchronous cross-service HTTP/RPC fan-in, and RabbitMQ/PubSub queues (13,782 files, TS + Go + SQL, zero schema hints).</td>
                <td><code>CodeExplorer</code><br>vs<br><code>ripgrep</code> + Python</td>
                <td class="cell-win" data-bind-html="catalog.cs02_outcome"><strong>~7.0× faster</strong> (4m 16s vs 29m 52s)<br><strong>~25× fewer tokens</strong> (~686K vs ~17.2M)<br><strong>100% vs ~82%</strong> (rg missed 9 services)</td>
                <td><a href="/benchmarks/02-enterprise-microservices-deep-audit" class="btn">Read Case 02</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/05-polyglot-architecture-and-blast-radius">Case Study 05</a></strong><br><span class="tone-muted">OrchardCore</span></td>
                <td>4-part architectural governance, AppSec <code>AuthorizeAsync</code> audit, <code>YesSql.ISession</code> layer violations, <code>PublishedAsync</code> reactive pipeline, and <code>IContentManager</code> blast radius across 334 projects (7,837 files).</td>
                <td><code>CodeExplorer</code><br>vs<br><code>ripgrep</code></td>
                <td class="cell-win" data-bind-html="catalog.cs05_outcome"><strong class="tone-ok">~2.5× faster</strong> (4m 25s vs 11m 04s, 92 vs 193 turns)<br><strong class="tone-ok">~4.1× fewer tokens</strong> (~3.9M vs ~16.1M, ~2.9× cheaper)<br><strong>100% vs ~93%</strong> (331/331 vs 307/331 items)</td>
                <td><a href="/benchmarks/05-polyglot-architecture-and-blast-radius" class="btn">Read Case 05</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/06-precommit-architectural-guard">Case Study 06</a></strong><br><span class="tone-muted">argoproj/argo-cd</span></td>
                <td>Automated pre-commit architectural guard &amp; 4-phase SHA-256 ratchet showcase: enforce 6 invariant rules across 18 subsystems (4,216 files), freeze legacy debt with line-shift immunity, block single-line PR leaks, and monotonically tighten baselines.</td>
                <td><code>CodeExplorer</code><br><span class="tone-muted">(Native SIG)</span></td>
                <td class="cell-win" data-bind-html="catalog.cs06_outcome"><strong class="tone-ok">422 ms native build check</strong><br><strong class="tone-ok">0 tokens ($0.00)</strong> · 4-phase SHA-256 ratchet<br><strong>6 of 6 gates evaluated</strong> (2 PASS, 1 WARN, 3 FAIL)</td>
                <td><a href="/benchmarks/06-precommit-architectural-guard" class="btn">Read Showcase</a></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="band band--deep" style="border-bottom: none;">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">2. Graph engine head-to-head (no LLM in the loop).</h2>
          <p class="band-lede">
            Direct indexer and MCP server comparison on <code>twenty</code> (24,527 files) and <code>immich</code> (3,205 files) across 371 source-verified items: cold scan time, memory/disk footprint, query latency, and extraction recall.
          </p>
        </div>

        <div class="table-surface" role="region" aria-label="Track 2: Graph engine head-to-head benchmarks">
          <div class="table-caption-bar">
            <span>Track 2: Deterministic Indexer &amp; MCP Benchmarks (Cases 03 &amp; 04)</span>
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
                <td><code>CodeExplorer</code><br>vs<br><code>CBM</code></td>
                <td class="cell-num" data-bind-html="catalog.cs03_outcome"><strong class="tone-ok">Basic Code: 78%</strong> vs 38% (102/130 vs 49/130)<br><strong class="tone-ok">DB &amp; Queues: ~98%</strong> vs 0% (235/241)<br><strong class="tone-accent">Scan:</strong> <code>ce</code> faster on <code>immich</code>; <code>CBM</code> faster on <code>twenty</code></td>
                <td><a href="/benchmarks/03-code-explorer-vs-codebase-memory-mcp" class="btn">Read Case 03</a></td>
              </tr>
              <tr>
                <td><strong><a href="/benchmarks/04-code-explorer-vs-graphify">Case Study 04</a></strong><br><span class="tone-muted">twenty + immich</span></td>
                <td>Monorepo indexing scalability from 461k LOC (<code>immich</code>) to 2.49M LOC (<code>twenty</code>), cold MCP server startup time, and graph recall across all 371 items.</td>
                <td><code>CodeExplorer</code><br>vs<br><code>Graphify</code></td>
                <td class="cell-win" data-bind-html="catalog.cs04_outcome"><strong>~6.1× faster scan</strong> on <code>immich</code> (~18s vs ~108s)<br><strong>&gt;18× faster</strong> on <code>twenty</code> (~147s vs DNF &gt;45m)<br><strong>Basic Code: 78%</strong> vs 18% · <strong>DB &amp; Queues: ~98%</strong></td>
                <td><a href="/benchmarks/04-code-explorer-vs-graphify" class="btn">Read Case 04</a></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-xl);" data-bind-html="catalog.participants_html"><strong>Tested participants &amp; versions across the benchmark suite:</strong><br>
          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>v1.75.0 to v1.80.2</code> (.NET 10 &amp; .NET 11, SQLite + OpenCypher, 36 MCP tools)<br>
          2. <strong>codebase-memory-mcp (<code>CBM</code>):</strong> <code>v0.12.1</code> (<code>win32-x64</code> Go + SQLite binary, 17 MCP tools)<br>
          3. <strong>Graphify (<code>graphifyy</code>):</strong> <code>v0.9.104</code> (Python 3.10, Tree-sitter + NetworkX <code>--code-only</code>, 10 MCP tools)<br>
          4. <strong>Lexical CLI Baseline:</strong> <code>ripgrep</code> (<code>rg v14.1.1</code>) + Python <code>3.10</code> (Case 02) + <code>view_file</code><br>
          5. <strong>LLM Model (Track 1):</strong> <code>Gemini 3.8 Flash</code> (symmetric system prompts across paired runs)<br>
          6. <strong>Pinned Target Commits:</strong> <code>twentyhq/twenty @ b9ef7107</code> (Case 01), <code>twentyhq/twenty @ 12e8fae4</code> &amp; <code>immich-app/immich @ 2153d22f</code> (Cases 03 &amp; 04), <code>OrchardCMS/OrchardCore @ 05ad2d9a</code> (Case 05)</div>
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
              Two identical AI agents were given the same 4-part architectural audit on the public <code>twentyhq/twenty</code> monorepo with <strong>100% word-for-word symmetric system prompts</strong>: one equipped with <code>CodeExplorer</code> and one equipped with <code>codebase-memory-mcp</code> (<code>CBM</code>). Both graphs were pre-indexed before the run.
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
            <p class="panel-copy" data-bind-html="cs01.card_speed_copy"><code>CodeExplorer</code> answered in <strong>1m 33s</strong> (<strong>2m 14s</strong> wall); <code>CBM</code> took <strong>9m 15s</strong> (<strong>10m 34s</strong> wall).</p>
            <code class="panel-code" data-bind="cs01.card_turns_footer">7 turns vs 28 turns</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs01.card_tokens">~7.0×</span>
            <h2 class="panel-title">Fewer Input Tokens</h2>
            <p class="panel-copy" data-bind-html="cs01.card_tokens_copy"><code>~242K</code> cumulative input tokens vs <code>~1.7M</code> tokens across the session.</p>
            <code class="panel-code" data-bind="cs01.card_cost_footer">$0.06 vs $0.23 (~3.9×)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs01.card_recall">100% vs 3.4%</span>
            <h2 class="panel-title">Native Graph Recall</h2>
            <p class="panel-copy" data-bind-html="cs01.card_recall_copy"><code>ce</code> resolved <strong>381 / 381</strong> items directly from <code>graph.db</code>. <code>CBM</code> returned <strong>13 / 381</strong> (only self-calls).</p>
            <code class="panel-code" data-bind="cs01.card_recall_footer">0 source files read by ce</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs01.card_scripts">0 vs 3</span>
            <h2 class="panel-title">Fallback AST Scripts</h2>
            <p class="panel-copy" data-bind-html="cs01.card_scripts_copy">After 8 <code>CBM</code> queries returned 0 cross-file edges, the <code>CBM</code> agent wrote 3 custom TS AST scripts &amp; read 10 files.</p>
            <code class="panel-code" data-bind="cs01.card_scripts_footer">0 scripts vs 1,037 LOC of JS</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs01.participants_html"><strong>Tested participants:</strong><br>
          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>v1.77.2</code> (.NET 10, SQLite + OpenCypher)<br>
          2. <strong>codebase-memory-mcp (<code>CBM</code>):</strong> <code>v0.12.1</code> (Go + SQLite)<br>
          3. <strong>LLM Model:</strong> <code>Gemini 3.8 Flash</code> (100% symmetric system prompts)<br>
          4. <strong>Target Repository:</strong> <code>twentyhq/twenty</code> (commit <code>b9ef7107</code>, 46 packages, 24,527 files, 2.49M LOC)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Execution telemetry scorecard.</h2>
          <p class="band-lede">
            All metrics are extracted directly from the runtime logs (<code>transcript_full.jsonl</code>) of conversations <code data-bind="cs01.conv_ce">4a49e7f1</code> (<code>ce</code>) and <code data-bind="cs01.conv_cbm">9a849d22</code> (<code>CBM</code>). Both agents ran under 100% word-for-word symmetric system prompts with zero pre-baked query examples.
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
                <th><code>CBM</code> (<code>cbm</code> + <code>rg</code> + 3 AST Scripts)</th>
                <th><code>CodeExplorer</code> (<code>ce</code> Graph Only)</th>
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
            In Turns 1 to 5, <code>cbm_twenty_audit_agent</code> ran <code>get_graph_schema</code> and 7 <code>query_graph</code> calls across all four tasks. Because <code>CBM</code> returned zero cross-file relationships, the agent fell back to writing 3 custom TypeScript Compiler AST scripts:
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
                <th><code>CBM</code> Graph Only</th>
                <th><code>CBM</code> + 3 AST Scripts</th>
                <th><code>CodeExplorer</code> Graph Only</th>
                <th>Root Cause of <code>CBM</code> Graph Gap</th>
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
          <p class="band-lede" data-bind-html="cs01.sec3_lede">Every LLM turn re-sends the active conversation history. Because the <code>CodeExplorer</code> agent finished in 7 turns while the <code>CBM</code> agent iterated through 28 turns of AST script writing and file reading, cumulative billed input tokens differed by <strong>~7.0×</strong> (<code>~242K</code> vs <code>~1.7M</code>).</p>
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
                <th><code>CBM</code> Agent Context<br><span class="tone-muted">Cumulative Input</span></th>
                <th><code>CodeExplorer</code> Agent Context<br><span class="tone-muted">Cumulative Input</span></th>
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
              Two identical AI agents executed a 4-part cross-service architectural audit on a distributed multi-repo microservices workspace (324 independent microservice repositories, 93 shared library repositories, 33 workers, 13,782 source files) under <strong>symmetric system prompts with zero schema hints</strong>: one using <code>ripgrep</code> + Python scripts + <code>view_file</code>, and one using <code>CodeExplorer</code> (<code>ce taxonomy</code> + <code>ce query</code> + <code>view_file</code>).
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
            <p class="panel-copy" data-bind-html="cs02.card_speed_copy"><code>4m 16s</code> (<code>256s</code>) with CodeExplorer (including <code>ce taxonomy</code> discovery) vs <code>29m 52s</code> (<code>1,792s</code>) with <code>ripgrep</code> + Python scripts.</p>
            <code class="panel-code" data-bind="cs02.card_turns_footer">18 turns vs 285 turns (~16×)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs02.card_tokens">~25×</span>
            <h2 class="panel-title">Fewer Input Tokens</h2>
            <p class="panel-copy" data-bind-html="cs02.card_tokens_copy"><code>~686K</code> cumulative input tokens (<code>0</code> compactions) vs <code>~17.2M</code> post-compaction tokens.</p>
            <code class="panel-code" data-bind="cs02.card_cost_footer">$0.10 vs $1.15 (~11×)</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs02.card_reads">16 vs 114</span>
            <h2 class="panel-title">Manual File Reads</h2>
            <p class="panel-copy" data-bind-html="cs02.card_reads_copy">Without schema hints in the prompt, the <code>ce</code> agent discovered the 5-layer ontology via <code>ce taxonomy</code> and needed <code>16</code> file reads vs <code>114</code> file reads and <code>14</code> scripts.</p>
            <code class="panel-code" data-bind="cs02.card_reads_footer">27 tool calls vs 284 tool calls</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs02.card_recall">100% vs ~82%</span>
            <h2 class="panel-title">Blast-Radius Recall</h2>
            <p class="panel-copy">Both agents tied at 100% on Tasks 1 &amp; 4. On Task 2, <code>ripgrep</code> missed 9 RTB microservices wired via environment host constants.</p>
            <code class="panel-code" data-bind="cs02.card_recall_footer">83/83 callers vs 9 services missed</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs02.participants_html"><strong>Tested participants:</strong><br>
          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>v1.77.0</code> (.NET 10, SQLite + OpenCypher, schema <code>v6</code>: 209,271 nodes, 454,394 edges)<br>
          2. <strong>Lexical CLI Baseline:</strong> <code>ripgrep</code> (<code>rg v14.1.1</code>) + Python <code>3.10</code> + <code>view_file</code><br>
          3. <strong>LLM Model:</strong> <code>Gemini 3.8 Flash</code> (symmetric system prompts with zero schema spoilers)<br>
          4. <strong>Target Workspace:</strong> Enterprise Multi-Repo Microservices System (450 independent service/library repositories, 324 services, 13,782 files; service names anonymized 1-to-1)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Execution telemetry scorecard.</h2>
          <p class="band-lede">
            Extracted from <code>transcript_full.jsonl</code> (conversations <code data-bind="cs02.conv_ce">3e1e9d36</code> for <code>ce</code> and <code>ede04347</code> for <code>ripgrep</code>) and committed in <code>benchmarks/02-enterprise-microservices-deep-audit/results/metrics.json</code>. Neither agent received any schema hints or pre-baked Cypher templates in its system prompt.
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
                <th data-bind-html="cs02.th_rg"><code>ripgrep</code> + Python (142 CLI runs + 114 file reads)</th>
                <th data-bind-html="cs02.th_ce"><code>CodeExplorer</code> (10 CLI runs + 16 file reads)</th>
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
                <th><code>ripgrep</code> + Python</th>
                <th><code>CodeExplorer</code></th>
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
    </section>`},"/benchmarks/03-code-explorer-vs-codebase-memory-mcp":{routePath:"/benchmarks/03-code-explorer-vs-codebase-memory-mcp",pageKind:"benchmark-case",title:"Case Study 03: CodeExplorer vs codebase-memory-mcp (CBM) on twenty &amp; immich",description:"Deterministic benchmark comparing CodeExplorer (v1.75.0) and codebase-memory-mcp (v0.12.1) across cold indexing, 371 ground-truth items (seed=42), and MCP server latency on twenty (2.49M LOC) and immich (461k LOC).",canonical:"https://code-explorer.app/benchmarks/03-code-explorer-vs-codebase-memory-mcp",ogUrl:"https://code-explorer.app/benchmarks/03-code-explorer-vs-codebase-memory-mcp",ogTitle:"Case Study 03: CodeExplorer vs codebase-memory-mcp (CBM) on twenty &amp; immich",ogDescription:"Deterministic benchmark comparing CodeExplorer (v1.75.0) and codebase-memory-mcp (v0.12.1) across cold indexing, 371 ground-truth items (seed=42), and MCP server latency on twenty (2.49M LOC) and immich (461k LOC).",mainHtml:`<section class="hero-stage">
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
              Head-to-head indexer and MCP server comparison on <code>twenty</code> and <code>immich</code>: how accurately each engine extracts routes, function calls, inheritance, queues, and database tables across 371 source-verified items.
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
            <p class="panel-copy">Across 130 basic items supported by both tools (routes, HTTP calls, DI method calls, class inheritance): <code>ce</code> scored 102/130 vs <code>CBM</code> 49/130 (7 wins, 1 tie, 0 losses).</p>
            <code class="panel-code">102 / 130 vs 49 / 130</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs03.card_group_b">~98%</span>
            <h2 class="panel-title">Queues, DB Tables &amp; SQL</h2>
            <p class="panel-copy">Across 241 message queue, ORM table, SQL read/write lineage, and sub-project items: <code>ce</code> matched 235/241. <code>CBM</code> does not index these.</p>
            <code class="panel-code">235 / 241 verified items</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool">~4.3 to 4.7ms</span>
            <h2 class="panel-title">OpenCypher Latency</h2>
            <p class="panel-copy">Single-shot warm OpenCypher round-trip in <code>ce</code> (<code>4.3 to 4.7 ms</code>) vs <code>38.1 to 71.2 ms</code> in <code>CBM</code> over persistent JSON-RPC stdio.</p>
            <code class="panel-code">~8.9× to 15× faster Cypher query</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs03.card_scan">97s vs 147s</span>
            <h2 class="panel-title">Where CBM Won</h2>
            <p class="panel-copy"><code>CBM</code> indexed <code>twenty</code> ~1.5× faster with ~2.2× lower peak RAM (<code>2.1 GB</code> vs <code>4.7 GB</code>), while <code>ce</code> was faster on <code>immich</code> with a 5.1× smaller DB.</p>
            <code class="panel-code">CBM faster on 24.5k-file cold scan</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs03.participants_html"><strong>Tested participants:</strong><br>
          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>v1.75.0</code> (.NET 10, SQLite + OpenCypher, 36 MCP tools)<br>
          2. <strong>codebase-memory-mcp (<code>CBM</code>):</strong> <code>v0.12.1</code> (<code>win32-x64</code> Go + SQLite binary, 17 MCP tools)<br>
          3. <strong>Target Repositories:</strong> <code>twentyhq/twenty</code> (commit <code>12e8fae4</code>, 24,527 files, 2.49M LOC) &amp; <code>immich-app/immich</code> (commit <code>2153d22f</code>, 3,205 files, 461k LOC)</div>
      </div>
    </section>

    <section class="band band--deep">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Graph completeness: basic code vs full architecture.</h2>
          <p class="band-lede">
            <code>CBM</code> indexes files, classes, routes, and function calls, whereas <code>CodeExplorer</code> also extracts message queues, ORM database tables, and SQL read/write queries. To keep the comparison fair, we separate the 371 verified items into <strong>Basic Code (130 items supported by both tools)</strong> and <strong>Architecture, DB &amp; Queues (241 items supported only by CodeExplorer)</strong>.
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
                <th><code>CodeExplorer</code></th>
                <th><code>CBM</code></th>
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
            <code>CodeExplorer</code> was faster and produced a 5.1× smaller SQLite database on <code>immich</code> (3,205 files). On <code>twenty</code> (24,527 files), <code>CBM</code> completed cold indexing ~1.5× faster with lower peak RAM because it extracts only basic AST and call edges, whereas <code>ce</code> also runs 250 framework, ORM, and queue analyzers.
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
                <th><code>CodeExplorer</code></th>
                <th><code>CBM</code></th>
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
            Single-shot JSON-RPC round-trip measurements over persistent <code>stdio</code> sessions (<code>results/mcp-benchmark.json</code>).
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
                <th><code>CodeExplorer</code> (<code>immich</code> / <code>twenty</code>)</th>
                <th><code>CBM</code> (<code>immich</code> / <code>twenty</code>)</th>
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
    </section>`},"/benchmarks/04-code-explorer-vs-graphify":{routePath:"/benchmarks/04-code-explorer-vs-graphify",pageKind:"benchmark-case",title:"Case Study 04: CodeExplorer vs Graphify on twenty &amp; immich",description:"Deterministic benchmark comparing CodeExplorer (v1.75.0) and Graphify (v0.9.104) across cold indexing, 371 ground-truth items (seed=42), and MCP server latency on twenty (2.49M LOC) and immich (461k LOC).",canonical:"https://code-explorer.app/benchmarks/04-code-explorer-vs-graphify",ogUrl:"https://code-explorer.app/benchmarks/04-code-explorer-vs-graphify",ogTitle:"Case Study 04: CodeExplorer vs Graphify on twenty &amp; immich",ogDescription:"Deterministic benchmark comparing CodeExplorer (v1.75.0) and Graphify (v0.9.104) across cold indexing, 371 ground-truth items (seed=42), and MCP server latency on twenty (2.49M LOC) and immich (461k LOC).",mainHtml:`<section class="hero-stage">
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
              Head-to-head indexer and MCP server comparison on <code>immich</code> and <code>twenty</code>: cold indexing scalability, MCP startup latency, and graph completeness across 371 verified items.
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
            <p class="panel-copy"><code>~18s</code> vs <code>~108s</code> on <code>immich</code> (3.2k files), and <code>~147s</code> vs <strong>DNF (&gt;45m timeout)</strong> on <code>twenty</code> (24.5k files).</p>
            <code class="panel-code">SQLite vs NetworkX JSON</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-accent" data-bind="cs04.card_group_a">78% vs 18%</span>
            <h2 class="panel-title">Basic Code &amp; Call Graph</h2>
            <p class="panel-copy">Across 130 basic items: <code>ce</code> scored 102/130 vs <code>Graphify</code> 23/130 (on <code>immich</code> alone: <code>ce</code> 73% vs <code>Graphify</code> 35%).</p>
            <code class="panel-code">6 wins, 2 ties, 0 losses</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs04.card_group_b">~98%</span>
            <h2 class="panel-title">Queues, DB Tables &amp; SQL</h2>
            <p class="panel-copy"><code>ce</code> matched 235/241 queue, ORM table, data-lineage, and sub-project items. <code>Graphify</code> does not index these.</p>
            <code class="panel-code">235 / 241 verified items</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading">100% Tie</span>
            <h2 class="panel-title">Where Graphify Tied</h2>
            <p class="panel-copy">On <code>immich</code> Call Graph &amp; DI (<code>C6</code>), <code>Graphify</code> tied <code>ce</code> at <strong>100% (20/20)</strong>, resolving TS constructor DI accurately.</p>
            <code class="panel-code">20/20 on immich C6</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs04.participants_html"><strong>Tested participants:</strong><br>
          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>v1.75.0</code> (.NET 10, SQLite + OpenCypher, 36 MCP tools)<br>
          2. <strong>Graphify (<code>graphifyy</code>):</strong> <code>v0.9.104</code> (Python 3.10, Tree-sitter + NetworkX <code>--code-only</code>, 10 MCP tools)<br>
          3. <strong>Target Repositories:</strong> <code>twentyhq/twenty</code> (commit <code>12e8fae4</code>, 24,527 files, 2.49M LOC) &amp; <code>immich-app/immich</code> (commit <code>2153d22f</code>, 3,205 files, 461k LOC)</div>
      </div>
    </section>

    <section class="band">
      <div class="wrap">
        <div class="band-head">
          <h2 class="band-title">1. Cold indexing &amp; scalability across repository sizes.</h2>
          <p class="band-lede">
            On <code>immich</code> (3,205 files), both tools completed cleanly, with <code>ce</code> finishing ~6.1× faster. On <code>twenty</code> (24,527 files), <code>Graphify</code>&rsquo;s Python AST &amp; NetworkX pipeline stalled on a single CPU core in <code>build_from_json</code> and did not finish within the 45-minute timeout.
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
                <th><code>CodeExplorer</code></th>
                <th><code>Graphify</code></th>
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
            Because <code>Graphify</code> timed out on <code>twenty</code>, we report both <strong><code>immich</code>-only recall (66 basic items where both tools completed indexing)</strong> and combined recall across all 371 verified items.
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
                <th><code>CodeExplorer</code></th>
                <th><code>Graphify</code></th>
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
            Because Graphify loads <code>graph.json</code> into an in-memory Python <code>NetworkX</code> graph on startup, cold MCP initialization takes ~3.7 seconds vs ~0.6 seconds for <code>ce</code>&rsquo;s SQLite engine.
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
                <th><code>CodeExplorer</code></th>
                <th><code>Graphify</code></th>
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
    </section>`},"/benchmarks/05-polyglot-architecture-and-blast-radius":{routePath:"/benchmarks/05-polyglot-architecture-and-blast-radius",pageKind:"benchmark-case",title:"Case Study 05: OrchardCore Modular .NET Audit (CodeExplorer vs ripgrep)",description:"",canonical:"https://code-explorer.app/benchmarks/05-polyglot-architecture-and-blast-radius",ogUrl:"https://code-explorer.app/benchmarks/05-polyglot-architecture-and-blast-radius",ogTitle:"Case Study 05: OrchardCore Modular .NET Audit (CodeExplorer vs ripgrep)",ogDescription:"",mainHtml:'<section class="hero-stage">\n      <div class="wrap">\n        <div class="crumb-bar">\n          <div class="crumb-trail">\n            <a href="/benchmarks">Benchmarks Catalog</a>\n            <span>/</span>\n            <span class="tone-ink">Case Study 05: OrchardCore Modular .NET Audit</span>\n          </div>\n          <span class="measured" data-bind="cs05.crumb_scale">334 projects, 7,837 files</span>\n        </div>\n\n        <div class="hero-top">\n          <div>\n            <h1 class="hero-title">Case Study 05: OrchardCore modular .NET monolith audit (ce vs ripgrep).</h1>\n          </div>\n          <div class="hero-aside">\n            <p class="hero-lede">\n              Paired A/B agent benchmark on <code>OrchardCMS/OrchardCore</code> (open-source modular .NET CMS &amp; framework with <code>334</code> projects and <code>7,837</code> files). Measures how an AI agent resolves AppSec authorization invariants, <code>YesSql.ISession</code> boundary violations, cross-module <code>PublishedAsync</code> pipelines, and <code>IContentManager</code> blast radius.\n            </p>\n            <div class="hero-controls">\n              <a href="https://github.com/vmikhailov/code-explorer/tree/main/benchmarks/05-polyglot-architecture-and-blast-radius/results" target="_blank" rel="noopener" class="btn btn--primary">Inspect Outputs &amp; metrics.json</a>\n              <a href="#prompts" class="btn">Inspect Tested Prompts</a>\n            </div>\n          </div>\n        </div>\n\n        <div class="panels panels--4" style="margin-top: var(--space-xl);">\n          <div class="panel panel--sunken">\n            <span class="reading tone-ok" data-bind="cs05.card_speedup">~2.5×</span>\n            <h2 class="panel-title">Shorter Time to Answer</h2>\n            <p class="panel-copy" data-bind-html="cs05.card_speed_copy"><code>4m 25s</code> with <code>CodeExplorer</code> vs <code>11m 04s</code> with <code>ripgrep</code> + <code>view_file</code> across <code>334</code> projects.</p>\n            <code class="panel-code" data-bind="cs05.card_turns_footer">92 turns / 184 steps vs 193 turns / 389 steps</code>\n          </div>\n          <div class="panel panel--sunken">\n            <span class="reading tone-accent" data-bind="cs05.card_tokens">~4.1×</span>\n            <h2 class="panel-title">Fewer Input Tokens</h2>\n            <p class="panel-copy" data-bind-html="cs05.card_tokens_copy"><code>~3.9M</code> cumulative input tokens with <code>CodeExplorer</code> vs <code>~16.1M</code> with <code>ripgrep</code> (<code>~2.9×</code> lower API cost).</p>\n            <code class="panel-code" data-bind="cs05.card_cost_footer">$0.65 vs $1.91 (~1.9× less tool output)</code>\n          </div>\n          <div class="panel panel--sunken">\n            <span class="reading tone-cool" data-bind="cs05.card_reads">~3.3×</span>\n            <h2 class="panel-title">Fewer File Reads</h2>\n            <p class="panel-copy" data-bind-html="cs05.card_reads_copy"><code>36</code> <code>view_file</code> reads (<code>31</code> C# files + <code>5</code> schema/spill reads) vs <code>117</code> C# reads by <code>ripgrep</code>.</p>\n            <code class="panel-code" data-bind="cs05.card_reads_footer">287 KB vs 560 KB tool output</code>\n          </div>\n          <div class="panel panel--sunken">\n            <span class="reading" data-bind="cs05.card_recall">100% vs ~93%</span>\n            <h2 class="panel-title">Symbol Recall &amp; Context</h2>\n            <p class="panel-copy" data-bind-html="cs05.card_recall_copy"><code>CodeExplorer</code> matched all <code>331/331</code> items; <code>ripgrep</code> guessed or missed <code>24</code> Task 4a caller methods from raw grep lines.</p>\n            <code class="panel-code" data-bind="cs05.card_recall_footer">331/331 vs 307/331 verified items</code>\n          </div>\n        </div>\n\n        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs05.participants_html"><strong>Tested participants:</strong><br>\n          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>v1.78.1</code> (.NET 10, SQLite + OpenCypher MCP server, <code>134,067</code> nodes, <code>288,527</code> edges)<br>\n          2. <strong>Lexical CLI Baseline:</strong> <code>ripgrep</code> (<code>rg v14.1.1</code>) + <code>view_file</code> (pure lexical search + file reading; custom AST scripts disallowed, complementing Case 02)<br>\n          3. <strong>LLM Model:</strong> <code>Gemini 3.8 Flash</code> (matched subagent prompts without <code>write_to_file</code>; see methodological note below)<br>\n          4. <strong>Target Repository:</strong> <code>OrchardCMS/OrchardCore</code> (commit <code>05ad2d9a</code>, <code>334</code> projects, <code>7,837</code> files)</div>\n      </div>\n    </section>\n\n    <section class="band">\n      <div class="wrap">\n        <div class="band-head">\n          <h2 class="band-title">1. What the agents had to solve on OrchardCore.</h2>\n          <p class="band-lede">\n            In a modular .NET monolith like <code>OrchardCore</code>, architectural rules span <code>334</code> projects: verifying security invariants, layer boundaries, reactive event pipelines, and breaking-change blast radius requires joining attributes, inheritance hierarchies, member access, and multi-hop call chains.\n          </p>\n        </div>\n\n        <div class="panels panels--4">\n          <div class="panel">\n            <div class="panel-head">\n              <h3 class="panel-title">AppSec AuthorizeAsync Audit</h3>\n              <span class="badge badge--accent">Task 1</span>\n            </div>\n            <p class="panel-copy">Audit all 88 non-anonymous <code>[HttpPost]</code>, <code>[HttpPut]</code>, and <code>[HttpDelete]</code> actions across 5 security modules to find which actions call <code>AuthorizeAsync</code> within 1 to 2 hops vs which do not.</p>\n            <code class="panel-code">67 calling vs 21 missing AuthorizeAsync</code>\n          </div>\n          <div class="panel">\n            <div class="panel-head">\n              <h3 class="panel-title">YesSql.ISession Boundary Audit</h3>\n              <span class="badge badge--win">Task 2</span>\n            </div>\n            <p class="panel-copy">Identify every Controller and Display Driver across <code>src/OrchardCore.Modules/*</code> that bypasses domain services to invoke operations directly on an injected <code>YesSql.ISession</code> field.</p>\n            <code class="panel-code">25 classes across 13 modules (ACCESSED_BY)</code>\n          </div>\n          <div class="panel">\n            <div class="panel-head">\n              <h3 class="panel-title">PublishedAsync Event Fan-Out</h3>\n              <span class="badge badge--cool">Task 3</span>\n            </div>\n            <p class="panel-copy">Locate all <code>PublishingAsync</code> and <code>PublishedAsync</code> content handlers across the monorepo and trace their 1 to 2 hop downstream calls into DB/index stores, tag caches, and workflows.</p>\n            <code class="panel-code">16 concrete + 6 base handler methods</code>\n          </div>\n          <div class="panel">\n            <div class="panel-head">\n              <h3 class="panel-title">IContentManager Blast Radius</h3>\n              <span class="badge badge--accent">Task 4</span>\n            </div>\n            <p class="panel-copy">Find all projects calling <code>GetAsync</code>, <code>CreateAsync</code>, or <code>PublishAsync</code> (including via DI service locators) and isolate over-coupled <code>DEPENDS_ON</code> projects with 0 calls.</p>\n            <code class="panel-code">19 caller projects + 38 over-coupled projects</code>\n          </div>\n        </div>\n      </div>\n    </section>\n\n    <section class="band band--deep">\n      <div class="wrap">\n        <div class="band-head">\n          <h2 class="band-title">2. Execution telemetry scorecard.</h2>\n          <p class="band-lede">\n            Measured from paired A/B agent runs on the same <code>OrchardCMS/OrchardCore</code> commit with matched subagent system prompts.\n          </p>\n        </div>\n\n        <div class="table-surface">\n          <div class="table-caption-bar">\n            <span>Paired A/B Agent Telemetry on OrchardCMS/OrchardCore (334 Projects, 7,837 Files)</span>\n            <span class="measured">Source: benchmarks/05-polyglot-architecture-and-blast-radius/results/metrics.json</span>\n          </div>\n          <table class="bench-table">\n            <thead>\n              <tr>\n                <th>Metric</th>\n                <th><code>ripgrep</code> + File Reads</th>\n                <th><code>CodeExplorer</code></th>\n                <th>Delta</th>\n              </tr>\n            </thead>\n            <tbody>\n              <tr>\n                <td>Time to Answer (<code>send_message</code>)</td>\n                <td class="cell-loss" data-bind="cs05.tbl_time_rg">664 s (11 min 04 sec) / 701 s wall</td>\n                <td class="cell-win" data-bind="cs05.tbl_time_ce">265 s (4 min 25 sec) / 299 s wall</td>\n                <td class="cell-win" data-bind="cs05.tbl_time_delta">2.51× faster (2.34× wall)</td>\n              </tr>\n              <tr>\n                <td>LLM Turns, Steps &amp; Compactions</td>\n                <td class="cell-loss" data-bind="cs05.tbl_turns_rg">193 turns / 389 steps (0 compactions)</td>\n                <td class="cell-win" data-bind="cs05.tbl_turns_ce">92 turns / 184 steps (0 compactions)</td>\n                <td class="cell-win" data-bind="cs05.tbl_turns_delta">2.10× fewer turns (2.11× fewer steps)</td>\n              </tr>\n              <tr>\n                <td>Total Tool Calls &amp; File Reads</td>\n                <td class="cell-loss" data-bind-html="cs05.tbl_calls_rg">192 (74 <code>rg</code> + 117 <code>view_file</code>, 1 <code>send_message</code>)</td>\n                <td class="cell-win" data-bind-html="cs05.tbl_calls_ce">91 (54 <code>mcp</code> + 36 <code>view_file</code>, 1 <code>send_message</code>)</td>\n                <td class="cell-win" data-bind="cs05.tbl_calls_delta">2.11× fewer calls (3.25× fewer reads)</td>\n              </tr>\n              <tr>\n                <td>Raw Tool Output Ingested</td>\n                <td class="cell-loss" data-bind="cs05.tbl_payload_rg">573,355 chars (~143,339 tokens)</td>\n                <td class="cell-win" data-bind="cs05.tbl_payload_ce">294,355 chars (~73,589 tokens)</td>\n                <td class="cell-win" data-bind="cs05.tbl_payload_delta">1.95× less tool payload</td>\n              </tr>\n              <tr>\n                <td>Cumulative Input Tokens</td>\n                <td class="cell-loss" data-bind="cs05.tbl_in_rg">16,098,025 est. (25,878,074 API tokens)</td>\n                <td class="cell-win" data-bind="cs05.tbl_in_ce">3,910,897 est. (6,000,146 API tokens)</td>\n                <td class="cell-win" data-bind="cs05.tbl_in_delta">4.12× fewer input tokens (4.31× API tokens)</td>\n              </tr>\n              <tr>\n                <td>API Cost w/ 90% Cache Discount (Gemini Flash)</td>\n                <td class="cell-num" data-bind="cs05.tbl_cost_rg">$1.91 Flash ($7.51 Pro)</td>\n                <td class="cell-win" data-bind="cs05.tbl_cost_ce">$0.65 Flash ($2.52 Pro)</td>\n                <td class="cell-win" data-bind="cs05.tbl_cost_delta">2.94× cheaper</td>\n              </tr>\n              <tr>\n                <td>Symbol Recall &amp; Context Health (Tasks 1 to 4)</td>\n                <td class="cell-num" data-bind="cs05.tbl_recall_rg">92.7% (307 / 331; 24 Task 4a caller errors)</td>\n                <td class="cell-win" data-bind="cs05.tbl_recall_ce">100% (331 / 331 verified items)</td>\n                <td data-bind-html="cs05.tbl_recall_delta"><strong>+24 caller edges resolved via AST</strong></td>\n              </tr>\n            </tbody>\n          </table>\n        </div>\n\n        <div class="tradeoff-note" style="margin-top: var(--space-lg);">\n          <strong>Methodological &amp; impartiality notes:</strong><br>\n          1. <strong>Controlled baseline scope (pure <code>rg</code> + <code>view_file</code>):</strong> Unlike Case Studies 01 and 02 (where baseline agents had <code>write_to_file</code> access and wrote custom TypeScript/Python AST parsers), neither subagent here had <code>write_to_file</code>, and <code>baseline-agent.md</code> explicitly forbade writing custom AST/graph-walking scripts. This isolates the interactive <code>rg</code> + <code>view_file</code> workflow most coding agents run out of the box.<br>\n          2. <strong>Why <code>CodeExplorer</code> finished in <code>92</code> turns (<code>184</code> steps) vs <code>ripgrep</code>\'s <code>193</code> turns (<code>389</code> steps):</strong> The <code>CodeExplorer</code> MCP agent executed <code>54</code> MCP calls (<code>51</code> <code>execute_custom_read_cypher</code> queries, <code>2</code> <code>get_taxonomy</code> calls, and <code>1</code> <code>get_workspace_status</code> call) plus <code>36</code> targeted <code>view_file</code> calls (<code>31</code> C# source files, <code>1</code> MCP schema, and <code>4</code> spilled outputs). By contrast, <code>ripgrep</code> ran <code>74</code> <code>rg</code> searches and <code>117</code> C# <code>view_file</code> reads, burning <code>4.12×</code> more cumulative input tokens (<code>16.10M</code> vs <code>3.91M</code>).<br>\n          3. <strong>Source-verified output audit (<code>331</code> ground-truth items):</strong> <code>CodeExplorer</code> matched <code>331 / 331</code> items (<code>100%</code>). <code>ripgrep</code> matched <code>307 / 331</code> (<code>92.7%</code>): while it reached <code>100%</code> on Tasks 1, 2, 3, and 4b, on Task 4a (<code>94 / 118</code>) raw <code>rg</code> call-site lines lacked enclosing method headers, leading the agent to guess non-existent method names (<code>AddCreateContentEndpoint</code> instead of <code>HandleAsync</code> on <code>CreateEndpoint</code>, <code>ExecuteAsync</code> instead of <code>SaveAsync</code> on <code>SaveContentItemsStep</code>, <code>BuildMenuShapeAsync</code> instead of <code>DiscoverAsync</code> on <code>MenuShapes</code>, and <code>MetaWeblogGetUserBlogsAsync</code> in place of 5 distinct <code>MetaWeblog*</code> methods on <code>MetaWeblogHandler</code>).\n        </div>\n      </div>\n    </section>\n\n    <section class="band" style="border-bottom: none;">\n      <div class="wrap">\n        <div id="prompts" class="prompt-stack" style="margin-top: 0; padding-top: 0; border-top: none;">\n          <div class="prompt-stack-head">\n            <h2 class="prompt-stack-title">3. Reproducibility: matched prompts &amp; task specification.</h2>\n            <span class="prompt-stack-note">Click any drawer below to expand verbatim prompt text</span>\n          </div>\n          <details class="prompt-drawer">\n            <summary>\n              <span class="prompt-drawer-title">\n                <span class="badge badge--accent">Task Prompt</span>\n                <span>prompts/task-prompt.md: Shared 4-Part OrchardCore Audit Task Given Identically to Both Agents</span>\n              </span>\n              <span class="prompt-drawer-toggle"></span>\n            </summary>\n            <div class="prompt-drawer-body">\n              <pre class="code-block" data-bind-file="benchmarks/05-polyglot-architecture-and-blast-radius/prompts/task-prompt.md">Perform a comprehensive, 4-part architectural governance, security, and blast-radius audit on the `OrchardCMS/OrchardCore` modular .NET monolith located at `./repos/OrchardCore` (`src/OrchardCore/OrchardCore.*` and `src/OrchardCore.Modules/OrchardCore.*`, excluding `test/` projects, `src/Templates/`, and `*Tests*` files). Answer all four engineering questions below with 100% completeness (do not guess, sample, or truncate items). To keep both query outputs and your final report concise, **do not include file path columns** and **use compact grouped bullet lists** rather than wide multi-line Markdown tables:\n\n1. **Task 1 — AppSec Authorization Audit: State-Mutating Controller Actions Missing `AuthorizeAsync` (`Negative Graph Query / NOT EXISTS`)**:\n   Across the following 5 security-sensitive modules:\n   - `OrchardCore.Users`\n   - `OrchardCore.Roles`\n   - `OrchardCore.Tenants`\n   - `OrchardCore.ContentTypes`\n   - `OrchardCore.Workflows`\n   Identify every public action method (`ControllerClass.ActionMethod`) on an MVC/API controller class (inheriting from `Controller` or `ControllerBase`, or decorated with `[Controller]` / `[ApiController]`) that is decorated with `[HttpPost]`, `[HttpPut]`, or `[HttpDelete]` (excluding `[AllowAnonymous]` actions) and:\n   - **(1a)** Calls `IAuthorizationService.AuthorizeAsync` directly (`1` hop) or via a helper method within `2` hops (`1..2` hops), vs\n   - **(1b)** **Does NOT call `AuthorizeAsync` within `1..2` hops** (indicating whether class-level attributes like `[Admin]` or `[Authorize]` are present on the controller).\n   Group by module and report the exact counts and compact list of `ControllerClass.ActionMethod` for both **(1a)** and **(1b)**.\n\n2. **Task 2 — Architectural Boundary Violations: Web &amp; UI Layer Bypassing Services to Access `YesSql.ISession` Directly (`Semantic Invariant Audit`)**:\n   In a clean modular architecture, MVC controllers and UI display drivers should interact with domain services (`IContentManager`, `ISiteService`, stores) rather than querying or mutating the low-level `YesSql.ISession` document session directly.\n   Across all modules under `src/OrchardCore.Modules/*`, identify every non-test **Controller** class (inheriting from `Controller` or `ControllerBase`, or decorated with `[Controller]` / `[ApiController]`) and **Display Driver** class (inheriting from or implementing any `*DisplayDriver*` contract such as `DisplayDriver`, `ContentPartDisplayDriver`, `ContentFieldDisplayDriver`, `SectionDisplayDriver`, or `IDisplayDriver`) whose methods **directly invoke methods on an injected `YesSql.ISession` member** (such as `Query`, `QueryIndex`, `SaveAsync`, `GetAsync`, `CancelAsync`, `CreateConnectionAsync`).\n   Group by module (`.csproj` project name), report the exact count of violating classes and method-to-session operation pairs per module, and list `ClassName.MethodName -&gt; ISession.Operation`.\n\n3. **Task 3 — Event Lifecycle Side-Effect Fan-Out: What Happens Across Modules When Content Is Published? (`Cross-Module Reactive Pipeline`)**:\n   When content is published in `OrchardCore`, the pipeline broadcasts `PublishingAsync` and `PublishedAsync` across content handlers (`IContentHandler`, `ContentHandlerBase`, `ContentPartHandler`, `ContentFieldHandler`, or `IScopedIndexProvider` implementations).\n   Identify every non-test class across `src/OrchardCore/` and `src/OrchardCore.Modules/` that implements or overrides `PublishingAsync` or `PublishedAsync`, and trace its **downstream calls (`1 to 2` hops)** to classify what side-effects each handler triggers:\n   - **Database / Session / Index / Content / Store mutation** (`ISession`, `*Entries`, `*IndexProvider`, `*TaskManager`, `*Store`, `IContentManager`, `IContentDefinitionManager`, `ISiteService`, `IAuditTrailManager`, `ISitemapUpdateHandler`)\n   - **Cache invalidation** (`ITagCache`, `RemoveTagAsync`, `IMemoryCache`, `IDistributedCache`)\n   - **Workflow / Deferred Task / Template trigger** (`IWorkflowManager`, `TriggerEventAsync`, `AddDeferredTask`, `ILiquidTemplateManager`)\n   Report the total count of handler methods (`ClassName.PublishingAsync` / `ClassName.PublishedAsync`) and list `Module -&gt; HandlerClass.MethodName -&gt; [Triggered Downstream Services/Methods]`.\n\n4. **Task 4 — Breaking-Change Blast Radius &amp; Unused Project Dependencies (`Layer 1–3 Cross-Layer Coupling`)**:\n   Suppose we introduce a breaking signature change to these 3 core contract methods on `IContentManager` in `OrchardCore.ContentManagement.Abstractions`:\n   - `IContentManager.GetAsync`\n   - `IContentManager.CreateAsync`\n   - `IContentManager.PublishAsync`\n   Determine across all non-test projects (`!is_test`, excluding `test/` and `src/Templates/`):\n   - **(4a) Direct Call Blast Radius**: Which projects contain classes/methods that invoke at least one of these 3 `IContentManager` methods directly (`1` hop), and how many distinct caller methods (`CallerClass.CallerMethod`) invoke each of `GetAsync`, `CreateAsync`, and `PublishAsync` per project?\n   - **(4b) Over-Coupled Projects (`DEPENDS_ON` Without Call Usage)**: Which non-test projects declare a direct project dependency (`ProjectReference` / `DEPENDS_ON`) on `OrchardCore.ContentManagement.Abstractions`, yet contain **zero calls (`0` call sites)** to any of `GetAsync`, `CreateAsync`, or `PublishAsync`?\n   Report exact project counts and compact lists for both **(4a)** and **(4b)**.</pre>\n            </div>\n          </details>\n\n          <details class="prompt-drawer">\n            <summary>\n              <span class="prompt-drawer-title">\n                <span class="badge badge--win">System Prompt (ce)</span>\n                <span>prompts/graph-agent.md: Symmetric System Instructions for CodeExplorer Agent</span>\n              </span>\n              <span class="prompt-drawer-toggle"></span>\n            </summary>\n            <div class="prompt-drawer-body">\n              <pre class="code-block" data-bind-file="benchmarks/05-polyglot-architecture-and-blast-radius/prompts/graph-agent.md"># Agent System Instructions\n\nYou are a senior software engineer analyzing the `OrchardCMS/OrchardCore` repository (`334` projects, `7,837` files).\nAnswer all architectural questions with 100% accuracy and completeness based on the repository.\n\nCRITICAL BENCHMARK RULES:\n1. Use the **CodeExplorer MCP server (`codeexplorer`)** (passing `workspacePath: "./repos/OrchardCore"` to query the pre-indexed `.codeexplorer/graph.db` semantic graph) and `read_file` to inspect source files when needed. Do not invoke `ce` or `ce.exe` via `run_command`; call the `codeexplorer` MCP tools directly.\n2. To inspect the graph schema (node labels, properties, and relationship types) and query the graph, use the `codeexplorer` MCP tools (`get_taxonomy`, `execute_custom_read_cypher`, `find_symbol`, `get_call_chain`, `get_project_dependencies`, etc.).\n3. You are STRICTLY FORBIDDEN from reading or referencing anything inside `benchmarks/`.\n4. Do NOT modify or create any files inside the repository.</pre>\n            </div>\n          </details>\n\n          <details class="prompt-drawer">\n            <summary>\n              <span class="prompt-drawer-title">\n                <span class="badge badge--cool">System Prompt (ripgrep)</span>\n                <span>prompts/baseline-agent.md: Symmetric System Instructions for ripgrep Baseline Agent</span>\n              </span>\n              <span class="prompt-drawer-toggle"></span>\n            </summary>\n            <div class="prompt-drawer-body">\n              <pre class="code-block" data-bind-file="benchmarks/05-polyglot-architecture-and-blast-radius/prompts/baseline-agent.md"># Agent System Instructions\n\nYou are a senior software engineer analyzing the `OrchardCMS/OrchardCore` repository (`334` projects, `7,837` files).\nAnswer all architectural questions with 100% accuracy and completeness based on the repository.\n\nCRITICAL BENCHMARK RULES:\n1. You have access to the `ripgrep` CLI (`rg`) via `run_command` and `read_file` to inspect source files when needed. Do not write custom AST/graph-walking scripts; use `rg` searches and `read_file`.\n2. You are STRICTLY FORBIDDEN from running `ce`, `ce.exe`, `codeexplorer`, or accessing `.codeexplorer/graph.db` (always pass `-g "!.codeexplorer/**"` if needed).\n3. You are STRICTLY FORBIDDEN from reading or referencing anything inside `benchmarks/`.\n4. Do NOT modify or create any files inside the repository.</pre>\n            </div>\n          </details>\n        </div>\n      </div>\n    </section>'},"/benchmarks/06-precommit-architectural-guard":{routePath:"/benchmarks/06-precommit-architectural-guard",pageKind:"benchmark-case",title:"Case Study 06: Pre-Commit Architectural Guard (CodeExplorer)",description:"",canonical:"https://code-explorer.app/benchmarks/06-precommit-architectural-guard",ogUrl:"https://code-explorer.app/benchmarks/06-precommit-architectural-guard",ogTitle:"Case Study 06: Pre-Commit Architectural Guard (CodeExplorer)",ogDescription:"",mainHtml:`<section class="hero-stage">
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
              Continuous architectural governance showcase on <code>argoproj/argo-cd</code> (Kubernetes continuous delivery platform with <code>18</code> subsystems and <code>4,216</code> files). Demonstrates how CodeExplorer Semantic Invariant Gates (SIG) evaluate 6 production rules in 422 ms with zero LLM tokens ($0.00), freeze legacy technical debt into line-shift-immune SHA-256 baselines, block single-line PR regressions, and monotonically tighten architectural ceilings.
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
            <p class="panel-copy" data-bind-html="cs06.card_tokens_copy"><strong>0 tokens ($0.00 cost)</strong>. Runs entirely offline as a compiled native binary with zero network calls.</p>
            <code class="panel-code" data-bind="cs06.card_cost_footer">Zero API cost / local pre-commit execution</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading tone-cool" data-bind="cs06.card_cli">Exit 1 / 0</span>
            <h2 class="panel-title">Native CI/CD Blocker</h2>
            <p class="panel-copy" data-bind-html="cs06.card_cli_copy">Blocks Day 0 debt (<code>Exit 1</code>), freezes SHA-256 baselines (<code>Exit 0</code>), and rejects any single new PR leak.</p>
            <code class="panel-code" data-bind="cs06.card_cli_footer">Line-shift immune SHA-256 ratchet</code>
          </div>
          <div class="panel panel--sunken">
            <span class="reading" data-bind="cs06.card_recall">6 of 6</span>
            <h2 class="panel-title">Architectural Invariants</h2>
            <p class="panel-copy" data-bind-html="cs06.card_recall_copy">Evaluated all 6 rules: 2 PASS, 1 WARN, 3 FAIL with exact call sites, line numbers, and AST symbols.</p>
            <code class="panel-code" data-bind="cs06.card_recall_footer">3 layer leaks &amp; 12 god types caught</code>
          </div>
        </div>

        <div class="tradeoff-note" style="margin-top: var(--space-lg);" data-bind-html="cs06.participants_html"><strong>Evaluated system &amp; configuration:</strong><br>
          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>v1.80.2</code> (.NET 11, SQLite + OpenCypher, Semantic Invariant Gates &amp; SHA-256 Baseline Ratchet, <code>45,616</code> nodes, <code>134,005</code> edges)<br>
          2. <strong>Evaluation Mode:</strong> 4-Phase CLI Lifecycle (<code>ce gate check</code> + <code>ce gate ratchet --update</code> + <code>ce gate details --new</code>): 0 tokens, $0.00<br>
          3. <strong>Target Repository:</strong> <code>argoproj/argo-cd</code> (commit <code>6d689fa3</code>, <code>18</code> subsystems, <code>4,216</code> files)</div>
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
            <p class="panel-copy">Foundational utility packages (<code>util/**</code>) must remain self-contained and never directly call package functions in <code>server/**</code>, <code>controller/**</code>, <code>reposerver/**</code>, or <code>cmpserver/**</code>.</p>
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
            <p class="panel-copy">The embedded <code>gitops-engine/**</code> subsystem must remain strictly standalone with zero upward package calls into Argo CD host services or <code>util/**</code>.</p>
            <code class="panel-code">PASS: 0 upward host calls</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Storage Abstraction Boundary</h3>
              <code class="badge badge--cool">SIG-P-B-104</code>
            </div>
            <p class="panel-copy">Raw Kubernetes secret repository storage backends (<code>repository_secrets.go</code>) must be encapsulated strictly within <code>util/db</code>.</p>
            <code class="panel-code">PASS: 0 external call sites</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Notification DB Decoupling</h3>
              <code class="badge badge--win">SIG-P-B-105</code>
            </div>
            <p class="panel-copy">Notification utilities in <code>util/notification</code> must not instantiate standalone database instances or call <code>db.NewDB</code> directly.</p>
            <code class="panel-code">FAIL: 2 violation sites in service.go</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Public Auth Bypass Audit</h3>
              <code class="badge badge--accent">SIG-M-B-106</code>
            </div>
            <p class="panel-copy">All server endpoints implementing <code>AuthFuncOverride</code> bypass standard authentication and must be explicitly audited and allowlisted.</p>
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
            Evaluated natively on <code>argoproj/argo-cd @ 6d689fa3</code> via <code>ce gate check</code> in 422 ms total wall-clock time (330 ms pure OpenCypher query execution) across 45,616 graph nodes and 134,005 relationships.
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
            <p class="panel-copy">Run <code>ce gate check</code> on unbaselined <code>argo-cd</code>: surfaces all <code>17</code> existing errors and <code>4</code> auth overrides in <code>422 ms</code>.</p>
            <code class="panel-code">Exit 1: 21 new findings</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Line-Shift-Immune Freeze</h3>
              <span class="badge badge--win">Phase 2</span>
            </div>
            <p class="panel-copy">Run <code>ce gate ratchet --update</code> to snapshot SHA-256 AST hashes. Inserting <code>+15</code> header lines in <code>service.go</code> triggers zero false alarms.</p>
            <code class="panel-code">Exit 0: 0 new, 21 tolerated (356 ms)</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Surgical PR Blocker</h3>
              <span class="badge badge--cool">Phase 3</span>
            </div>
            <p class="panel-copy">A PR adds <code>GetClusterSecretCount</code> calling <code>db.NewDB</code>. All <code>21</code> legacy items stay quiet; <code>ce gate details --new</code> isolates only the <code>1 new</code> leak.</p>
            <code class="panel-code">Exit 1: 1 new at service.go:188</code>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">Monotonic Debt Ratchet</h3>
              <span class="badge badge--win">Phase 4</span>
            </div>
            <p class="panel-copy">Refactoring <code>GetCommitMetadata</code> drops <code>SIG-P-B-105</code> from <code>2</code> to <code>1</code>. <code>ce</code> detects the improvement and locks the lower ceiling permanently.</p>
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
            Every violation reported across the failing and warning gates was verified directly against the Go and TypeScript source files at commit <code>6d689fa3</code>.
          </p>
        </div>

        <div class="panels panels--2">
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">3 Reverse Layer Leaks &amp; 2 Direct DB Calls</h3>
              <code class="badge badge--loss">SIG-P-B-101 &amp; 105</code>
            </div>
            <p class="panel-copy">
              <strong>SIG-P-B-101 (<code>util/**</code> upward calls):</strong><br>
              1. <code>util/session/sessionmanager.go:288</code>: <code>Parse</code> &rarr; <code>GetProjectRoleFromSubject</code> (<code>server/rbacpolicy</code>)<br>
              2. <code>util/app/discovery/discovery.go:171</code>: <code>cmpSupports</code> &rarr; <code>cmpserver/apiclient</code><br>
              3. <code>util/webhook/webhook.go:500</code>: <code>storePreviouslyCachedManifests</code> &rarr; <code>NewManifestKey</code> (<code>reposerver/cache</code>)<br><br>
              <strong>SIG-P-B-105 (<code>util/notification</code> direct DB instantiation):</strong><br>
              1. <code>util/notification/argocd/service.go:66</code>: <code>GetCommitMetadata</code> &rarr; <code>db.NewDB</code><br>
              2. <code>util/notification/argocd/service.go:89</code>: <code>GetAppDetails</code> &rarr; <code>db.NewDB</code>
            </p>
          </div>
          <div class="panel">
            <div class="panel-head">
              <h3 class="panel-title">12 Polyglot God Types &amp; 4 Public Auth Overrides</h3>
              <code class="badge badge--accent">SIG-C-R-102 &amp; 106</code>
            </div>
            <p class="panel-copy">
              <strong>SIG-C-R-102 (&gt;30 methods across Go &amp; TypeScript):</strong><br>
              <code>SettingsManager</code> (<code>70</code>), <code>ApplicationController</code> (<code>64</code>), <code>client</code> (<code>61</code>), <code>Server</code> (<code>59</code>), <code>nativeGitClient</code> (<code>50</code>), <code>store</code> (<code>44</code>), <code>ArgoCDSettings</code> (<code>38</code>), Angular <code>ApplicationsService</code> (<code>36</code> in <code>applications-service.ts</code>), <code>db</code> (<code>35</code>), <code>Service</code> (<code>35</code>), <code>syncContext</code> (<code>35</code>), and <code>Cache</code> (<code>34</code>).<br><br>
              <strong>SIG-M-B-106 (<code>AuthFuncOverride</code> public gRPC endpoints):</strong><br>
              <code>server/session/session.go:103</code> (login), <code>server/settings/settings.go:202</code> (UI login config), <code>server/version/version.go:76</code> (CLI version), and <code>server/server.go:969</code> (<code>healthServer</code> probes).
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
    </section>`},"/privacy":{routePath:"/privacy",pageKind:"privacy",title:"Privacy Policy · CodeExplorer",description:"CodeExplorer is a 100% local, privacy-first developer tool. Zero telemetry, zero cloud dependencies, zero source code transmission.",canonical:"https://code-explorer.app/privacy",ogUrl:"https://code-explorer.app/privacy",ogTitle:"Privacy Policy · CodeExplorer",ogDescription:"CodeExplorer is a 100% local, privacy-first developer tool. Zero telemetry, zero cloud dependencies, zero source code transmission.",mainHtml:`<h1>Privacy Policy</h1>\r
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
      </div>`}};function pu(){if(typeof window>"u"||typeof document>"u")return()=>{};const S=[{src:"all_services.png",description:"<strong>Full System Architecture Map:</strong> Every microservice, REST/gRPC endpoint, RabbitMQ/Kafka topic, and database table rendered automatically from your code. Double-click any service to drill into internal call chains.",pills:['<span class="metric-chip metric-chip--accent">Always Synced With Code</span>','<span class="metric-chip metric-chip--ok">Sub-10ms SQLite Queries</span>','<span class="metric-chip">Click image to enlarge</span>']},{src:"selected_services.png",description:"<strong>Focused Dependency Slice (Blast Radius):</strong> Pick only the services relevant to your task and hide the rest. CodeExplorer collapses intermediate hops into dashed indirect edges so you see the exact blast radius without visual clutter.",pills:['<span class="metric-chip metric-chip--accent">Blast Radius View</span>','<span class="metric-chip metric-chip--ok">Zero Noise Focus</span>','<span class="metric-chip">Click image to enlarge</span>']},{src:"project_flow.png",description:"<strong>Cross-Service Call Flows (API → Queue → DB):</strong> Trace synchronous HTTP/gRPC calls, asynchronous message broker publications, and SQL table reads/writes across project boundaries before modifying a single line of code.",pills:['<span class="metric-chip metric-chip--accent">End-to-End Call Chains</span>','<span class="metric-chip metric-chip--ok">SQL &amp; Broker Lineage</span>','<span class="metric-chip">Click image to enlarge</span>']}];function D(L,te){const $=S[L];if(!$)return;const Te=document.getElementById("showcaseImage"),Je=document.getElementById("showcaseDescription"),Me=document.getElementById("showcasePills"),vt=document.getElementById("showcasePathLabel");document.querySelectorAll("#cockpit .tab-btn").forEach(It=>It.classList.remove("active")),te&&te.classList.add("active"),Te.style.opacity="0.35",setTimeout(()=>{Te.src=$.src,Je.innerHTML=$.description,Me.innerHTML=$.pills.join(""),vt&&(vt.textContent=$.src),Te.style.opacity="1"},120)}function g(){const L=document.getElementById("lightboxModal"),te=document.getElementById("lightboxImg"),$=document.getElementById("showcaseImage");te.src=$.src,L.classList.add("open")}function z(){document.getElementById("lightboxModal").classList.remove("open")}document.addEventListener("keydown",L=>{L.key==="Escape"&&z()});const H={blast_radius:{tag:"blast_radius.cypher",raw:'MATCH path = (caller:Method)-[:CALLS*1..4]->(target:Method {name: "ChargeAsync"}) RETURN caller.fullName AS UpstreamCaller, length(path) AS Hops ORDER BY Hops ASC LIMIT 10',codeHtml:`<span class="syn-kw">MATCH</span> path = (caller:Method)-[:CALLS*<span class="syn-num">1..4</span>]-&gt;(target:Method {name: <span class="syn-str">"ChargeAsync"</span>})
<span class="syn-kw">RETURN</span> caller.fullName <span class="syn-kw">AS</span> UpstreamCaller,
       <span class="syn-fn">length</span>(path) <span class="syn-kw">AS</span> Hops
<span class="syn-kw">ORDER BY</span> Hops <span class="syn-kw">ASC</span>
<span class="syn-kw">LIMIT</span> <span class="syn-num">10</span>`,explanation:"Finds all upstream callers within 4 hops that can impact PaymentProcessor.ChargeAsync.",timing:"4 rows · 3.8 ms",headers:["UpstreamCaller","Hops"],rows:[["Billing.Application.Commands.CaptureOrderHandler.Handle","1"],["Orders.Api.Controllers.CheckoutController.SubmitOrder","2"],["Subscriptions.Worker.Consumers.RenewalConsumer.Consume","2"],["Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute","3"]]},cqrs_pipeline:{tag:"cqrs_pipeline.cypher",raw:"MATCH (ep:Endpoint)-[:CALLS*1..2]->(cmd:Class)-[:HANDLED_BY]->(h:Class)-[:WRITES_TO]->(t:SqlTable) RETURN ep.route AS Route, cmd.name AS Command, h.name AS Handler, t.name AS Table",codeHtml:`<span class="syn-kw">MATCH</span> (ep:Endpoint)-[:CALLS*<span class="syn-num">1..2</span>]-&gt;(cmd:Class)-[:HANDLED_BY]-&gt;(h:Class)-[:WRITES_TO]-&gt;(t:SqlTable)
<span class="syn-kw">RETURN</span> ep.route <span class="syn-kw">AS</span> Route,
       cmd.name <span class="syn-kw">AS</span> Command,
       h.name <span class="syn-kw">AS</span> Handler,
       t.name <span class="syn-kw">AS</span> Table`,explanation:"Traces HTTP endpoints through MediatR/CQRS commands to handlers and mutated SQL tables.",timing:"3 rows · 4.2 ms",headers:["Route","Command","Handler","Table"],rows:[["POST /api/v1/orders","CreateOrderCommand","CreateOrderCommandHandler","orders.purchase_orders"],["POST /api/v1/refunds","IssueRefundCommand","IssueRefundCommandHandler","billing.ledger_entries"],["PUT /api/v1/inventory/reserve","ReserveStockCommand","ReserveStockHandler","warehouse.stock_reservations"]]},api_to_db:{tag:"api_to_db.cypher",raw:'MATCH (s:Service)-[:CONTAINS*]->(m:Method)-[op:READS_FROM|WRITES_TO]->(tbl:SqlTable {name: "customer_ledgers"}) RETURN s.name AS Service, m.name AS Method, type(op) AS Operation',codeHtml:`<span class="syn-kw">MATCH</span> (s:Service)-[:CONTAINS*]-&gt;(m:Method)-[op:READS_FROM|WRITES_TO]-&gt;(tbl:SqlTable {name: <span class="syn-str">"customer_ledgers"</span>})
<span class="syn-kw">RETURN</span> s.name <span class="syn-kw">AS</span> Service,
       m.name <span class="syn-kw">AS</span> Method,
       <span class="syn-fn">type</span>(op) <span class="syn-kw">AS</span> Operation`,explanation:"Discovers every microservice and repository method reading from or writing to a shared database table.",timing:"4 rows · 2.9 ms",headers:["Service","Method","Operation"],rows:[["Billing.Service","LedgerRepository.AppendEntryAsync","WRITES_TO"],["Billing.Service","BalanceQueryService.GetBalanceAsync","READS_FROM"],["Reporting.Worker","DailyReconciliationJob.ExportLedgers","READS_FROM"],["Fraud.Engine","RiskScorer.EvaluateRecentVelocity","READS_FROM"]]},cyclic_deps:{tag:"cyclic_deps.cypher",raw:"MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON*1..3]->(a) WHERE a.name < b.name RETURN a.name AS ProjectA, b.name AS ProjectB",codeHtml:`<span class="syn-kw">MATCH</span> (a:Project)-[:DEPENDS_ON]-&gt;(b:Project)-[:DEPENDS_ON*<span class="syn-num">1..3</span>]-&gt;(a)
<span class="syn-kw">WHERE</span> a.name &lt; b.name
<span class="syn-kw">RETURN</span> a.name <span class="syn-kw">AS</span> ProjectA,
       b.name <span class="syn-kw">AS</span> ProjectB`,explanation:"Detects direct or multi-hop circular project dependencies across the solution.",timing:"2 rows · 1.9 ms",headers:["ProjectA","ProjectB"],rows:[["Orders.Application","Shared.Fulfillment.Contracts"],["Identity.Core","Tenancy.Resolution"]]}};let ae="blast_radius";function fe(L,te){const $=H[L];if(!$)return;ae=L,document.querySelectorAll("#playground .preset-btn").forEach(Me=>Me.classList.remove("active")),te&&te.classList.add("active"),document.getElementById("queryPresetTag").textContent=$.tag,document.getElementById("queryCodeArea").innerHTML=$.codeHtml,document.getElementById("queryExplanation").textContent=$.explanation,document.getElementById("queryTimingBadge").textContent=$.timing;const Te=$.headers.map(Me=>`<th>${Me}</th>`).join(""),Je=$.rows.map(Me=>`<tr>${Me.map(vt=>`<td>${vt}</td>`).join("")}</tr>`).join("");document.getElementById("queryResultsArea").innerHTML=`<table class="data-table"><thead><tr>${Te}</tr></thead><tbody>${Je}</tbody></table>`}function ye(){const L=document.getElementById("runQueryBtn"),te=document.getElementById("queryTimingBadge"),$=H[ae];L.textContent="Running...",te.textContent="Executing query...",setTimeout(()=>{L.textContent="Run Query",te.textContent=$.timing},180)}function ne(){const te=`ce query '${H[ae].raw}'`;ke(te,"Copied OpenCypher CLI command!")}const ge={boundaries:{file:".codeexplorer/gates/domain-purity.yaml",dimension:"Dimension: Boundaries (L2/L3)",description:"Blocks any type inside a Domain module from depending on Infrastructure or EF Core types.",status:"Exit Code 1 · 2 Violations",statusOk:!1,rawYaml:`id: ARCH-001
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
  <span class="syn-kw">RETURN</span> d.fullName <span class="syn-kw">AS</span> Violator, inf.fullName <span class="syn-kw">AS</span> ForbiddenTarget`,violations:[{rule:"ARCH-001 (error)",target:"Orders.Domain.Aggregates.Order -> Orders.Infrastructure.Persistence.OrderDbContext",loc:"src/Orders.Domain/Aggregates/Order.cs:84"},{rule:"ARCH-001 (error)",target:"Billing.Domain.Services.InvoiceCalculator -> Billing.Infrastructure.Http.TaxRateClient",loc:"src/Billing.Domain/Services/InvoiceCalculator.cs:41"}]},topology:{file:".codeexplorer/gates/no-shared-database-writes.yaml",dimension:"Dimension: Topology (L4/L5)",description:"Forbids two distinct microservices from writing to the same SQL table directly.",status:"Exit Code 1 · 1 Violation",statusOk:!1,rawYaml:`id: TOPO-004
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
  <span class="syn-kw">RETURN</span> t.name <span class="syn-kw">AS</span> SharedTable, s1.name <span class="syn-kw">AS</span> WriterA, s2.name <span class="syn-kw">AS</span> WriterB`,violations:[{rule:"TOPO-004 (error)",target:'Table "inventory.stock_items" written by both Checkout.Service and Warehouse.Service',loc:"src/Checkout.Service/Repositories/StockWriter.cs:59"}]},visitor:{file:".codeexplorer/gates/no-sync-over-async.yaml",dimension:"Dimension: AST Visitor (L1/L3)",description:"Catches blocking .Result or .Wait() calls inside async methods that cause thread-pool starvation.",status:"Exit Code 1 · 2 Violations",statusOk:!1,rawYaml:`id: AST-012
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
  <span class="syn-kw">RETURN</span> m.fullName <span class="syn-kw">AS</span> AsyncMethod, b.name <span class="syn-kw">AS</span> BlockingCall`,violations:[{rule:"AST-012 (error)",target:"Payments.Api.WebhookController.HandleStripeAsync calls Task.get_Result",loc:"src/Payments.Api/Controllers/WebhookController.cs:112"},{rule:"AST-012 (error)",target:"Notifications.Worker.EmailDispatcher.SendBatchAsync calls Task.Wait",loc:"src/Notifications.Worker/EmailDispatcher.cs:67"}]},literal:{file:".codeexplorer/gates/no-hardcoded-urls.yaml",dimension:"Dimension: Literal Audit (L1)",description:"Prevents AI agents from hardcoding internal service URLs or connection strings in production classes.",status:"Exit Code 1 · 1 Violation",statusOk:!1,rawYaml:`id: SEC-003
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
  <span class="syn-kw">RETURN</span> c.fullName <span class="syn-kw">AS</span> ClassName, lit.value <span class="syn-kw">AS</span> HardcodedLiteral`,violations:[{rule:"SEC-003 (error)",target:'Shipping.Infrastructure.CarrierClient contains literal "http://carrier-internal.prod:8080/v2"',loc:"src/Shipping.Infrastructure/Clients/CarrierClient.cs:29"}]},ratchet:{file:".codeexplorer/gate-baseline.json",dimension:"Dimension: Ratchet Baseline",description:"Suppresses fingerprinted legacy debt while failing CI immediately on any net-new violation.",status:"Exit Code 0 · 0 Net-New (3 Baselined)",statusOk:!0,rawYaml:`{
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
}`,violations:[{rule:"RATCHET PASS (0 net-new violations)",target:"3 known legacy violations matched baseline fingerprints; 0 new violations introduced in PR.",loc:".codeexplorer/gate-baseline.json · verified in 6.4 ms",ok:!0}]}};let Ce="boundaries";function b(L,te){const $=ge[L];if(!$)return;Ce=L,document.querySelectorAll("#gates .preset-btn").forEach(Me=>Me.classList.remove("active")),te&&te.classList.add("active"),document.getElementById("gateFileLabel").textContent=$.file,document.getElementById("gateDimensionBadge").textContent=$.dimension,document.getElementById("gateYamlArea").innerHTML=$.yamlHtml,document.getElementById("gateDescriptionText").textContent=$.description;const Te=document.getElementById("gateStatusBadge");Te.textContent=$.status,Te.className=$.statusOk?"measured tone-ok":"measured tone-bad";const Je=$.violations.map(Me=>`
        <div class="violation-item ${Me.ok?"violation-item--ok":""}">
          <strong class="${Me.ok?"tone-ok":"tone-bad"}">${Me.rule}</strong>
          <span>${Me.target}</span>
          <span class="tone-muted">${Me.loc}</span>
        </div>
      `).join("");document.getElementById("gateViolationsArea").innerHTML=Je}function M(){const L=ge[Ce];ke(L.rawYaml,"Copied Gate configuration to clipboard!")}function j(L,te){document.querySelectorAll("#install .tab-btn").forEach(Te=>Te.classList.remove("active")),te&&te.classList.add("active"),document.querySelectorAll("#install .install-pane").forEach(Te=>Te.classList.remove("active"));const $=document.getElementById("tab-"+L);$&&$.classList.add("active")}function T(L){ke(L,"Copied install command to clipboard!")}function Le(){ke(`{
  "mcpServers": {
    "code-explorer": {
      "command": "ce",
      "args": ["mcp"]
    }
  }
}`,"Copied MCP Server JSON config!")}function ke(L,te){try{const $=L.includes("brew ")?"/event/copy-brew":L.includes("mcp")?"/event/copy-mcp":"/event/copy-cli";navigator.sendBeacon&&navigator.sendBeacon($)}catch{}if(navigator.clipboard&&navigator.clipboard.writeText)navigator.clipboard.writeText(L).then(()=>it(te));else{const $=document.createElement("textarea");$.value=L,document.body.appendChild($),$.select(),document.execCommand("copy"),document.body.removeChild($),it(te)}}function it(L){const te=document.getElementById("toast");te.textContent=L,te.classList.add("show"),setTimeout(()=>te.classList.remove("show"),2400)}window.switchShowcase=D,window.openLightbox=g,window.closeLightbox=z,window.loadQueryPreset=fe,window.runQueryAnimation=ye,window.copyCurrentQuery=ne,window.loadGatePreset=b,window.copyCurrentGateYaml=M,window.switchInstallTab=j,window.copyInstallCmd=T,window.copyMCPConfig=Le;const yt=document.querySelector("#playground .preset-btn");yt&&fe("blast_radius",yt);const ft=document.querySelector("#gates .preset-btn");return ft&&b("boundaries",ft),fetch("/version.json",{cache:"no-cache"}).then(L=>L.ok?L.json():null).then(L=>{const te=L&&(L.tag_name||L.version);if(te){const $=te.startsWith("v")?te:"v"+te,Te=document.getElementById("heroReleaseBadge"),Je=document.getElementById("footerCeVersion");Te&&(Te.textContent=$),Je&&(Je.textContent="CodeExplorer CLI & Extension "+$)}}).catch(()=>{}),()=>{}}const uu="/",mu=()=>{Tr.useEffect(()=>pu(),[]);const S=jn[uu];return A.jsx("main",{className:"page-home",dangerouslySetInnerHTML:{__html:S.mainHtml}})},hu=()=>A.jsxs(A.Fragment,{children:[A.jsx("div",{className:"lightbox-modal",id:"lightboxModal",role:"dialog","aria-modal":"true","aria-label":"Architecture Cockpit screenshot preview",children:A.jsx("img",{id:"lightboxImg",src:"all_services.png",alt:"Enlarged CodeExplorer Architecture Cockpit view"})}),A.jsx("div",{className:"toast",id:"toast",role:"status","aria-live":"polite",children:"Copied to clipboard!"})]});function gu(){if(typeof window>"u"||typeof document>"u")return()=>{};var S={agnostic:{domain:"/Domain/",app:"/Application/",infra:"/Infrastructure/",ui:"/Api/",modPrefix:"Modules/",slicePrefix:"Features/"},dotnet:{domain:".Domain/",app:".Application/",infra:".Infrastructure/",ui:".Api/",modPrefix:"src/Modules/",slicePrefix:"Features/"},typescript:{domain:"src/domain/",app:"src/application/",infra:"src/infrastructure/",ui:"src/controllers/",modPrefix:"packages/",slicePrefix:"src/features/"},go:{domain:"internal/domain/",app:"internal/usecase/",infra:"internal/adapter/repository/",ui:"internal/adapter/http/",modPrefix:"internal/modules/",slicePrefix:"internal/features/"},python:{domain:"/domain/",app:"/use_cases/",infra:"/adapters/orm/",ui:"/routers/",modPrefix:"apps/",slicePrefix:"features/"},java:{domain:"/domain/",app:"/application/",infra:"/infrastructure/",ui:"/web/",modPrefix:"modules/",slicePrefix:"features/"}},D={"clean-architecture":{title:"Clean Architecture (Onion Dependency Rule)",subtitle:"All source code dependencies point strictly inward toward Domain entities.",note:"Why AST + SQL graph beats import linters: import linters only check file imports. CodeExplorer catches transitive call chains, DI interface resolutions, and raw SQL or ORM table accesses crossing layer boundaries.",kv:[["Best fit","Complex domain rules, long-lived backends"],["Avoid when","Thin CRUD microservices or prototypes"],["Graph layers used","L2 Project + L3 Call/Type + L4 SQL/ORM"],["Legacy repo onboarding","SHA-256 ratchet (0 Day-1 rewrites)"]],layers:function(b){return[{name:"Presentation & Delivery (API / UI / CLI)",paths:b.ui+"**",core:!1},{name:"Infrastructure & Persistence (ORM / HTTP / Queue)",paths:b.infra+"**",core:!1},{name:"Application Use Cases (Commands / Queries / Ports)",paths:b.app+"**",core:!1},{name:"Domain Core (Entities / Value Objects / Policies)",paths:b.domain+"** (0 outward deps)",core:!0}]},edges:[{label:"Presentation to Application",allow:!0,code:"ALLOW"},{label:"Infrastructure to Application & Domain",allow:!0,code:"ALLOW"},{label:"Domain to Infrastructure or Web",allow:!1,code:"SIG-CA-B-001",drawer:"drawer-0"},{label:"Controller direct to DbContext / SQL",allow:!1,code:"SIG-CA-B-003",drawer:"drawer-2"}],gates:function(b,M){return[{id:"SIG-CA-B-001",name:"Domain Core Purity",coords:"Module x Boundaries",pattern:"(Domain)-[:DEPENDS_ON|CALLS]->(Application|Infrastructure|Presentation)",severity:"Error",description:"Domain types and functions must never call or depend on Application, Infrastructure, or Presentation layers.",query:`MATCH (src:Function)-[:CALLS]->(dst:Function)
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
         'Verify outbound RPC call ' + es.name + ' in ' + fn.name + ' uses batching and explicit timeout' AS message`,platform:M}]}}},g="clean-architecture",z="agnostic";function H(b){return String(b).replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function ae(b){var M=b.coords.split(" x ");return["id: "+b.id,"category: Architectural","platform: "+b.platform,"scope: "+M[0],"dimension: "+M[1],"severity: "+b.severity,"name: "+b.name,"description: "+b.description,"query: >-","  "+b.query].join(`
`)}function fe(){var b=D[g],M=S[z],j=b.gates(M,z),T=`# CodeExplorer Semantic Invariant Gate Pack
# Pack: `+g+" | Stack: "+z+`
# Place in .codeexplorer/gates/`+g+`.yaml and run: ce gate check
---
`;return T+j.map(ae).join(`
---
`)+`
`}function ye(){var b=D[g],M=S[z],j=b.layers(M),T=b.gates(M,z);document.getElementById("bp-title").textContent=b.title,document.getElementById("bp-subtitle").textContent=b.subtitle,document.getElementById("bp-note").innerHTML="<strong>Architectural guard note:</strong> "+H(b.note),document.getElementById("pack-slug-badge").textContent="pack: "+g,document.getElementById("cli-cmd").textContent="ce gate install "+g+" --stack "+z;var Le=b.kv.map(function(L){return'<li class="kv-row"><span class="kv-label">'+H(L[0])+'</span><span class="kv-val">'+H(L[1])+"</span></li>"}).join("");document.getElementById("pack-kv").innerHTML=Le;var ke=j.map(function(L){var te=L.core?"topo-layer topo-layer--core":"topo-layer";return'<div class="'+te+'"><span class="topo-layer-name">'+H(L.name)+'</span><span class="topo-layer-paths">'+H(L.paths)+"</span></div>"}).join(""),it='<div class="topo-connectors">'+b.edges.map(function(L){return L.allow?'<div class="edge-rule edge-rule--allow"><span>'+H(L.label)+'</span><code class="edge-rule-code">Allowed</code></div>':'<a href="#'+H(L.drawer)+'" class="edge-rule edge-rule--block" data-open-drawer="'+H(L.drawer)+'"><span>'+H(L.label)+'</span><code class="edge-rule-code">'+H(L.code.toLowerCase())+"</code></a>"}).join("")+"</div>";document.getElementById("topology-stage").innerHTML=ke+it,document.getElementById("invariants-heading").textContent="1. Included Semantic Invariant Gates: "+b.title.split(" (")[0]+".",document.getElementById("table-pack-caption").textContent="Pack: "+g+" ("+T.length+" executable invariants)",document.getElementById("table-stack-caption").textContent="Stack preset: "+z,document.getElementById("gates-tbody").innerHTML=T.map(function(L){var te=L.severity==="Error"?"badge badge--bad":"badge badge--accent";return"<tr><td><code>"+H(L.id)+"</code><br><strong>"+H(L.name)+"</strong></td><td>"+H(L.coords)+"</td><td><code>"+H(L.pattern)+'</code></td><td class="cell-num"><code class="'+te+'">'+H(L.severity)+"</code></td></tr>"}).join("");var yt='<div class="prompt-stack-head"><h3 class="prompt-stack-title">Inspect executable YAML &amp; OpenCypher definitions</h3><span class="prompt-stack-note">Drop directly into <code>.codeexplorer/gates/</code> or adapt paths with your AI agent</span></div>',ft=T.map(function(L,te){var $=te===0?" open":"",Te=L.severity==="Error"?"badge badge--bad":"badge badge--accent";return'<details id="drawer-'+te+'" class="prompt-drawer"'+$+'><summary><span class="prompt-drawer-title"><code class="'+Te+'">'+H(L.id)+"</code><span>"+H(L.name)+' (OpenCypher YAML)</span></span><span class="prompt-drawer-toggle"></span></summary><div class="prompt-drawer-body"><pre class="code-block">'+H(ae(L))+"</pre></div></details>"}).join("");document.getElementById("yaml-drawers").innerHTML=yt+ft,document.querySelectorAll(".arch-tab").forEach(function(L){L.setAttribute("aria-selected",L.getAttribute("data-pack")===g?"true":"false")}),document.querySelectorAll(".stack-btn").forEach(function(L){L.setAttribute("aria-pressed",L.getAttribute("data-stack")===z?"true":"false")})}function ne(){var b=fe(),M=new Blob([b],{type:"text/yaml;charset=utf-8"}),j=URL.createObjectURL(M),T=document.createElement("a");T.href=j,T.download=g+"-"+z+".gates.yaml",document.body.appendChild(T),T.click(),document.body.removeChild(T),URL.revokeObjectURL(j)}function ge(b,M){var j=b.textContent;b.textContent=M,setTimeout(function(){b.textContent=j},1600)}var Ce=function(b){var M=b.target.closest(".arch-tab");if(M){g=M.getAttribute("data-pack"),ye();return}var j=b.target.closest(".stack-btn");if(j){z=j.getAttribute("data-stack"),ye();return}var T=b.target.closest("[data-select-pack]");if(T){g=T.getAttribute("data-select-pack"),ye(),document.getElementById("workbench").scrollIntoView({behavior:"smooth"});return}var Le=b.target.closest("[data-open-drawer]");if(Le){var ke=Le.getAttribute("data-open-drawer"),it=document.getElementById(ke);it&&(b.preventDefault(),it.open=!0,it.scrollIntoView({behavior:"smooth",block:"center"}));return}};return document.addEventListener("click",Ce),document.getElementById("download-pack-btn").addEventListener("click",ne),document.getElementById("hero-download-btn").addEventListener("click",ne),document.getElementById("copy-cli-btn").addEventListener("click",function(){var b=this,M=document.getElementById("cli-cmd").textContent;navigator.clipboard&&navigator.clipboard.writeText(M).then(function(){ge(b,"Copied")})}),document.getElementById("copy-prompt-btn").addEventListener("click",function(){var b=this,M="Inspect my workspace using CodeExplorer get_architecture_map, adapt the following '"+g+"' ("+z+`) Semantic Invariant Gate pack to match our actual folder/project paths, save the rules to .codeexplorer/gates/, run check_semantic_gates, and freeze existing baseline debt via ce gate ratchet --update:

`+fe();navigator.clipboard&&navigator.clipboard.writeText(M).then(function(){ge(b,"Prompt Copied")})}),()=>{document.removeEventListener("click",Ce)}}const yu="/architectures",fu=()=>{Tr.useEffect(()=>gu(),[]);const S=jn[yu];return A.jsx("main",{className:"arch-page",dangerouslySetInnerHTML:{__html:S.mainHtml}})};function vu(){if(typeof window>"u"||typeof document>"u")return()=>{};for(var S={commonProperties:[{name:"id",sourceName:"Id",type:"string",description:"Globally unique deterministic URN identifier of the node."},{name:"name",sourceName:"Name",type:"string",description:"Human-readable symbol, contract, or resource name."},{name:"path",sourceName:"Path",type:"string",description:"Workspace-relative path of the container or declaration."},{name:"project_id",sourceName:"ProjectId",type:"string",description:"URN of the enclosing compilation project, when applicable."}],nodes:[{kind:"Domain",pluralLabel:"Domains",layerId:5,layerKey:"L5_logical",icon:"globe",order:1,idScheme:"{workspaceId}:dom:{domainKey}",purpose:"Represents a high-level business domain (e.g. Core, Supporting, Generic, Shared) containing one or more bounded contexts in Level 5 (Logical Structure).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The canonical domain name."},{name:"display_name",sourceName:"DisplayName",type:"string",description:"The human-readable display name of the domain."},{name:"domain_type",sourceName:"DomainType",type:"enum (DomainType)",description:"The DDD classification of the domain (Core, Supporting, Generic, Shared)."},{name:"summary",sourceName:"Summary",type:"string",description:"A concise summary of the domain's business responsibility."},{name:"path",sourceName:"Path",type:"string",description:"Primary path associated with the domain."}],outEdges:[{rel:"DEPENDS_ON",toKind:"Domain",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",toKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"Domain",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."}],sampleCypher:`MATCH (n:Domain)-[:DEPENDS_ON]->(t:Domain)
RETURN n.name, n.display_name, n.domain_type, t.name
LIMIT 25`},{kind:"BoundedContext",pluralLabel:"Bounded Contexts",layerId:5,layerKey:"L5_logical",icon:"layers",order:2,idScheme:"{workspaceId}:bc:{contextKey}",purpose:"Represents a DDD Bounded Context in Level 5 (Logical Structure) that encapsulates semantic workloads, data stores, topics, and domain types.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The canonical bounded context name."},{name:"display_name",sourceName:"DisplayName",type:"string",description:"The human-readable display name of the bounded context."},{name:"domain_id",sourceName:"DomainId",type:"string",description:"The parent domain ID that owns this bounded context."},{name:"summary",sourceName:"Summary",type:"string",description:"A concise summary of the bounded context's business responsibility."},{name:"purity_percentage",sourceName:"PurityPercentage",type:"number",description:"Domain purity percentage (0-100)."},{name:"path",sourceName:"Path",type:"string",description:"Primary path associated with the bounded context."}],outEdges:[{rel:"DEPENDS_ON",toKind:"BoundedContext",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",toKind:"Database",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Module",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Topic",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DEFINES",toKind:"Type",category:"structural",description:"Indicates that an entity defines a particular property or configuration."}],inEdges:[{rel:"DEPENDS_ON",fromKind:"BoundedContext",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"CONTAINS",fromKind:"Domain",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:BoundedContext)-[:DEPENDS_ON]->(t:BoundedContext)
RETURN n.name, n.display_name, n.domain_id, t.name
LIMIT 25`},{kind:"Module",pluralLabel:"Software Modules",layerId:4,layerKey:"L4_semantic",icon:"package",order:0,idScheme:"{workspaceId}:{kindPrefix}:{moduleName}",purpose:"Abstract base software module encompassing executable workloads, shared libraries, and automated test suites.",isAbstract:!0,subtypes:["Workload","Library","TestSuite"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name."},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace."},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language."},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module."},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework."}],outEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it."}],inEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies."},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Module)-[:DEPENDS_ON]->(t:Library)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Workload",pluralLabel:"Runtime Workloads",layerId:4,layerKey:"L4_semantic",icon:"server",order:1,idScheme:"{workspaceId}:{workloadPrefix}:{workloadName}",purpose:"Abstract executable runtime workload encompassing backend services, client applications, background workers, and CLI tools.",isAbstract:!0,extends:"Module",ancestors:["Module"],subtypes:["App","Service","Worker","CliTool"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to."},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."}],inEdges:[],inheritedOutEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Workload)-[:SERVICE_CALL]->(t:ExternalService)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"App",pluralLabel:"Applications",layerId:4,layerKey:"L4_semantic",icon:"browser",order:2,idScheme:"{workspaceId}:app:{appName}",purpose:"Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular, Electron).",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[],inEdges:[],inheritedOutEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:App)-[:SERVICE_CALL]->(t:ExternalService)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Service",pluralLabel:"Services",layerId:4,layerKey:"L4_semantic",icon:"server-process",order:3,idScheme:"{workspaceId}:s:{serviceName}",purpose:"Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server).",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"CALLED_BY",toKind:"Function",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"EXPOSES",toKind:"Endpoint",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."},{rel:"COORDINATES_WITH",toKind:"PeerMesh",category:"type_dependency",description:"Links a workload to the symmetric peer mesh it coordinates with."}],inEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."}],inheritedOutEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Service)-[:CALLED_BY]->(t:Function)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Worker",pluralLabel:"Workers",layerId:4,layerKey:"L4_semantic",icon:"gear",order:4,idScheme:"{workspaceId}:w:{workerName}",purpose:"Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service).",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"SUBSCRIBES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."}],inEdges:[],inheritedOutEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Worker)-[:SUBSCRIBES_TO]->(t:Topic)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"CliTool",pluralLabel:"CLI Tools",layerId:4,layerKey:"L4_semantic",icon:"terminal",order:5,idScheme:"{workspaceId}:cli:{toolName}",purpose:"Represents a command-line tool, developer script, or administrative CLI utility.",extends:"Workload",ancestors:["Workload","Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[],inEdges:[],inheritedOutEdges:[{rel:"SERVICE_CALL",toKind:"ExternalService",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"SERVICE_CALL",toKind:"Service",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",inheritedFrom:"Workload"},{rel:"PUBLISHES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",inheritedFrom:"Workload"},{rel:"USES_DB",toKind:"Database",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",inheritedFrom:"Workload"},{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",inheritedFrom:"Workload"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:CliTool)-[:SERVICE_CALL]->(t:ExternalService)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Library",pluralLabel:"Libraries & SDKs",layerId:4,layerKey:"L4_semantic",icon:"library",order:6,idScheme:"{workspaceId}:lib:{libraryName}",purpose:"Represents a shared library, utility module, DTO package, or domain contract reused across services.",extends:"Module",ancestors:["Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[],inEdges:[{rel:"DEPENDS_ON",fromKind:"Module",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities."}],inheritedOutEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:Library)-[:DEPENDS_ON]->(t:Library)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"TestSuite",pluralLabel:"Test Suites",layerId:4,layerKey:"L4_semantic",icon:"beaker",order:7,idScheme:"{workspaceId}:suite:{suiteName}",purpose:"Represents an automated test project, runner suite, or test module (e.g. NUnit/xUnit test project, Jest test suite, pytest suite).",extends:"Module",ancestors:["Module"],properties:[{name:"name",sourceName:"Name",type:"string",description:"The module name.",inheritedFrom:"Module"},{name:"path",sourceName:"Path",type:"string",description:"The path of the module directory relative to the workspace.",inheritedFrom:"Module"},{name:"project_type",sourceName:"ProjectType",type:"enum (ProjectLanguage)",description:"The project type or language.",inheritedFrom:"Module"},{name:"role",sourceName:"Role",type:"string",description:"The primary semantic role of the module.",inheritedFrom:"Module"},{name:"framework",sourceName:"Framework",type:"string",description:"The detected application, service, or test framework.",inheritedFrom:"Module"}],outEdges:[{rel:"TESTS",toKind:"Module",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies."}],inEdges:[],inheritedOutEdges:[{rel:"DEPENDS_ON",toKind:"Library",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"DEPENDS_ON",toKind:"Package",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",inheritedFrom:"Module"},{rel:"IMPLEMENTED_BY",toKind:"Project",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",inheritedFrom:"Module"}],inheritedInEdges:[{rel:"TESTS",fromKind:"TestSuite",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",inheritedFrom:"Module"},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",inheritedFrom:"Module"}],sampleCypher:`MATCH (n:TestSuite)-[:TESTS]->(t:Module)
RETURN n.name, n.project_type, n.role, t.name
LIMIT 25`},{kind:"Endpoint",pluralLabel:"API & Message Endpoints (REST, gRPC, WS, MSG)",layerId:4,layerKey:"L4_semantic",icon:"radio-tower",order:10,idScheme:"{workspaceId}:ep:{httpMethod}:{routeTemplate}",purpose:"Represents an exposed external API or message contract endpoint (REST, gRPC, GraphQL, WebSocket, WCF, or Broker Consumer).",properties:[{name:"name",sourceName:"Name",type:"string",description:"The endpoint name (e.g. GET /api/orders, MSG:SubmitOrder)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"http_method",sourceName:"HttpMethod",type:"string",description:"The HTTP Verb or contract verb (GET, POST, PUT, DELETE, ALL, MSG, WS, RPC)."},{name:"route_template",sourceName:"RouteTemplate",type:"string",description:"The declared route template or message contract name."},{name:"protocol",sourceName:"Protocol",type:"string",description:"The contract protocol (REST, gRPC, GraphQL, WS, WCF, MSG, RPC)."},{name:"is_anonymous",sourceName:"IsAnonymous",type:"boolean",description:"Whether endpoint allows unauthenticated anonymous access."},{name:"required_roles",sourceName:"RequiredRoles",type:"string",description:"Comma-separated required roles or permissions."},{name:"policies",sourceName:"Policies",type:"string",description:"Authorization policies guarding the endpoint."},{name:"request_type",sourceName:"RequestType",type:"string",description:"The request payload type (e.g. StartRideRequest, CreateOrderDto)."},{name:"response_type",sourceName:"ResponseType",type:"string",description:"The response payload type (e.g. RideDto, OrderResponse)."},{name:"operation_type",sourceName:"OperationType",type:"enum (OperationType)",description:"The operation type for GraphQL (Query, Mutation, Subscription) or gRPC (Unary, Streaming)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring the endpoint."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed starting line number of the endpoint declaration."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed source line number of the route or contract mapping."},{name:"operation",sourceName:"Operation",type:"string",description:"Operation or handler action name associated with this endpoint."},{name:"evidence_kind",sourceName:"EvidenceKind",type:"string",description:"Extraction provenance kind (endpoint, grpc, graphql, ws, consumer)."},{name:"graphql_field",sourceName:"GraphqlField",type:"string",description:"Exposed GraphQL root field name when protocol is GraphQL."},{name:"graphql_sdl",sourceName:"IsGraphqlSdl",type:"boolean",description:"Indicates whether the endpoint was extracted from a GraphQL SDL schema."},{name:"status_code",sourceName:"StatusCode",type:"string",description:"Declared HTTP response status codes (e.g. 200, 201, 404)."},{name:"summary",sourceName:"Summary",type:"string",description:"OpenAPI / Swagger or docstring endpoint summary."}],outEdges:[{rel:"CALLS",toKind:"Function",category:"control_flow",description:"Links a calling function to the function it directly invokes."},{rel:"EXPOSED_BY",toKind:"Function",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"EXPOSED_BY",toKind:"Type",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it."},{rel:"TRIGGERS",toKind:"Function",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"ATTRIBUTED_TO",toKind:"Database",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"ATTRIBUTED_TO",toKind:"ExternalService",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"ATTRIBUTED_TO",toKind:"Query",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"PASSES_LITERAL",toKind:"CallLiteral",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation."},{rel:"SUBSCRIBES_TO",toKind:"Topic",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",toKind:"Project",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARES",toKind:"CatchClause",category:"structural",description:"Indicates that a container entity declares a sub-entity."}],inEdges:[{rel:"CALLS_ENDPOINT",fromKind:"ExternalService",category:"control_flow",description:"Links an external API call to the target HTTP endpoint it invokes."},{rel:"EXPOSES",fromKind:"Service",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."}],sampleCypher:`MATCH (n:Endpoint)-[:CALLS]->(t:Function)
RETURN n.name, n.http_method, n.route_template, t.name
LIMIT 25`},{kind:"Topic",pluralLabel:"Message Topics & Queues",layerId:4,layerKey:"L4_semantic",icon:"mail",order:11,idScheme:"{workspaceId}:top:{brokerType}:{topicName}",purpose:"Represents a message queue, event exchange, or topic boundary.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the topic or exchange."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"broker_type",sourceName:"BrokerType",type:"enum (BrokerType)",description:"The broker system type (rabbitmq, kafka, sqs, in-memory)."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file declaring or referencing the topic."},{name:"line",sourceName:"Line",type:"integer",description:"1-indexed source line of the publish or subscribe site."},{name:"messaging_direction",sourceName:"MessagingDirection",type:"string",description:"Direction of broker interaction (publish, subscribe, both)."},{name:"scope",sourceName:"Scope",type:"string",description:"Broker scope or virtual host / namespace."},{name:"message_type",sourceName:"MessageType",type:"string",description:"Payload or event contract type published/consumed on this topic."}],outEdges:[{rel:"PUBLISHED_BY",toKind:"Function",category:"data_flow",description:"Links a topic to the function that publishes to it."},{rel:"SUBSCRIBED_BY",toKind:"Function",category:"data_flow",description:"Links a topic to the function that subscribes to it."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"PUBLISHES_TO",fromKind:"Workload",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to."},{rel:"SUBSCRIBES_TO",fromKind:"Endpoint",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{rel:"SUBSCRIBES_TO",fromKind:"Worker",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Topic)-[:PUBLISHED_BY]->(t:Function)
RETURN n.name, n.broker_type, n.file_path, t.name
LIMIT 25`},{kind:"ExternalService",pluralLabel:"External Services & Resources",layerId:4,layerKey:"L4_semantic",icon:"globe",order:12,idScheme:"{workspaceId}:es:{protocol}:{host}",purpose:"Represents an external API, managed cloud resource, or remote service dependency outside the workspace (REST, gRPC, GraphQL, S3, Stripe, Auth0).",properties:[{name:"name",sourceName:"Name",type:"string",description:"Logical host or external client dependency name."},{name:"protocol",sourceName:"Protocol",type:"string",description:"Outbound transport protocol (HTTP, HTTPS, gRPC, GraphQL, WS, WCF)."},{name:"domain_or_service",sourceName:"DomainOrService",type:"string",description:"Target domain hostname or logical service identifier."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"file_path",sourceName:"FilePath",type:"string",description:"Workspace-relative source file issuing the outbound call."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"1-indexed line number of the outbound client call."},{name:"base_url",sourceName:"BaseUrl",type:"string",description:"Configured base URL or service address."},{name:"url",sourceName:"Url",type:"string",description:"Resolved outbound request URL or path template."},{name:"config_key",sourceName:"ConfigKey",type:"string",description:"Configuration setting or environment variable supplying the host URL."},{name:"graphql_field",sourceName:"GraphqlField",type:"string",description:"Queried GraphQL field name on the remote service."},{name:"operation_type",sourceName:"OperationType",type:"string",description:"Remote operation kind (Query, Mutation, Unary, Streaming)."},{name:"method",sourceName:"Method",type:"string",description:"Outbound HTTP verb or RPC method name."},{name:"provider",sourceName:"Provider",type:"string",description:"Cloud platform or SaaS vendor hosting this external service (e.g. AWS, GCP, Azure, Stripe, Auth0)."},{name:"resource_role",sourceName:"ResourceRole",type:"string",description:"Operational role when representing a managed cloud resource (storage, secrets, auth, compute, cdn, ai)."},{name:"is_external",sourceName:"IsExternal",type:"boolean",description:"True when the target resolves outside workspace services."}],outEdges:[{rel:"CALLED_BY",toKind:"Function",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it."},{rel:"CALLS_ENDPOINT",toKind:"Endpoint",category:"control_flow",description:"Links an external API call to the target HTTP endpoint it invokes."},{rel:"TRANSITIVELY_CALLS",toKind:"Function",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"ACCESSED_BY",toKind:"Type",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",toKind:"Project",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"SERVICE_CALL",fromKind:"Workload",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{rel:"ATTRIBUTED_TO",fromKind:"Endpoint",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."}],sampleCypher:`MATCH (n:ExternalService)-[:CALLED_BY]->(t:Function)
RETURN n.name, n.protocol, n.domain_or_service, t.name
LIMIT 25`},{kind:"Cloud",pluralLabel:"Cloud Platforms & Vendors",layerId:4,layerKey:"L4_semantic",icon:"cloud",order:13,idScheme:"{workspaceId}:cloud:{provider}",purpose:"Represents a cloud platform, managed infrastructure provider, or SaaS vendor hosting external services, databases, brokers, or workloads (e.g. AWS, GCP, Azure, Cloudflare, Stripe, Supabase).",properties:[{name:"name",sourceName:"Name",type:"string",description:"Cloud platform or vendor display name (e.g. AWS, GCP, Azure, Cloudflare, Stripe)."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"provider",sourceName:"Provider",type:"string",description:"Normalized cloud or vendor identifier (aws, gcp, azure, cloudflare, stripe, auth0, supabase, firebase)."},{name:"region",sourceName:"Region",type:"string",description:"Configured cloud region when declared (e.g. us-east-1, europe-west1)."},{name:"category",sourceName:"Category",type:"string",description:"Vendor category (hyperscaler, saas, edge, managed_data)."}],outEdges:[{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"HOSTED_IN",fromKind:"Database",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"ExternalService",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"Topic",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"HOSTED_IN",fromKind:"Workload",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."}],sampleCypher:`MATCH (n:Cloud)-[:DECLARED_IN]->(t:File)
RETURN n.name, n.provider, n.region, t.name
LIMIT 25`},{kind:"PeerMesh",pluralLabel:"Peer Meshes",layerId:4,layerKey:"L4_semantic",icon:"share-alt",order:14,idScheme:"{workspaceId}:mesh:peer-network",purpose:"Represents a symmetric peer-to-peer cluster or collective topology.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the peer mesh."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"member_count",sourceName:"MemberCount",type:"integer",description:"The number of configured peer members, when known."},{name:"protocols",sourceName:"Protocols",type:"string",description:"The detected communication protocols."}],outEdges:[],inEdges:[{rel:"COORDINATES_WITH",fromKind:"Service",category:"type_dependency",description:"Links a workload to the symmetric peer mesh it coordinates with."}],sampleCypher:`MATCH (s:Service)-[:COORDINATES_WITH]->(n:PeerMesh)
RETURN s.name, n.name, n.member_count, n.protocols
LIMIT 25`},{kind:"Database",pluralLabel:"Databases & Storage",layerId:4,layerKey:"L4_semantic",icon:"database",order:20,idScheme:"{workspaceId}:db:{dbType}:{dbName}",purpose:"Represents a database server instance or catalog configured by a workload and containing one or more DbSchema namespaces.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The database engine or catalog name."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"db_type",sourceName:"DbType",type:"enum (DatabaseType)",description:"The database system type (sqlserver, postgres, sqlite, mongodb, neo4j)."},{name:"display_name",sourceName:"DisplayName",type:"string",description:"Human-readable database or catalog display name."},{name:"engine",sourceName:"Engine",type:"string",description:"Detected database engine (e.g. postgresql, sqlserver, sqlite, mysql, mongodb, redis)."},{name:"technology",sourceName:"Technology",type:"string",description:"ORM, driver, or client technology accessing the database (e.g. EF Core, Dapper, Prisma, TypeORM)."},{name:"schema",sourceName:"Schema",type:"string",description:"Physical database schema name when declared (e.g. public, dbo, billing)."},{name:"provider",sourceName:"Provider",type:"string",description:"Database driver or provider package identifier."},{name:"role",sourceName:"Role",type:"string",description:"Storage role classification (primary, cache, analytics, read-replica)."},{name:"config_key",sourceName:"ConfigKey",type:"string",description:"Configuration key or connection string name bound to this database."},{name:"from_config",sourceName:"FromConfig",type:"boolean",description:"Indicates whether the database connection was discovered from configuration files."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether this database instance is used exclusively in tests."},{name:"is_orm",sourceName:"IsOrm",type:"boolean",description:"Indicates whether access was inferred via an ORM entity/DbContext mapping."}],outEdges:[{rel:"TARGETED_BY",toKind:"CallArgument",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{rel:"HOSTED_IN",toKind:"Cloud",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{rel:"CONTAINS",toKind:"DbSchema",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"ATTRIBUTED_TO",fromKind:"Endpoint",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{rel:"USES_DB",fromKind:"Workload",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes."},{rel:"CONTAINS",fromKind:"BoundedContext",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (n:Database)-[:TARGETED_BY]->(t:CallArgument)
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
LIMIT 25`},{kind:"Member",pluralLabel:"Members & Fields",layerId:3,layerKey:"L3_syntactic",icon:"symbol-field",order:3,idScheme:"{workspaceId}:sym:{filePath}:Member:{name}:{line}",purpose:"Represents a declared field, property, parameter, or local variable.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"symbol",sourceName:"Symbol",type:"string",description:"A globally unique ID for this symbol scope."},{name:"file_path",sourceName:"FilePath",type:"string",description:"The relative path of the declaring file."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"start_line",sourceName:"StartLine",type:"integer",description:"The starting line number (1-indexed) of the declaration."},{name:"kind",sourceName:"MemberKind",type:"enum (MemberKind)",description:"The specific member kind (field, property, parameter, variable)."},{name:"data_type",sourceName:"DataType",type:"string",description:"The declared data type of the member."},{name:"parameter_index",sourceName:"ParameterIndex",type:"integer",description:"0-based formal parameter index when the member is a function parameter."},{name:"variable_type",sourceName:"VariableClassification",type:"string",description:"Variable classification tags (e.g. parameter, config, etl, constant, global, property, field)."},{name:"initializer_expression",sourceName:"InitializerExpression",type:"string",description:"Bounded initializer expression text."},{name:"is_constant",sourceName:"IsConstant",type:"boolean",description:"Indicates whether the member is a compile-time or immutable constant."},{name:"attributes",sourceName:"Attributes",type:"string",description:"Comma-separated annotations, attributes, or decorators applied to this member."}],outEdges:[{rel:"ACCESSED_BY",toKind:"Function",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{rel:"OF_TYPE",toKind:"Type",category:"type_dependency",description:"Links a member variable or field to its declared type."},{rel:"DECLARED_IN",toKind:"File",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],inEdges:[{rel:"DECLARES",fromKind:"Function",category:"structural",description:"Indicates that a container entity declares a sub-entity."},{rel:"HAS_MEMBER",fromKind:"Type",category:"structural",description:"Links a type declaration to its declared member variables or fields."}],sampleCypher:`MATCH (n:Member)-[:ACCESSED_BY]->(t:Function)
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
LIMIT 25`},{kind:"File",pluralLabel:"Files",layerId:1,layerKey:"L1_physical",icon:"file-code",order:1,idScheme:"{workspaceId}:f:{relativeFilePath}",purpose:"Represents a source code file containing parsable content.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"is_test",sourceName:"IsTest",type:"boolean",description:"Indicates whether the file is part of a test suite or test project."},{name:"project_kind",sourceName:"ProjectKind",type:"enum (ProjectEntityKind)",description:"The architectural kind of the enclosing project."},{name:"project_name",sourceName:"ProjectName",type:"string",description:"The name of the enclosing project."},{name:"line_count",sourceName:"LineCount",type:"integer",description:"Total lines of code in this file."},{name:"size_bytes",sourceName:"SizeBytes",type:"integer",description:"Size of the file in bytes."},{name:"artifact_role",sourceName:"ArtifactRole",type:"string",description:"The physical artifact role of the file (source, dist, minified, declaration, script)."}],outEdges:[],inEdges:[{rel:"TRIGGERS",fromKind:"EntryPoint",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers."},{rel:"CONTAINS",fromKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",fromKind:"Project",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"DECLARED_IN",fromKind:"CallArgument",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"CallLiteral",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"CatchClause",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Cloud",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Database",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"DbSchema",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Endpoint",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"EntryPoint",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"ExternalService",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Function",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Member",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Parameter",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Query",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Topic",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."},{rel:"DECLARED_IN",fromKind:"Type",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file."}],sampleCypher:`MATCH (s:EntryPoint)-[:TRIGGERS]->(n:File)
RETURN s.name, n.name, n.is_test, n.project_kind
LIMIT 25`},{kind:"Folder",pluralLabel:"Folders",layerId:1,layerKey:"L1_physical",icon:"folder",order:2,idScheme:"{workspaceId}:dir:{relativeDirectoryPath}",purpose:"Represents a directory within the indexed workspace.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the folder."},{name:"path",sourceName:"Path",type:"string",description:"The path to the folder."}],outEdges:[{rel:"CONTAINS",toKind:"File",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"CONTAINS",toKind:"GitSettings",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],inEdges:[{rel:"CONTAINS",fromKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."},{rel:"LOCATED_IN",fromKind:"Project",category:"structural",description:"Links a project to the physical folder or files structure where it is located."}],sampleCypher:`MATCH (n:Folder)-[:CONTAINS]->(t:File)
RETURN n.name, n.path, t.name
LIMIT 25`},{kind:"GitSettings",pluralLabel:"Git Settings",layerId:1,layerKey:"L1_physical",icon:"git-commit",order:3,idScheme:"{workspaceId}:git",purpose:"Represents the Git repository configuration settings for the workspace.",properties:[{name:"name",sourceName:"Name",type:"string",description:"The name of the entity."},{name:"branch",sourceName:"Branch",type:"string",description:"The currently checked-out branch name."},{name:"origin_url",sourceName:"OriginUrl",type:"string",description:"The remote origin repository URL."},{name:"user_name",sourceName:"UserName",type:"string",description:"The git user name."},{name:"user_email",sourceName:"UserEmail",type:"string",description:"The git user email address."},{name:"path",sourceName:"Path",type:"string",description:"The path of the folder or file relative to its parent container."},{name:"commit_hash",sourceName:"CommitHash",type:"string",description:"Current Git HEAD commit hash."},{name:"head_commit",sourceName:"HeadCommit",type:"string",description:"Current Git HEAD commit hash alias."},{name:"repo_name",sourceName:"RepoName",type:"string",description:"Git repository name."}],outEdges:[],inEdges:[{rel:"USES_GIT",fromKind:"Project",category:"type_dependency",description:"Links the workspace to its repository configuration."},{rel:"CONTAINS",fromKind:"Folder",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements."}],sampleCypher:`MATCH (s:Project)-[:USES_GIT]->(n:GitSettings)
RETURN s.name, n.name, n.branch, n.origin_url
LIMIT 25`}],relationships:[{rel:"CALLED_BY",category:"control_flow",description:"Links a service, external service, or database to the function that invokes or queries it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"ExternalService",toKind:"Function"},{fromKind:"Service",toKind:"Function"}]},{rel:"CALLS",category:"control_flow",description:"Links a calling function to the function it directly invokes.",properties:[{name:"line",type:"integer",description:"1-indexed source line of the primary invocation site."},{name:"call_count",type:"integer",description:"Aggregated number of call sites between caller and callee."},{name:"in_loop",type:"boolean",description:"True when invoked inside a for/foreach/while/do loop (N+1 risk signal)."},{name:"loop_depth",type:"integer",description:"Nesting depth of enclosing loops at the call site."},{name:"is_conditional",type:"boolean",description:"True when guarded by an if/switch/ternary branch."},{name:"in_try",type:"boolean",description:"True when enclosed inside a try/catch block."},{name:"arg_flow",type:"string",description:"Parameter-to-argument dataflow slot mapping across the call boundary."}],pairs:[{fromKind:"Endpoint",toKind:"Function"},{fromKind:"Function",toKind:"Function"},{fromKind:"Function",toKind:"Type"},{fromKind:"Type",toKind:"Function"}]},{rel:"CALLS_ENDPOINT",category:"control_flow",description:"Links an external API call to the target HTTP endpoint it invokes.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"ExternalService",toKind:"Endpoint"}]},{rel:"EXPOSED_BY",category:"control_flow",description:"Links an entrypoint or endpoint to the type or function that exposes it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"Function"},{fromKind:"Endpoint",toKind:"Type"}]},{rel:"EXPOSES",category:"control_flow",description:"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Service",toKind:"Endpoint"}]},{rel:"HAS_ENTRYPOINT",category:"control_flow",description:"Links an executable ProjectNode to its binary or process EntryPointNode.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Project",toKind:"EntryPoint"}]},{rel:"IMPLEMENTS",category:"control_flow",description:"Links a class/struct declaration to the interface it implements.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"EntryPoint"},{fromKind:"Type",toKind:"Type"}]},{rel:"INSTANTIATES",category:"control_flow",description:"Links a host component to a runtime UI or subsystem component it instantiates.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"EntryPoint"},{fromKind:"Function",toKind:"Function"},{fromKind:"Function",toKind:"Type"}]},{rel:"SERVICE_CALL",category:"control_flow",description:"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Workload",toKind:"ExternalService"},{fromKind:"Workload",toKind:"Service"}]},{rel:"TRANSITIVELY_CALLS",category:"control_flow",description:"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph.",properties:[{name:"hops",type:"integer",description:"Shortest call-chain hop distance from the calling function to the sink."}],pairs:[{fromKind:"ExternalService",toKind:"Function"},{fromKind:"Query",toKind:"Function"}]},{rel:"TRIGGERS",category:"control_flow",description:"Links an entry point or API endpoint to the handler function it triggers.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"Function"},{fromKind:"EntryPoint",toKind:"File"}]},{rel:"ACCESSED_BY",category:"data_flow",description:"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"ExternalService",toKind:"Function"},{fromKind:"ExternalService",toKind:"Type"},{fromKind:"Member",toKind:"Function"},{fromKind:"Procedure",toKind:"Function"},{fromKind:"Query",toKind:"Function"},{fromKind:"Query",toKind:"Type"},{fromKind:"Table",toKind:"Function"},{fromKind:"Table",toKind:"Query"},{fromKind:"Table",toKind:"Type"}]},{rel:"ATTRIBUTED_TO",category:"data_flow",description:"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain.",properties:[{name:"hops",type:"integer",description:"Call-chain hop distance from the entrypoint or endpoint to the sink."},{name:"sink_kind",type:"string",description:"Target sink classification (Database, Query, ExternalService, Topic)."}],pairs:[{fromKind:"Endpoint",toKind:"Database"},{fromKind:"Endpoint",toKind:"ExternalService"},{fromKind:"Endpoint",toKind:"Query"}]},{rel:"FLOWS_TO",category:"data_flow",description:"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the value transfer occurs."},{name:"arg_index",type:"integer",description:"0-indexed target parameter or argument slot."}],pairs:[{fromKind:"CallArgument",toKind:"Parameter"},{fromKind:"Parameter",toKind:"CallArgument"}]},{rel:"HAS_PARAMETER",category:"data_flow",description:"Links a function or method to one of its semantically colored parameters.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Parameter"}]},{rel:"PASSES_ARGUMENT",category:"data_flow",description:"Links a calling function or scope to a semantically colored call argument.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the value transfer occurs."},{name:"arg_index",type:"integer",description:"0-indexed target parameter or argument slot."}],pairs:[{fromKind:"Function",toKind:"CallArgument"}]},{rel:"PASSES_LITERAL",category:"data_flow",description:"Links a calling function or scope to a literal argument passed directly into an invocation.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the literal argument is passed."}],pairs:[{fromKind:"Endpoint",toKind:"CallLiteral"},{fromKind:"Function",toKind:"CallLiteral"},{fromKind:"Type",toKind:"CallLiteral"}]},{rel:"PUBLISHED_BY",category:"data_flow",description:"Links a topic to the function that publishes to it.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Topic",toKind:"Function"}]},{rel:"PUBLISHES_TO",category:"data_flow",description:"Links a service or function to a message queue or topic it publishes messages or events to.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Workload",toKind:"Topic"}]},{rel:"READS_FROM",category:"data_flow",description:"Links a query or workload to the table or cloud resource it reads from.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Query",toKind:"Table"}]},{rel:"SUBSCRIBED_BY",category:"data_flow",description:"Links a topic to the function that subscribes to it.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Topic",toKind:"Function"}]},{rel:"SUBSCRIBES_TO",category:"data_flow",description:"Links a service or handler function to a message queue or topic it listens to or consumes events from.",properties:[{name:"evidence_kind",type:"string",description:"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying call-site or endpoint URNs proving this link."},{name:"hops",type:"integer",description:"Transitive call-chain hop count from workload root."}],pairs:[{fromKind:"Endpoint",toKind:"Topic"},{fromKind:"Worker",toKind:"Topic"}]},{rel:"TARGETED_BY",category:"data_flow",description:"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it.",properties:[{name:"line",type:"integer",description:"1-indexed source line where the value transfer occurs."},{name:"arg_index",type:"integer",description:"0-indexed target parameter or argument slot."}],pairs:[{fromKind:"Database",toKind:"CallArgument"},{fromKind:"Endpoint",toKind:"CallArgument"},{fromKind:"ExternalService",toKind:"CallArgument"},{fromKind:"Procedure",toKind:"CallArgument"},{fromKind:"Table",toKind:"CallArgument"},{fromKind:"Topic",toKind:"CallArgument"}]},{rel:"USES_DB",category:"data_flow",description:"Links a project to a database catalog or instance it utilizes.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Workload",toKind:"Database"}]},{rel:"WRITES_TO",category:"data_flow",description:"Links a query or workload to the table or cloud resource it writes to.",properties:[{name:"operation",type:"string",description:"Normalized data access mode (read, write, read_write, schema)."},{name:"evidence_kind",type:"string",description:"Provenance source (query, orm, table, migration, config, library_call)."},{name:"evidence_ids",type:"string",description:"JSON array of underlying UAST/Query node URNs proving this link."},{name:"hops",type:"integer",description:"Call-chain distance when lifted transitively from helper libraries."}],pairs:[{fromKind:"Query",toKind:"Table"}]},{rel:"COORDINATES_WITH",category:"type_dependency",description:"Links a workload to the symmetric peer mesh it coordinates with.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Service",toKind:"PeerMesh"}]},{rel:"DEPENDS_ON",category:"type_dependency",description:"Represents a dependency relationship between projects, packages, or other entities.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"BoundedContext"},{fromKind:"Domain",toKind:"Domain"},{fromKind:"Module",toKind:"Library"},{fromKind:"Module",toKind:"Package"},{fromKind:"Project",toKind:"Package"},{fromKind:"Project",toKind:"Project"},{fromKind:"Query",toKind:"Table"}]},{rel:"HOSTED_IN",category:"type_dependency",description:"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source or config line where the cloud hosting binding is declared."}],pairs:[{fromKind:"Database",toKind:"Cloud"},{fromKind:"ExternalService",toKind:"Cloud"},{fromKind:"Topic",toKind:"Cloud"},{fromKind:"Workload",toKind:"Cloud"}]},{rel:"IMPLEMENTED_BY",category:"type_dependency",description:"Links a library package to the project that implements or encapsulates it.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Module",toKind:"Project"},{fromKind:"Package",toKind:"Project"}]},{rel:"INHERITS_FROM",category:"type_dependency",description:"Links a class/interface to its base class or inherited interface.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Type"}]},{rel:"OF_TYPE",category:"type_dependency",description:"Links a member variable or field to its declared type.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Member",toKind:"Type"}]},{rel:"POTENTIAL_TYPE",category:"type_dependency",description:"Links a variable or parameter to concrete classes that implement its declared interface type.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Type"}]},{rel:"PRODUCES",category:"type_dependency",description:"Indicates that a library project builds and produces a package.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Package",toKind:"Project"},{fromKind:"Project",toKind:"Package"}]},{rel:"TESTS",category:"type_dependency",description:"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"TestSuite",toKind:"Module"}]},{rel:"USES_GIT",category:"type_dependency",description:"Links the workspace to its repository configuration.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Project",toKind:"GitSettings"}]},{rel:"USES_TYPE",category:"type_dependency",description:"Links a calling function or type reference to the type it instantiates or references.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Function",toKind:"Type"},{fromKind:"Type",toKind:"Type"}]},{rel:"BELONGS_TO",category:"structural",description:"Links an entity to its parent project or container.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"EntryPoint",toKind:"Project"}]},{rel:"CONTAINS",category:"structural",description:"Represents directory structure containment or syntactic scoping of elements.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"Database"},{fromKind:"BoundedContext",toKind:"Module"},{fromKind:"BoundedContext",toKind:"Topic"},{fromKind:"Database",toKind:"DbSchema"},{fromKind:"DbSchema",toKind:"Procedure"},{fromKind:"DbSchema",toKind:"Table"},{fromKind:"Domain",toKind:"BoundedContext"},{fromKind:"Folder",toKind:"File"},{fromKind:"Folder",toKind:"Folder"},{fromKind:"Folder",toKind:"GitSettings"},{fromKind:"Procedure",toKind:"Query"},{fromKind:"Project",toKind:"EntryPoint"},{fromKind:"Project",toKind:"File"},{fromKind:"Project",toKind:"Project"}]},{rel:"DECLARED_IN",category:"structural",description:"Links a syntactic type, function, or member declaration to its physical declaring source file.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"CallArgument",toKind:"File"},{fromKind:"CallLiteral",toKind:"File"},{fromKind:"CatchClause",toKind:"File"},{fromKind:"Cloud",toKind:"File"},{fromKind:"Database",toKind:"File"},{fromKind:"DbSchema",toKind:"File"},{fromKind:"Endpoint",toKind:"File"},{fromKind:"Endpoint",toKind:"Project"},{fromKind:"EntryPoint",toKind:"File"},{fromKind:"ExternalService",toKind:"File"},{fromKind:"ExternalService",toKind:"Project"},{fromKind:"Function",toKind:"File"},{fromKind:"Member",toKind:"File"},{fromKind:"Parameter",toKind:"File"},{fromKind:"Query",toKind:"File"},{fromKind:"Topic",toKind:"File"},{fromKind:"Type",toKind:"File"}]},{rel:"DECLARES",category:"structural",description:"Indicates that a container entity declares a sub-entity.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Endpoint",toKind:"CatchClause"},{fromKind:"Function",toKind:"CatchClause"},{fromKind:"Function",toKind:"Function"},{fromKind:"Function",toKind:"Member"},{fromKind:"Function",toKind:"Type"},{fromKind:"Type",toKind:"Type"}]},{rel:"DEFINES",category:"structural",description:"Indicates that an entity defines a particular property or configuration.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"BoundedContext",toKind:"Type"}]},{rel:"HAS_MEMBER",category:"structural",description:"Links a type declaration to its declared member variables or fields.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Member"}]},{rel:"HAS_METHOD",category:"structural",description:"Links a type declaration to its declared methods or functions.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Type",toKind:"Function"}]},{rel:"LOCATED_IN",category:"structural",description:"Links a project to the physical folder or files structure where it is located.",properties:[{name:"line",type:"integer",description:"Optional 1-indexed source line where the relationship is declared."}],pairs:[{fromKind:"Project",toKind:"Folder"}]}]},D="graph",g="Endpoint",z=null,H=!1,ae=null,fe=null,ye="",ne={control_flow:!0,data_flow:!0,type_dependency:!0,structural:!0},ge={5:!0,4:!0,3:!0,2:!0,1:!0},Ce={},b=0;b<S.nodes.length;b++)S.nodes[b].subtypes&&S.nodes[b].subtypes.length>0&&(Ce[S.nodes[b].kind]=!0);var M="";function j(s){return getComputedStyle(document.documentElement).getPropertyValue(s).trim()}for(var T={canvasBg:j("--ont-canvas-bg"),gridDot:j("--ont-grid-dot"),logical:j("--ont-c-logical"),module:j("--ont-c-module"),workload:j("--ont-c-workload"),contract:j("--ont-c-contract"),data:j("--ont-c-data"),cloud:j("--ont-c-cloud"),syntax:j("--ont-c-syntax"),slicing:j("--ont-c-slicing"),project:j("--ont-c-project"),physical:j("--ont-c-physical"),capOuterFill:j("--ont-capsule-outer-fill"),capOuterBorder:j("--ont-capsule-outer-border"),capInnerFill:j("--ont-capsule-inner-fill"),capInnerBorder:j("--ont-capsule-inner-border"),capHoverBorder:j("--ont-capsule-hover-border"),capHeadBg:j("--ont-capsule-head-bg"),capHeadHoverBg:j("--ont-capsule-head-hover-bg"),abstractRing:j("--ont-abstract-ring"),badgeBg:j("--ont-badge-bg"),badgeBorder:j("--ont-badge-border"),pillBg:j("--ont-pill-bg"),pillBorder:j("--ont-pill-border"),pillBorderActive:j("--ont-pill-border-active"),edgeIdle:j("--ont-edge-idle"),edgeDim:j("--ont-edge-dim"),edgeControl:j("--ont-edge-control"),edgeData:j("--ont-edge-data"),edgeType:j("--ont-edge-type"),edgeStruct:j("--ont-edge-struct"),nodeRing:j("--ont-node-ring"),nodeHalo:j("--ont-node-halo"),nodeHover:j("--ont-node-hover"),inkBright:j("--ont-ink-bright"),inkSoft:j("--ont-ink-soft"),inkDim:j("--ont-ink-dim"),inkZero:j("--ont-ink-zero")},Le={Domain:{fill:T.logical,glyph:"globe",cluster:8},BoundedContext:{fill:T.logical,glyph:"layers",cluster:8},Module:{fill:T.module,glyph:"package",cluster:0},Workload:{fill:T.workload,glyph:"layers",cluster:0},App:{fill:T.workload,glyph:"browser",cluster:0},Service:{fill:T.workload,glyph:"server",cluster:0},Worker:{fill:T.workload,glyph:"gear",cluster:0},CliTool:{fill:T.workload,glyph:"terminal",cluster:0},Library:{fill:T.module,glyph:"library",cluster:0},TestSuite:{fill:T.module,glyph:"beaker",cluster:0},Endpoint:{fill:T.contract,glyph:"endpoint",cluster:1},Topic:{fill:T.contract,glyph:"topic",cluster:1},PeerMesh:{fill:T.contract,glyph:"mesh",cluster:1},Database:{fill:T.data,glyph:"database",cluster:2},DbSchema:{fill:T.data,glyph:"schema",cluster:2},Table:{fill:T.data,glyph:"table",cluster:2},Query:{fill:T.data,glyph:"query",cluster:2},Procedure:{fill:T.data,glyph:"procedure",cluster:2},ExternalService:{fill:T.cloud,glyph:"cloud",cluster:3},Cloud:{fill:T.cloud,glyph:"cloudUp",cluster:3},Type:{fill:T.syntax,glyph:"type",cluster:4},Function:{fill:T.syntax,glyph:"function",cluster:4},Member:{fill:T.syntax,glyph:"member",cluster:4},CatchClause:{fill:T.syntax,glyph:"catch",cluster:4},Parameter:{fill:T.slicing,glyph:"param",cluster:5},CallArgument:{fill:T.slicing,glyph:"arg",cluster:5},CallLiteral:{fill:T.slicing,glyph:"literal",cluster:5},Project:{fill:T.project,glyph:"project",cluster:6},EntryPoint:{fill:T.project,glyph:"zap",cluster:6},Package:{fill:T.project,glyph:"package",cluster:6},File:{fill:T.physical,glyph:"file",cluster:7},Folder:{fill:T.physical,glyph:"folder",cluster:7},GitSettings:{fill:T.physical,glyph:"git",cluster:7}},ke=[{x:255,y:235,rx:145,ry:105},{x:635,y:255,rx:145,ry:85},{x:1125,y:285,rx:135,ry:110},{x:925,y:155,rx:105,ry:60},{x:705,y:495,rx:165,ry:115},{x:1065,y:575,rx:125,ry:95},{x:305,y:605,rx:130,ry:90},{x:645,y:695,rx:155,ry:55},{x:595,y:115,rx:85,ry:24}],it={App:{x:115,y:145},CliTool:{x:115,y:265},Workload:{x:265,y:215},Service:{x:405,y:150},Worker:{x:405,y:270},Module:{x:295,y:385},Library:{x:135,y:385},TestSuite:{x:155,y:495}},yt={App:1,Worker:1,CliTool:1,Library:1,TestSuite:1},ft={control_flow:T.edgeControl,data_flow:T.edgeData,type_dependency:T.edgeType,structural:T.edgeStruct},L={},te=0;te<S.nodes.length;te++)L[S.nodes[te].kind]=S.nodes[te];for(var $={},Te=0;Te<S.relationships.length;Te++)$[S.relationships[Te].rel]=S.relationships[Te];function Je(s,u){var v=L[s];if(!v||!v.subtypes||v.subtypes.length===0)return[];for(var w=[],C=0;C<v.subtypes.length;C++){var c=v.subtypes[C];if(!u[c]){u[c]=!0,w.push(c);for(var N=Je(c,u),P=0;P<N.length;P++)w.push(N[P])}}return w}for(var Me=[],vt=0;vt<S.nodes.length;vt++){var It=S.nodes[vt];It.subtypes&&It.subtypes.length>0&&Me.push({kind:It.kind,depth:It.ancestors?It.ancestors.length:0,allDescendants:Je(It.kind,{})})}Me.sort(function(s,u){return s.depth-u.depth||(s.kind<u.kind?-1:1)});function ln(s){for(var u={},v=0;v<s.length;v++)u[s[v].kind]=!0;for(var w={},C={},c={},N=0;N<Me.length;N++){var P=Me[N];if(!Ce[P.kind]&&!(!u[P.kind]||w[P.kind])){for(var G=0,O=0;O<P.allDescendants.length;O++){var q=P.allDescendants[O];u[q]&&!w[q]&&(w[q]=!0,G++),C[q]||(C[q]=P.kind)}G>0&&(c[P.kind]=G)}}for(var F=[],K=0;K<s.length;K++)w[s[K].kind]||F.push(s[K]);return{visibleDtos:F,redirectMap:C,collapsedCounts:c}}function pe(s){return String(s||"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function tn(s,u){if(!u||s.kind.toLowerCase().indexOf(u)!==-1||s.pluralLabel.toLowerCase().indexOf(u)!==-1||s.purpose.toLowerCase().indexOf(u)!==-1||s.idScheme.toLowerCase().indexOf(u)!==-1||s.extends&&s.extends.toLowerCase().indexOf(u)!==-1)return!0;if(s.subtypes){for(var v=0;v<s.subtypes.length;v++)if(s.subtypes[v].toLowerCase().indexOf(u)!==-1)return!0}for(var w=0;w<s.properties.length;w++){var C=s.properties[w];if(C.name.toLowerCase().indexOf(u)!==-1||C.description.toLowerCase().indexOf(u)!==-1)return!0}for(var c=s.inheritedOutEdges?s.outEdges.concat(s.inheritedOutEdges):s.outEdges,N=0;N<c.length;N++){var P=c[N];if(P.rel.toLowerCase().indexOf(u)!==-1||P.toKind.toLowerCase().indexOf(u)!==-1)return!0}for(var G=s.inheritedInEdges?s.inEdges.concat(s.inheritedInEdges):s.inEdges,O=0;O<G.length;O++){var q=G[O];if(q.rel.toLowerCase().indexOf(u)!==-1||q.fromKind.toLowerCase().indexOf(u)!==-1)return!0}return!1}var ut=690,tt=440,Qe=.0228,B=.001,oe=.6,x=[],m={},k=[],Z=1,ue=1,ee=0,Se=0,ve=!1,_e=null,He="";try{var Nt=localStorage.getItem("ce_ontology_air");if(Nt!==null){var Hn=parseFloat(Nt);!isNaN(Hn)&&Hn>=.1&&Hn<=2.4&&(Z=Hn)}var Pa=localStorage.getItem("ce_ontology_hub");if(Pa!==null){var Gn=parseFloat(Pa);!isNaN(Gn)&&Gn>=0&&Gn<=5&&(ue=Gn)}}catch{}function Ra(s){for(var u={},v=0;v<s.length;v++){var w=s[v],C=Le[w.kind]||{fill:T.physical,glyph:"file",cluster:7};u[C.cluster]||(u[C.cluster]=[]),u[C.cluster].push(w)}var c={};for(var N in u)for(var P=u[N],G=ke[N]||ke[0],O=0;O<P.length;O++){var q=P[O],F=Le[q.kind]||{fill:T.physical,glyph:"file",cluster:7},K=Number(N)===0&&P.length>1?it[q.kind]:null,re=P.length===1?0:O/P.length*Math.PI*2-Math.PI/2,Ie=K?K.x:G.x+(P.length===1?0:Math.cos(re)*G.rx),de=K?K.y:G.y+(P.length===1?0:Math.sin(re)*G.ry);c[q.kind]={ax:Ie,ay:de,style:F,anchorStrength:K?.072:.048}}return c}function fo(s,u){var v=[],w={},C={};function c(Oe,Be,Ge){if(Oe!==Be){var Ye=s[Oe],Ze=s[Be];if(!(!Ye||!Ze)){var me=Oe<Be?Oe:Be,Ke=Oe<Be?Be:Oe,mt=me+"::"+Ke;w[mt]||(w[mt]=!0,v.push({a:Ye,b:Ze,isExt:Ge}),C[Oe]=(C[Oe]||0)+1,C[Be]=(C[Be]||0)+1)}}}for(var N=0;N<S.nodes.length;N++){var P=S.nodes[N];P.extends&&c(P.kind,P.extends,!0)}for(var G=0;G<S.relationships.length;G++){var O=S.relationships[G];if(ne[O.category])for(var q=0;q<O.pairs.length;q++){var F=O.pairs[q],K=u&&u[F.fromKind]||F.fromKind,re=u&&u[F.toKind]||F.toKind;c(K,re,!1)}}for(var Ie in s)s[Ie].degree=Math.max(1,C[Ie]||1);for(var de=[],Ne=0;Ne<v.length;Ne++){var be=v[Ne],Ae=be.a.degree||1,Re=be.b.degree||1,se=Math.max(.22,Math.min(.78,Ae/(Ae+Re))),U=Math.max(1,Math.min(Ae,Re)),J=be.isExt?.54:.48/Math.sqrt(U),De=Math.max(.16,Math.min(.56,J)),ze=be.a.style.cluster===be.b.style.cluster,qe=be.isExt?126:ze?144:212;de.push({source:be.a,target:be.b,distance:qe,strength:De,bias:se})}return de}function Jr(s,u,v,w,C){for(var c=typeof C=="number"?C:1,N=Math.pow(540*w*(1+c*.25),2),P=-620*w*v,G=0;G<s.length;G++)for(var O=s[G],q=O.degree||1,F=1+c*.24*Math.max(0,q-2),K=G+1;K<s.length;K++){var re=s[K],Ie=re.degree||1,de=re.x-O.x,Ne=re.y-O.y,be=de*de+Ne*Ne;if(!(be>N)){be<1&&(de=G%2===0?1:-1,Ne=K%2===0?.5:-.5,be=1.25);var Ae=1+c*.24*Math.max(0,Ie-2),Re=.5*(F+Ae)*(1+c*.08*Math.min(q,Ie)),se=P*Re/Math.max(be,900);O.fx===null&&(O.vx+=de*se,O.vy+=Ne*se),re.fx===null&&(re.vx-=de*se,re.vy-=Ne*se)}}for(var U=0;U<u.length;U++){var J=u[U],De=J.target.x+J.target.vx-J.source.x-J.source.vx,ze=J.target.y+J.target.vy-J.source.y-J.source.vy,qe=Math.sqrt(De*De+ze*ze)||1,Oe=Math.max(0,(J.source.degree||1)+(J.target.degree||1)-4),Be=1+c*.042*Oe,Ge=1/(1+c*.055*Oe),Ye=J.distance*w*Be,Ze=(qe-Ye)/qe*v*J.strength*Ge;De*=Ze,ze*=Ze,J.target.fx===null&&(J.target.vx-=De*J.bias,J.target.vy-=ze*J.bias),J.source.fx===null&&(J.source.vx+=De*(1-J.bias),J.source.vy+=ze*(1-J.bias))}for(var me=0;me<s.length;me++){var Ke=s[me];if(Ke.fx===null){var mt=ut+(Ke.ax-ut)*w,Pt=tt+(Ke.ay-tt)*w,rn=Ke.anchorStrength/(1+c*.35+c*.06*Math.max(0,(Ke.degree||1)-2));Ke.vx+=(mt-Ke.x)*rn*v,Ke.vy+=(Pt-Ke.y)*rn*v}}for(var Rt=26*Math.min(w,2.2),ct=0;ct<2;ct++)for(var ot=0;ot<s.length;ot++)for(var je=s[ot],xt=Rt*(1+c*.035*Math.max(0,(je.degree||1)-2)),wt=ot+1;wt<s.length;wt++){var We=s[wt],vn=Rt*(1+c*.035*Math.max(0,(We.degree||1)-2)),_t=xt+vn,dt=We.x+We.vx-(je.x+je.vx),Yt=We.y+We.vy-(je.y+je.vy),Gt=dt*dt+Yt*Yt;if(!(Gt>=_t*_t)){Gt<.01&&(dt=.5,Yt=.5,Gt=.5);var Mt=Math.sqrt(Gt),bn=(_t-Mt)/Mt*.42,Wt=dt*bn,Ut=Yt*bn;je.fx===null&&We.fx===null?(je.vx-=Wt,je.vy-=Ut,We.vx+=Wt,We.vy+=Ut):je.fx!==null&&We.fx===null?(We.vx+=Wt*2,We.vy+=Ut*2):We.fx!==null&&je.fx===null&&(je.vx-=Wt*2,je.vy-=Ut*2)}}for(var Wn=0;Wn<s.length;Wn++){var Ue=s[Wn];if(Ue.fx!==null&&Ue.fy!==null){Ue.x=Ue.fx,Ue.y=Ue.fy,Ue.vx=0,Ue.vy=0;continue}Ue.vx*=oe,Ue.vy*=oe;var Qt=Math.sqrt(Ue.vx*Ue.vx+Ue.vy*Ue.vy);Qt>28&&(Ue.vx=Ue.vx/Qt*28,Ue.vy=Ue.vy/Qt*28),Ue.x+=Ue.vx,Ue.y+=Ue.vy}}function _r(s){Tt=[],cn={};for(var u=0;u<x.length;u++){var v=x[u],w=!1;if(v.dto.subtypes){for(var C=0;C<v.dto.subtypes.length;C++)if(m[v.dto.subtypes[C]]){w=!0;break}}var c={kind:v.dto.kind,x:v.x,y:v.y,ax:v.ax,ay:v.ay,radius:25,fill:v.style.fill,glyph:v.style.glyph,isAbstract:!!v.dto.isAbstract,extendsKind:v.dto.extends||null,collapsedCount:s&&s[v.dto.kind]?s[v.dto.kind]:0,hasExpandedSubtypes:w};Tt.push(c),cn[c.kind]=c}}var Ar={};function xa(s,u,v){Ar=v||{};for(var w=x.length===0,C=m,c=Ra(s),N=[],P={},G=[],O=0;O<s.length;O++){var q=s[O];G.push(q.kind);var F=c[q.kind],K=C[q.kind];if(K){K.dto=q,K.style=F.style,K.ax=F.ax,K.ay=F.ay,K.anchorStrength=F.anchorStrength,N.push(K),P[q.kind]=K;continue}var re=ut+(F.ax-ut)*Z,Ie=tt+(F.ay-tt)*Z,de=re,Ne=Ie;if(!w&&q.extends){var be=C[q.extends]||P[q.extends];be&&(de=be.x+(re-be.x)*.32,Ne=be.y+(Ie-be.y)*.32)}var Ae={dto:q,style:F.style,x:de,y:Ne,ax:F.ax,ay:F.ay,vx:0,vy:0,fx:null,fy:null,anchorStrength:F.anchorStrength};N.push(Ae),P[q.kind]=Ae}x=N,m=P,k=fo(P,u);var Re=[];for(var se in ne)ne[se]&&Re.push(se);var U=G.join(",")+"|"+Re.join(",");if(w){for(var J=1,De=0;De<220;De++)J+=(0-J)*Qe,Jr(x,k,J,Z,ue);for(var ze=0;ze<x.length;ze++)x[ze].vx=0,x[ze].vy=0;ee=0}else U!==He&&!ve&&(ee=Math.max(ee,.42));He=U,_r(Ar)}function Lr(){for(var s=0;s<x.length;s++){var u=x[s];u.x=ut+(u.ax-ut)*Z,u.y=tt+(u.ay-tt)*Z,u.vx=0,u.vy=0,u.fx=null,u.fy=null}for(var v=1,w=0;w<220;w++)v+=(0-v)*Qe,Jr(x,k,v,Z,ue);for(var C=0;C<x.length;C++)x[C].vx=0,x[C].vy=0;ee=0,Se=0,_r(Ar)}function rr(s,u){for(var v={},w=[],C=0;C<S.nodes.length;C++){var c=S.nodes[C];if(c.extends&&!(!s[c.kind]||!s[c.extends])){var N=c.kind+"|EXTENDS|"+c.extends,P={fromKind:c.kind,toKind:c.extends,rel:"EXTENDS",category:"type_dependency",isExtends:!0,priority:1};v[N]=P,w.push(P)}}for(var G=0;G<S.relationships.length;G++){var O=S.relationships[G];if(ne[O.category])for(var q=0;q<O.pairs.length;q++){var F=O.pairs[q],K=u&&u[F.fromKind]||F.fromKind,re=u&&u[F.toKind]||F.toKind;if(!(!s[K]||!s[re])&&!(K===re&&F.fromKind!==F.toKind)){var Ie=1;O.rel==="DECLARED_IN"||O.rel==="BELONGS_TO"?Ie=4:yt[K]||yt[re]?Ie=3:(K==="Service"||re==="Service"||K==="Workload"||re==="Workload"||O.category==="structural")&&(Ie=2);var de=K+"|"+O.rel+"|"+re;if(v[de])Ie<v[de].priority&&(v[de].priority=Ie);else{var Ne={fromKind:K,toKind:re,rel:O.rel,category:O.category,priority:Ie};v[de]=Ne,w.push(Ne)}}}}for(var be={},Ae={},Re=[],se=0;se<w.length;se++){var U=w[se];if(U.fromKind===U.toKind){var J=be[U.fromKind]||0;be[U.fromKind]=J+1,Re.push({fromKind:U.fromKind,toKind:U.toKind,rel:U.rel,category:U.category,isSelf:!0,slot:J,offset:0,priority:U.priority})}else{var De=U.fromKind<U.toKind?U.fromKind:U.toKind,ze=U.fromKind<U.toKind?U.toKind:U.fromKind,qe=De+"::"+ze;Ae[qe]||(Ae[qe]=[]),Ae[qe].push(U)}}for(var Oe in Ae)for(var Be=Ae[Oe],Ge=0;Ge<Be.length;Ge++){var Ye=Be[Ge],Ze=22;Be.length>1&&(Ze=(Ge-(Be.length-1)/2)*34,Math.abs(Ze)<6&&(Ze=14)),Re.push({fromKind:Ye.fromKind,toKind:Ye.toKind,rel:Ye.rel,category:Ye.category,isExtends:!!Ye.isExtends,isSelf:!1,slot:0,offset:Ze,priority:Ye.priority})}return Re.sort(function(me,Ke){return me.priority-Ke.priority}),Re}var Tt=[],cn={},fn=[],ce={panX:0,panY:0,zoom:.72},Zr=!1,nn=!0,Ir=0,Ma=0,Fe=null;function jt(s,u,v,w,C,c){var N=Math.min(c,w/2,C/2);s.beginPath(),s.moveTo(u+N,v),s.arcTo(u+w,v,u+w,v+C,N),s.arcTo(u+w,v+C,u,v+C,N),s.arcTo(u,v+C,u,v,N),s.arcTo(u,v,u+w,v,N),s.closePath()}function vs(s,u,v,w){s.save(),s.translate(v,w),s.strokeStyle=T.inkBright,s.fillStyle=T.inkBright,s.lineWidth=1.65,s.lineCap="round",s.lineJoin="round",u==="globe"?(s.beginPath(),s.arc(0,0,6.8,0,Math.PI*2),s.stroke(),s.beginPath(),s.ellipse(0,0,3.1,6.8,0,0,Math.PI*2),s.stroke(),s.beginPath(),s.moveTo(-6.6,0),s.lineTo(6.6,0),s.stroke()):u==="database"||u==="procedure"?(s.beginPath(),s.ellipse(0,-5,6.5,2.6,0,0,Math.PI*2),s.stroke(),s.beginPath(),s.moveTo(-6.5,-5),s.lineTo(-6.5,5),s.arc(0,5,6.5,Math.PI,0,!0),s.lineTo(6.5,-5),s.stroke()):u==="schema"?(jt(s,-6.5,-6,13,4.5,1.5),s.stroke(),jt(s,-6.5,1.5,5.2,4.5,1.2),s.stroke(),jt(s,1.3,1.5,5.2,4.5,1.2),s.stroke(),s.beginPath(),s.moveTo(0,-1.5),s.lineTo(0,0),s.moveTo(-3.9,0),s.lineTo(3.9,0),s.moveTo(-3.9,0),s.lineTo(-3.9,1.5),s.moveTo(3.9,0),s.lineTo(3.9,1.5),s.stroke()):u==="layers"?(s.beginPath(),s.moveTo(0,-6.5),s.lineTo(7,-3),s.lineTo(0,.5),s.lineTo(-7,-3),s.closePath(),s.stroke(),s.beginPath(),s.moveTo(-7,.8),s.lineTo(0,4.2),s.lineTo(7,.8),s.moveTo(-7,4.2),s.lineTo(0,7.2),s.lineTo(7,4.2),s.stroke()):u==="table"?(jt(s,-7,-6,14,12,2),s.stroke(),s.beginPath(),s.moveTo(-7,-1.5),s.lineTo(7,-1.5),s.moveTo(-1.5,-1.5),s.lineTo(-1.5,6),s.stroke()):u==="query"?(s.beginPath(),s.arc(-1.5,-1.5,5,0,Math.PI*2),s.moveTo(2.2,2.2),s.lineTo(6.8,6.8),s.stroke()):u==="endpoint"?(s.beginPath(),s.arc(0,0,2.2,0,Math.PI*2),s.fill(),s.beginPath(),s.arc(0,0,5.5,-Math.PI*.7,Math.PI*.7),s.stroke(),s.beginPath(),s.arc(0,0,5.5,Math.PI*.3,Math.PI*1.7),s.stroke()):u==="topic"?(jt(s,-7,-5,14,10,2),s.stroke(),s.beginPath(),s.moveTo(-6.5,-4.2),s.lineTo(0,1),s.lineTo(6.5,-4.2),s.stroke()):u==="cloud"||u==="cloudUp"?(s.beginPath(),s.arc(-2.5,1,3.8,Math.PI*.5,Math.PI*1.5),s.arc(1,-1.5,4.2,Math.PI,Math.PI*1.95),s.arc(4.2,1.5,3.2,Math.PI*1.5,Math.PI*.5),s.closePath(),s.stroke()):u==="function"?(s.beginPath(),s.moveTo(2.5,-6.5),s.quadraticCurveTo(-1,-6.5,-1,-2),s.lineTo(-1,6.5),s.moveTo(-4.5,-1),s.lineTo(3,-1),s.stroke()):u==="type"?(s.beginPath(),s.moveTo(0,-7),s.lineTo(6.5,-3.5),s.lineTo(6.5,3.5),s.lineTo(0,7),s.lineTo(-6.5,3.5),s.lineTo(-6.5,-3.5),s.closePath(),s.stroke()):u==="param"||u==="arg"||u==="literal"?(s.beginPath(),s.moveTo(-4,-5.5),s.lineTo(-7,0),s.lineTo(-4,5.5),s.moveTo(4,-5.5),s.lineTo(7,0),s.lineTo(4,5.5),u!=="param"&&(s.moveTo(-1.5,0),s.lineTo(1.5,0)),s.stroke()):u==="folder"?(s.beginPath(),s.moveTo(-7,-5),s.lineTo(-2,-5),s.lineTo(0,-3),s.lineTo(7,-3),s.lineTo(7,5.5),s.lineTo(-7,5.5),s.closePath(),s.stroke()):u==="file"?(s.beginPath(),s.moveTo(-5,-6.5),s.lineTo(2,-6.5),s.lineTo(5.5,-3),s.lineTo(5.5,6.5),s.lineTo(-5,6.5),s.closePath(),s.stroke()):u==="package"||u==="project"?(jt(s,-6.5,-5.5,13,11,2),s.stroke(),s.beginPath(),s.moveTo(-6.5,-1.5),s.lineTo(6.5,-1.5),s.stroke()):u==="zap"?(s.beginPath(),s.moveTo(1,-7),s.lineTo(-5,.5),s.lineTo(0,.5),s.lineTo(-1,7),s.lineTo(5,-.5),s.lineTo(0,-.5),s.closePath(),s.stroke()):(jt(s,-6.5,-5.5,13,11,2.5),s.stroke(),s.beginPath(),s.moveTo(-3.5,0),s.lineTo(3.5,0),s.stroke()),s.restore()}function Da(s,u,v,w,C,c){s.save(),s.translate(u,v),s.rotate(w),s.fillStyle=C,s.beginPath(),s.moveTo(0,0),s.lineTo(-c,-c*.54),s.lineTo(-c*.78,0),s.lineTo(-c,c*.54),s.closePath(),s.fill(),s.restore()}function vo(s,u,v,w,C,c){s.save(),s.translate(u,v),s.rotate(w),s.fillStyle=T.canvasBg,s.strokeStyle=C,s.lineWidth=1.5,s.lineJoin="round",s.beginPath(),s.moveTo(0,0),s.lineTo(-c,-c*.58),s.lineTo(-c,c*.58),s.closePath(),s.fill(),s.stroke(),s.restore()}function In(){var s=document.getElementById("ont-canvas-frame");if(!(!s||Tt.length===0)){var u=s.clientWidth||760,v=s.clientHeight||640,w=document.getElementById("ont-filter-overlay"),C=0;w&&getComputedStyle(w).position==="absolute"&&(C=(w.offsetHeight||30)+14);for(var c=Math.max(160,v-C),N=1e9,P=1e9,G=-1e9,O=-1e9,q=0;q<Tt.length;q++){var F=Tt[q],K=F.x,re=F.y;if(ee>.05&&typeof F.ax=="number"){var Ie=ut+(F.ax-ut)*Z,de=tt+(F.ay-tt)*Z;Ie-68<N&&(N=Ie-68),de-74<P&&(P=de-74),Ie+68>G&&(G=Ie+68),de+88>O&&(O=de+88)}K-68<N&&(N=K-68),re-74<P&&(P=re-74),K+68>G&&(G=K+68),re+108>O&&(O=re+108)}var Ne=Math.max(200,G-N),be=Math.max(200,O-P),Ae=Math.max(.22,Math.min(1.85,Math.min((u-40)/Ne,(c-36)/be)));ce.zoom=Ae,ce.panX=u/2-(N+G)/2*Ae,ce.panY=C+c/2-(P+O)/2*Ae}}function ea(s,u){for(var v=(s-ce.panX)/ce.zoom,w=(u-ce.panY)/ce.zoom,C=Tt.length-1;C>=0;C--){var c=Tt[C],N=!!(c.collapsedCount&&c.collapsedCount>0||c.hasExpandedSubtypes);if(N){var P=c.x-c.radius*.78-14,G=c.y-c.radius*.74-9;if(v>=P-4&&v<=P+34&&w>=G-4&&w<=G+22)return c.kind}}return null}function ta(s,u){for(var v=(s-ce.panX)/ce.zoom,w=(u-ce.panY)/ce.zoom,C=Tt.length-1;C>=0;C--){var c=Tt[C],N=v-c.x,P=w-c.y;if(N*N+P*P<=1024)return c}return null}function bt(){if(D==="graph"){var s=document.getElementById("ont-graph-canvas"),u=document.getElementById("ont-canvas-frame");if(!(!s||!u)){var v=u.clientWidth||760,w=u.clientHeight||640;!Zr&&v>80&&Tt.length>0&&(Zr=!0,In());var C=window.devicePixelRatio||1;s.width=Math.max(1,Math.floor(v*C)),s.height=Math.max(1,Math.floor(w*C));var c=s.getContext("2d");if(c){c.setTransform(C,0,0,C,0,0),c.fillStyle=T.canvasBg,c.fillRect(0,0,v,w),c.fillStyle=T.gridDot;for(var N=32,P=(ce.panX%N+N)%N,G=(ce.panY%N+N)%N,O=P;O<v;O+=N)for(var q=G;q<w;q+=N)c.fillRect(O,q,1.4,1.4);var F=z,K={};if(F){K[F]=!0;for(var re=cn[F],Ie={};re&&re.extendsKind&&!Ie[re.extendsKind];)Ie[re.extendsKind]=!0,K[re.extendsKind]=!0,re=cn[re.extendsKind]}var de={};for(var Ne in K)de[Ne]=!0;if(F)for(var be=0;be<fn.length;be++)K[fn[be].fromKind]&&(de[fn[be].toKind]=!0),K[fn[be].toKind]&&(de[fn[be].fromKind]=!0);c.save(),c.translate(ce.panX,ce.panY),c.scale(ce.zoom,ce.zoom);for(var Ae=[],Re=0;Re<fn.length;Re++){var se=fn[Re],U=cn[se.fromKind],J=cn[se.toKind];if(!(!U||!J)){var De=!!(F&&(K[se.fromKind]||K[se.toKind])),ze=!!(F&&!De),qe=ze?T.edgeDim:se.isExtends?De?T.capHoverBorder:T.abstractRing:De?ft[se.category]||T.edgeControl:T.edgeIdle;if(c.strokeStyle=qe,c.lineWidth=De?1.9:se.isExtends?1.5:1.25,se.isSelf){var Oe=-Math.PI*.64-se.slot*.55,Be=Oe-.42,Ge=Oe+.42,Ye=U.radius,Ze=U.x+Math.cos(Be)*Ye,me=U.y+Math.sin(Be)*Ye,Ke=U.x+Math.cos(Ge)*(Ye+3),mt=U.y+Math.sin(Ge)*(Ye+3),Pt=Ye+42+se.slot*16,rn=U.x+Math.cos(Be-.18)*Pt,Rt=U.y+Math.sin(Be-.18)*Pt,ct=U.x+Math.cos(Ge+.18)*Pt,ot=U.y+Math.sin(Ge+.18)*Pt;c.beginPath(),c.moveTo(Ze,me),c.bezierCurveTo(rn,Rt,ct,ot,Ke,mt),c.stroke(),ze||(Da(c,Ke,mt,Math.atan2(mt-ot,Ke-ct),qe,7),Ae.push({mx:.125*Ze+.375*rn+.375*ct+.125*Ke,my:.125*me+.375*Rt+.375*ot+.125*mt,text:se.rel,active:De,prio:De?0:se.priority}));continue}var je=se.fromKind<se.toKind,xt=je?U.x:J.x,wt=je?U.y:J.y,We=je?J.x:U.x,vn=je?J.y:U.y,_t=We-xt,dt=vn-wt,Yt=Math.sqrt(_t*_t+dt*dt)||1,Gt=(U.x+J.x)/2+-dt/Yt*se.offset,Mt=(U.y+J.y)/2+_t/Yt*se.offset,bn=Gt-U.x,Wt=Mt-U.y,Ut=Math.sqrt(bn*bn+Wt*Wt)||1,Wn=U.x+bn/Ut*U.radius,Ue=U.y+Wt/Ut*U.radius,Qt=J.x-Gt,Sn=J.y-Mt,Un=Math.sqrt(Qt*Qt+Sn*Sn)||1,Qn=J.x-Qt/Un*(J.radius+(se.isExtends?4:3)),oa=J.y-Sn/Un*(J.radius+(se.isExtends?4:3));c.save(),se.isExtends&&c.setLineDash([5,4]),c.beginPath(),c.moveTo(Wn,Ue),c.quadraticCurveTo(Gt,Mt,Qn,oa),c.stroke(),c.restore(),ze||(se.isExtends?vo(c,Qn,oa,Math.atan2(Sn,Qt),qe,De?9.5:8.4):Da(c,Qn,oa,Math.atan2(Sn,Qt),qe,De?7.8:6.6),Ae.push({mx:.25*Wn+.5*Gt+.25*Qn,my:.25*Ue+.5*Mt+.25*oa,text:se.rel,active:De,isExtends:se.isExtends,prio:De?0:se.priority}))}}for(var sa=[],Or=0;Or<Tt.length;Or++){var we=Tt[Or],lr=we.kind===z,wo=we.kind===ae,wn=!!(F&&!de[we.kind]),ia=we.x*ce.zoom+ce.panX,Br=we.y*ce.zoom+ce.panY,Fr=(we.radius+14)*ce.zoom;if(sa.push({minX:ia-Fr,minY:Br-Fr,maxX:ia+Fr,maxY:Br+Fr+18*ce.zoom}),c.save(),wn&&(c.globalAlpha=.34),(lr||wo)&&(c.beginPath(),c.arc(we.x,we.y,we.radius+6.5,0,Math.PI*2),c.fillStyle=lr?T.nodeHalo:T.nodeHover,c.fill()),we.isAbstract&&(c.save(),c.beginPath(),c.arc(we.x,we.y,we.radius+4,0,Math.PI*2),c.setLineDash([3.5,3]),c.lineWidth=1.3,c.strokeStyle=T.abstractRing,c.stroke(),c.restore()),c.beginPath(),c.arc(we.x,we.y,we.radius,0,Math.PI*2),c.fillStyle=we.fill,c.fill(),c.lineWidth=lr?2.4:1.7,c.strokeStyle=lr?T.edgeControl:T.nodeRing,c.stroke(),vs(c,we.glyph,we.x,we.y),we.collapsedCount&&we.collapsedCount>0||we.hasExpandedSubtypes){var Kr=we.collapsedCount&&we.collapsedCount>0?"+"+we.collapsedCount:"−";c.font='400 10px "Atkinson Hyperlegible Mono", ui-monospace, monospace';var cr=Math.max(22,c.measureText(Kr).width+10),la=17,Dt=we.x-we.radius*.78-cr/2,zn=we.y-we.radius*.74-la/2,ca=fe===we.kind;jt(c,Dt,zn,cr,la,8.5),c.fillStyle=ca?T.capHeadHoverBg:T.capHeadBg,c.fill(),c.lineWidth=1.2,c.strokeStyle=ca?T.capHoverBorder:T.capInnerBorder,c.stroke(),c.fillStyle=T.inkBright,c.textAlign="center",c.textBaseline="middle",c.fillText(Kr,Dt+cr/2,zn+la/2+.5)}c.font='400 12px "Host Grotesk", system-ui, sans-serif',c.fillStyle=wn?T.inkDim:T.inkBright,c.textAlign="center",c.textBaseline="top",c.fillText(we.kind,we.x,we.y+we.radius+7),c.restore()}if(c.restore(),ce.zoom>=.42){Ae.sort(function(Ga,bs){return Ga.prio-bs.prio}),c.font='400 9.5px "Atkinson Hyperlegible Mono", ui-monospace, monospace',c.textAlign="center",c.textBaseline="middle";for(var qn=0;qn<Ae.length;qn++){var Cn=Ae[qn],xn=Cn.mx*ce.zoom+ce.panX,Mn=Cn.my*ce.zoom+ce.panY;if(!(xn<-40||xn>v+40||Mn<-20||Mn>w+20)){var Dn=c.measureText(Cn.text).width+11,Yn=16,dr={minX:xn-Dn/2-3,minY:Mn-Yn/2-2,maxX:xn+Dn/2+3,maxY:Mn+Yn/2+2},Co=!1;if(!Cn.active)for(var Ha=0;Ha<sa.length;Ha++){var da=sa[Ha];if(!(dr.maxX<da.minX||dr.minX>da.maxX||dr.maxY<da.minY||dr.minY>da.maxY)){Co=!0;break}}Co||(sa.push(dr),jt(c,xn-Dn/2,Mn-Yn/2,Dn,Yn,5),c.fillStyle=T.pillBg,c.fill(),c.lineWidth=1,c.strokeStyle=Cn.active?T.pillBorderActive:Cn.isExtends?T.capInnerBorder:T.pillBorder,c.stroke(),c.fillStyle=Cn.active?T.inkBright:Cn.isExtends?T.capHoverBorder:T.inkSoft,c.fillText(Cn.text,xn,Mn+.5))}}}}}}}function Oa(s){var u={},v=[],w={},C=L[s];if(!C)return{badges:u,conns:v};for(var c=C.inheritedOutEdges?C.outEdges.concat(C.inheritedOutEdges):C.outEdges,N=0;N<c.length;N++){var P=c[N];if(ne[P.category]){var G="out|"+P.rel+"|"+P.toKind;w[G]||(w[G]=!0,u[P.toKind]||(u[P.toKind]=[]),u[P.toKind].push({dir:"out",rel:P.rel,cat:P.category}),v.push({peer:P.toKind,rel:P.rel,dir:"out",cat:P.category}))}}for(var O=C.inheritedInEdges?C.inEdges.concat(C.inheritedInEdges):C.inEdges,q=0;q<O.length;q++){var F=O[q];if(ne[F.category]){var K="in|"+F.rel+"|"+F.fromKind;w[K]||(w[K]=!0,u[F.fromKind]||(u[F.fromKind]=[]),u[F.fromKind].push({dir:"in",rel:F.rel,cat:F.category}),v.push({peer:F.fromKind,rel:F.rel,dir:"in",cat:F.category}))}}return{badges:u,conns:v}}function Ba(s,u){if(s){var v=document.getElementById("insp-layer-pill");v.textContent="Layer "+s.layerId,v.className="ont-layer-pill ont-layer-pill--l"+s.layerId;var w=document.getElementById("insp-abstract-pill");w&&(w.hidden=!s.isAbstract);var C=document.getElementById("insp-lineage");if(C){var c="";if(s.ancestors&&s.ancestors.length>0){c+='<div class="ont-lineage-row"><span class="ont-layer-sub">Extends</span>';for(var N=0;N<s.ancestors.length;N++){var P=s.ancestors[N];c+='<button type="button" class="ont-jump-btn" data-select-kind="'+pe(P)+'">:'+pe(P)+"</button>"}c+="</div>"}if(s.subtypes&&s.subtypes.length>0){c+='<div class="ont-lineage-row"><span class="ont-layer-sub">Subtypes ('+s.subtypes.length+")</span>";for(var G=0;G<s.subtypes.length;G++){var O=s.subtypes[G];c+='<button type="button" class="ont-jump-btn" data-select-kind="'+pe(O)+'">:'+pe(O)+"</button>"}c+="</div>"}C.innerHTML=c,C.hidden=!c}document.getElementById("insp-plural").textContent=s.pluralLabel,document.getElementById("insp-title").textContent=":"+s.kind,document.getElementById("insp-purpose").textContent=s.purpose,document.getElementById("insp-urn").textContent=s.idScheme||"n/a",document.getElementById("insp-props-heading").textContent="Collected properties ("+s.properties.length+" + "+S.commonProperties.length+" base)";for(var q="",F=0;F<s.properties.length;F++){var K=s.properties[F],re=u&&(K.name.toLowerCase().indexOf(u)!==-1||K.description.toLowerCase().indexOf(u)!==-1),Ie=K.inheritedFrom?'<code class="ont-edge-prop">via :'+pe(K.inheritedFrom)+"</code>":"";q+='<div class="ont-prop-item'+(re?" is-match":"")+'"><div class="ont-prop-top"><code class="ont-prop-name">'+pe(K.name)+'</code><span class="ont-prop-right">'+Ie+'<code class="ont-prop-type">'+pe(K.type)+'</code></span></div><span class="ont-prop-desc">'+pe(K.description)+"</span></div>"}document.getElementById("insp-props-list").innerHTML=q;var de=s.inheritedOutEdges?s.outEdges.concat(s.inheritedOutEdges):s.outEdges,Ne=s.inheritedInEdges?s.inEdges.concat(s.inheritedInEdges):s.inEdges;document.getElementById("insp-rels-heading").textContent="Typed relationships ("+de.length+" out, "+Ne.length+" in)";for(var be="",Ae=0;Ae<de.length;Ae++){var Re=de[Ae],se=$[Re.rel],U="";if(se&&se.properties&&se.properties.length>1){U='<div class="ont-edge-props-row">';for(var J=0;J<se.properties.length;J++){var De=se.properties[J];U+='<code class="ont-edge-prop" title="'+pe(De.description)+'">r.'+pe(De.name)+"</code>"}U+="</div>"}var ze=Re.inheritedFrom?'<code class="ont-edge-prop">via :'+pe(Re.inheritedFrom)+"</code>":"";be+='<div class="ont-rel-row"><div class="ont-rel-line"><code class="ont-edge-tag ont-edge-tag--'+pe(Re.category)+'">-[:'+pe(Re.rel)+']-&gt;</code><button type="button" class="ont-jump-btn" data-select-kind="'+pe(Re.toKind)+'">:'+pe(Re.toKind)+"</button>"+ze+'</div><span class="ont-prop-desc">'+pe(Re.description)+"</span>"+U+"</div>"}for(var qe=0;qe<Ne.length;qe++){var Oe=Ne[qe],Be=Oe.inheritedFrom?'<code class="ont-edge-prop">via :'+pe(Oe.inheritedFrom)+"</code>":"";be+='<div class="ont-rel-row"><div class="ont-rel-line"><button type="button" class="ont-jump-btn" data-select-kind="'+pe(Oe.fromKind)+'">:'+pe(Oe.fromKind)+'</button><code class="ont-edge-tag ont-edge-tag--'+pe(Oe.category)+'">-[:'+pe(Oe.rel)+"]-&gt;</code>"+Be+'</div><span class="ont-prop-desc">'+pe(Oe.description)+"</span></div>"}document.getElementById("insp-rels-list").innerHTML=be,document.getElementById("insp-cypher").textContent=s.sampleCypher;var Ge=document.getElementById("insp-compact-props-label");Ge&&(Ge.textContent="Properties ("+s.properties.length+" + "+S.commonProperties.length+" base)");var Ye=document.getElementById("insp-compact-props");if(Ye){for(var Ze="",me=Math.min(6,s.properties.length),Ke=0;Ke<me;Ke++){var mt=s.properties[Ke];Ze+='<code class="ont-edge-prop" title="'+pe(mt.description)+'">'+pe(mt.name)+": "+pe(mt.type)+"</code>"}s.properties.length>6&&(Ze+='<code class="ont-edge-prop">+'+(s.properties.length-6)+" more</code>"),Ye.innerHTML=Ze}var Pt=document.getElementById("insp-compact-rels-label");Pt&&(Pt.textContent="Connected entities ("+de.length+" out, "+Ne.length+" in)");var rn=document.getElementById("insp-compact-rels");if(rn){for(var Rt={},ct=[],ot=0;ot<de.length;ot++){var je=de[ot].toKind;Rt[je]||(Rt[je]=!0,ct.push(je))}for(var xt=0;xt<Ne.length;xt++){var wt=Ne[xt].fromKind;Rt[wt]||(Rt[wt]=!0,ct.push(wt))}for(var We="",vn=Math.min(6,ct.length),_t=0;_t<vn;_t++){var dt=ct[_t];We+='<button type="button" class="ont-jump-btn" data-select-kind="'+pe(dt)+'">:'+pe(dt)+"</button>"}ct.length>6&&(We+='<code class="ont-edge-prop">+'+(ct.length-6)+" more</code>"),rn.innerHTML=We}var Yt=document.getElementById("entity-inspector"),Gt=document.getElementById("ont-graph-panel"),Mt=!!(Gt&&Gt.classList.contains("is-fullscreen"));if(Yt){Yt.hidden=D==="graph"&&!z;var bn=D==="layers"||Mt||H;Yt.classList.toggle("is-compact",!bn),Yt.classList.toggle("is-expanded-view",bn)}var Wt=document.getElementById("insp-toggle-mode-btn");Wt&&(Wt.hidden=D==="layers"||Mt,Wt.textContent=H?"Compact":"Full");var Ut=document.getElementById("insp-close-btn");Ut&&(Ut.hidden=D==="layers")}}function ar(s){var u=document.getElementById("ont-focus-label"),v=document.getElementById("ont-focus-count"),w=document.getElementById("ont-focus-links");if(!(!u||!v||!w)){if(u.textContent="Active connections for :"+g,v.textContent=s.length+" active links",s.length===0){w.innerHTML='<span class="ont-layer-sub">Enable relationship category filters above to display connected entities.</span>';return}for(var C=Math.min(12,s.length),c="",N=0;N<C;N++){var P=s[N],G=P.dir==="out"?"out: "+pe(P.rel)+" :"+pe(P.peer):"in: :"+pe(P.peer)+" "+pe(P.rel);c+='<button type="button" class="ont-focus-chip ont-focus-chip--'+pe(P.cat)+'" data-select-kind="'+pe(P.peer)+'"><code>'+G+"</code></button>"}w.innerHTML=c}}function Nn(s,u){return!!ge[s.layerId]&&tn(s,u)}function Pn(){if(_e!==null||ve)return;function s(){if(_e=null,!(ve||D!=="graph")){if(ee+=(Se-ee)*Qe,ee<B&&Se===0){ee=0;return}Jr(x,k,ee,Z,ue),_r(Ar),bt(),_e=requestAnimationFrame(s)}}_e=requestAnimationFrame(s)}function bo(s){for(var u=[],v=0;v<S.nodes.length;v++){var w=S.nodes[v];Nn(w,s)&&u.push(w)}for(var C=ln(u),c=C.visibleDtos,N={},P=[],G=0;G<c.length;G++)N[c[G].kind]=!0,P.push(c[G].kind);for(var O=[],q=0;q<Me.length;q++){var F=Me[q].kind;O.push(F+":"+(Ce[F]?"1":"0"))}var K=P.join(",")+"|"+O.join(","),re=!!(M&&M!==K);M=K,z&&!N[z]&&(z=C.redirectMap[z]||null),xa(c,C.redirectMap,C.collapsedCounts),fn=rr(N,C.redirectMap),re&&Zr&&Tt.length>0&&(nn=!0,In()),!ve&&(ee>=B||Se>0)&&Pn()}function St(){var s=ye.trim().toLowerCase(),u=L[g];if(!u||!ge[u.layerId]){for(var v=0;v<S.nodes.length;v++)if(Nn(S.nodes[v],s)){g=S.nodes[v].kind;break}if(!L[g]||!ge[L[g].layerId]){for(var w=0;w<S.nodes.length;w++)if(ge[S.nodes[w].layerId]){g=S.nodes[w].kind;break}}}for(var C=Oa(g),c=document.querySelectorAll(".ont-node-btn"),N=["control_flow","data_flow","type_dependency","structural"],P=0;P<c.length;P++){var G=c[P],O=G.getAttribute("data-kind"),q=L[O];if(q){var F=Nn(q,s);G.hidden=!F;var K=O===g,re=C.badges[O]||[],Ie=re.length>0,de=Ie?re[0].cat:"";G.setAttribute("aria-selected",K?"true":"false"),G.classList.toggle("is-neighbor",!K&&Ie),G.classList.toggle("is-dimmed",!K&&!Ie);for(var Ne=0;Ne<N.length;Ne++)G.classList.toggle("is-neighbor--"+N[Ne],!K&&Ie&&de===N[Ne])}}for(var be=document.querySelectorAll(".ont-layer[data-layer-id]"),Ae=0;Ae<be.length;Ae++){var Re=Number(be[Ae].getAttribute("data-layer-id"));be[Ae].hidden=!ge[Re]}for(var se=document.querySelectorAll(".ont-layer-btn"),U=0;U<se.length;U++){var J=Number(se[U].getAttribute("data-layer"));se[U].setAttribute("aria-pressed",ge[J]?"true":"false")}var De=document.getElementById("ont-capsule-group");De&&(De.hidden=D!=="graph"||!ge[4]);for(var ze=document.querySelectorAll(".ont-capsule-btn"),qe=0;qe<ze.length;qe++){var Oe=ze[qe].getAttribute("data-hierarchy"),Be=!!Ce[Oe],Ge=L[Oe];if(Ge&&Ge.ancestors)for(var Ye=0;Ye<Ge.ancestors.length;Ye++)Ce[Ge.ancestors[Ye]]||(Be=!1);ze[qe].setAttribute("aria-pressed",Be?"true":"false")}var Ze=document.getElementById("ont-graph-panel"),me=document.getElementById("ont-canvas-frame"),Ke=document.getElementById("ont-graph-foot"),mt=document.getElementById("ont-graph-zoom-group"),Pt=document.getElementById("ont-swimlanes"),rn=me?me.hidden:!1;Ze&&Ze.classList.toggle("is-layers-mode",D==="layers"),me&&(me.hidden=D!=="graph"),Ke&&(Ke.hidden=D!=="graph"),mt&&(mt.hidden=D!=="graph"),Pt&&(Pt.hidden=D!=="layers");var Rt=document.getElementById("graph-show-all");Rt&&(Rt.hidden=!z);for(var ct=document.querySelectorAll(".ont-mode-btn"),ot=0;ot<ct.length;ot++){var je=ct[ot].getAttribute("data-mode");ct[ot].setAttribute("aria-pressed",je===D?"true":"false")}bo(s),rn&&D==="graph"&&(nn=!0,In()),bt(),ar(C.conns),Ba(L[g],s)}function Fa(s){if(s){var u=!Ce[s],v=L[s];if(v&&v.ancestors)for(var w=0;w<v.ancestors.length;w++)Ce[v.ancestors[w]]||(u=!0,Ce[v.ancestors[w]]=!0);Ce[s]=u,nn=!0,St(),In(),bt()}}document.addEventListener("click",function(s){var u=s.target.closest("#insp-close-btn");if(u){z=null,H=!1,St();return}var v=s.target.closest("#insp-toggle-mode-btn");if(v){H=!H,St();return}var w=s.target.closest(".ont-mode-btn");if(w){D=w.getAttribute("data-mode")||"graph",St();return}var C=s.target.closest(".ont-capsule-btn");if(C){Fa(C.getAttribute("data-hierarchy"));return}var c=s.target.closest(".ont-layer-btn");if(c){var N=Number(c.getAttribute("data-layer"));ge[N]=!ge[N],!ge[5]&&!ge[4]&&!ge[3]&&!ge[2]&&!ge[1]&&(ge={5:!0,4:!0,3:!0,2:!0,1:!0}),St();return}var P=s.target.closest(".ont-node-btn");if(P){g=P.getAttribute("data-kind"),St();return}var G=s.target.closest("[data-select-kind]");if(G){g=G.getAttribute("data-select-kind");var O=L[g];if(O&&(ge[O.layerId]=!0,O.ancestors))for(var q=0;q<O.ancestors.length;q++)Ce[O.ancestors[q]]=!0;D==="graph"&&(z=g),St();return}var F=s.target.closest(".ont-cat-btn");if(F){var K=F.getAttribute("data-cat");ne[K]=!ne[K],F.setAttribute("aria-pressed",ne[K]?"true":"false"),St();return}});var lt=document.getElementById("ont-graph-canvas");lt&&(lt.style.cursor="grab",lt.addEventListener("pointerdown",function(s){var u=lt.getBoundingClientRect(),v=s.clientX-u.left,w=s.clientY-u.top,C=ea(v,w);if(C){lt.setPointerCapture(s.pointerId),Fe={mode:"capsule",kind:C,sx:s.clientX,sy:s.clientY,ox:0,oy:0,moved:!1};return}var c=ta(v,w);if(lt.setPointerCapture(s.pointerId),lt.style.cursor="grabbing",c){Fe={mode:"node",kind:c.kind,sx:s.clientX,sy:s.clientY,ox:c.x,oy:c.y,moved:!1};var N=m[c.kind];N&&(N.fx=N.x,N.fy=N.y),ve||(Se=.3,ee=Math.max(ee,.3),Pn())}else Fe={mode:"pan",kind:null,sx:s.clientX,sy:s.clientY,ox:ce.panX,oy:ce.panY,moved:!1}}),lt.addEventListener("pointermove",function(s){var u=lt.getBoundingClientRect();if(Fe){var v=s.clientX-Fe.sx,w=s.clientY-Fe.sy;if((Math.abs(v)>4||Math.abs(w)>4)&&(Fe.moved=!0),Fe.mode==="pan")Fe.moved&&(nn=!1),ce.panX=Fe.ox+v,ce.panY=Fe.oy+w,bt();else if(Fe.mode==="node"&&Fe.kind){var C=Fe.ox+v/ce.zoom,c=Fe.oy+w/ce.zoom,N=m[Fe.kind];N&&(N.fx=C,N.fy=c,N.x=C,N.y=c),cn[Fe.kind]&&(cn[Fe.kind].x=C,cn[Fe.kind].y=c),ve||(ee=Math.max(ee,.3),Pn()),bt()}return}var P=s.clientX-u.left,G=s.clientY-u.top,O=ea(P,G),q=O?null:ta(P,G),F=q?q.kind:null;lt.style.cursor=O||F?"pointer":"grab",(O!==fe||F!==ae)&&(fe=O,ae=F,bt())}),lt.addEventListener("pointerup",function(s){lt.hasPointerCapture(s.pointerId)&&lt.releasePointerCapture(s.pointerId);var u=Fe;if(Fe=null,lt.style.cursor=fe||ae?"pointer":"grab",u&&u.mode==="node"&&u.kind){Se=0;var v=m[u.kind];v&&!ve&&(v.fx=null,v.fy=null),ve||Pn()}!u||u.moved||(u.mode==="capsule"&&u.kind?(fe=null,Fa(u.kind)):u.mode==="node"&&u.kind?(g=u.kind,z=z===u.kind?null:u.kind,St()):z&&(z=null,St()))}),lt.addEventListener("pointerleave",function(){!Fe&&(ae||fe)&&(ae=null,fe=null,bt())}),lt.addEventListener("wheel",function(s){s.preventDefault(),nn=!1;var u=lt.getBoundingClientRect(),v=s.clientX-u.left,w=s.clientY-u.top,C=s.deltaY<0?1.12:.89,c=Math.max(.22,Math.min(2.8,ce.zoom*C)),N=c/ce.zoom;ce.zoom=c,ce.panX=v-(v-ce.panX)*N,ce.panY=w-(w-ce.panY)*N,bt()},{passive:!1}));function na(s){nn=!1;var u=document.getElementById("ont-canvas-frame"),v=(u?u.clientWidth:760)/2,w=(u?u.clientHeight:640)/2,C=Math.max(.22,Math.min(2.8,ce.zoom*s)),c=C/ce.zoom;ce.zoom=C,ce.panX=v-(v-ce.panX)*c,ce.panY=w-(w-ce.panY)*c,bt()}var Ka=document.getElementById("graph-zoom-in"),or=document.getElementById("graph-zoom-out"),ra=document.getElementById("graph-zoom-fit"),sr=document.getElementById("graph-show-all"),Nr=document.getElementById("graph-fullscreen"),Ht=document.getElementById("ont-graph-panel"),Rn=document.getElementById("ont-canvas-frame");function Pr(){if(Rn){var s=Rn.clientWidth||0,u=Rn.clientHeight||0;s<=0||u<=0||s===Ir&&u===Ma||(Ir=s,Ma=u,nn&&In(),bt())}}function Rr(s){Ht&&(nn=!0,Ht.classList.toggle("is-fullscreen",s),document.body.classList.toggle("has-fullscreen-graph",s),Nr&&(Nr.setAttribute("aria-pressed",s?"true":"false"),Nr.textContent=s?"Exit":"Expand"),St(),In(),bt(),requestAnimationFrame(function(){In(),bt()}))}Nr&&Ht&&Nr.addEventListener("click",function(){var s=Ht.classList.contains("is-fullscreen");Rr(!s)}),document.addEventListener("keydown",function(s){s.key==="Escape"&&Ht&&Ht.classList.contains("is-fullscreen")&&Rr(!1)}),document.addEventListener("mousedown",function(s){Ht&&Ht.classList.contains("is-fullscreen")&&!Ht.contains(s.target)&&Rr(!1)});var xr=document.getElementById("graph-air-slider"),aa=document.getElementById("graph-air-val"),Mr=document.getElementById("graph-hub-slider"),dn=document.getElementById("graph-hub-val"),Dr=document.getElementById("graph-freeze");if(xr&&(xr.value=String(Z)),aa&&(aa.textContent=Z.toFixed(1)+"x"),Mr&&(Mr.value=String(ue)),dn&&(dn.textContent=ue.toFixed(1)+"x"),xr&&xr.addEventListener("input",function(){var s=parseFloat(xr.value)||1;Z=Math.max(.1,Math.min(2.4,s)),aa&&(aa.textContent=Z.toFixed(1)+"x");try{localStorage.setItem("ce_ontology_air",String(Z))}catch{}ve||(ee=Math.max(ee,.38),Pn())}),Mr&&Mr.addEventListener("input",function(){var s=parseFloat(Mr.value),u=isNaN(s)?1:s;ue=Math.max(0,Math.min(5,u)),dn&&(dn.textContent=ue.toFixed(1)+"x");try{localStorage.setItem("ce_ontology_hub",String(ue))}catch{}ve||(ee=Math.max(ee,.42),Pn())}),Dr&&Dr.addEventListener("click",function(){if(ve=!ve,Dr.setAttribute("aria-pressed",ve?"true":"false"),Dr.textContent=ve?"Frozen":"Freeze",ve){ee=0,Se=0;for(var s=0;s<x.length;s++)x[s].vx=0,x[s].vy=0}else{for(var u=0;u<x.length;u++)x[u].fx=null,x[u].fy=null;ee=Math.max(ee,.28),Pn()}}),Ka&&Ka.addEventListener("click",function(){na(1.18)}),or&&or.addEventListener("click",function(){na(.84)}),ra&&ra.addEventListener("click",function(){z=null,nn=!0,Lr(),St(),In(),bt()}),sr&&sr.addEventListener("click",function(){z=null,St()}),Rn&&typeof ResizeObserver<"u"){var So=new ResizeObserver(function(){Pr()});So.observe(Rn)}window.addEventListener("resize",function(){Pr(),bt()});var ja=document.getElementById("ont-search");ja&&ja.addEventListener("input",function(){ye=ja.value||"",St()});var ir=document.getElementById("copy-cypher-btn");return ir&&ir.addEventListener("click",function(){var s=document.getElementById("insp-cypher").textContent;navigator.clipboard&&navigator.clipboard.writeText(s).then(function(){var u=ir.textContent;ir.textContent="Copied",setTimeout(function(){ir.textContent=u},1500)})}),St(),()=>{document.body.classList.remove("has-fullscreen-graph"),typeof _e<"u"&&_e!==null&&cancelAnimationFrame(_e)}}const bu="/ontology",Su=()=>{Tr.useEffect(()=>vu(),[]);const S=jn[bu];return A.jsx("main",{className:"ontology-page",dangerouslySetInnerHTML:{__html:S.mainHtml}})},wu="/benchmarks",Cu=()=>{const S=jn[wu];return A.jsx("main",{dangerouslySetInnerHTML:{__html:S.mainHtml}})},ku="/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty",Eu=({routePath:S})=>{const D=jn[S]||jn[ku];return A.jsx("main",{dangerouslySetInnerHTML:{__html:D.mainHtml}},S)},Tu="/privacy",_u=()=>{const S=jn[Tu];return A.jsx("main",{className:"page-privacy",dangerouslySetInnerHTML:{__html:S.mainHtml}})},ys="/",Sd="/index.html",yo="/",fs=".html",gd="";function zi(S){const D=S.trim();if(!D||D===yo||D===Sd)return ys;const g=D.startsWith(yo)?D:`${yo}${D}`,z=g.length>1&&g.endsWith(yo)?g.slice(0,-1):g,H=z.endsWith(fs)?z.slice(0,-fs.length):z;return jn[H]?H:ys}function Au(S,D,g){const z=`${D}${g}`,H=new URL(S,z);if(H.origin!==D)return{route:gd,hash:gd,isSpaRoute:!1};const ae=H.pathname,fe=ae.length>1&&ae.endsWith(yo)?ae.slice(0,-1):ae,ye=fe===Sd?ys:fe.endsWith(fs)?fe.slice(0,-fs.length):fe,ne=!!jn[ye];return{route:ne?ye:ys,hash:H.hash,isSpaRoute:ne}}const Lu="/",Iu="/architectures",Nu="/ontology",Pu="/benchmarks",Ru="/privacy",yd="popstate",fd="click",xu="a[href]",Mu="href",Du="target",Ou="download",Bu="data-open-drawer",Fu="_blank",Ku="smooth",wd="#",vd=0;function bd(S){if(S&&S.startsWith(wd)&&S.length>1){const D=S.slice(1),g=document.getElementById(D);if(g){g.scrollIntoView({behavior:Ku});return}}window.scrollTo(vd,vd)}const ju=({initialPath:S})=>{const[D,g]=Tr.useState(()=>zi(S));Tr.useEffect(()=>{const ae=jn[D];ae&&typeof document<"u"&&(document.title=ae.title)},[D]),Tr.useEffect(()=>{const ae=()=>{const ye=zi(window.location.pathname);g(ye);const ne=window.location.hash;requestAnimationFrame(()=>bd(ne))},fe=ye=>{if(ye.defaultPrevented||ye.button!==0||ye.metaKey||ye.ctrlKey||ye.shiftKey||ye.altKey)return;const ne=ye.target;if(!ne||typeof ne.closest!="function")return;const ge=ne.closest(xu);if(!ge||ge.getAttribute(Du)===Fu||ge.hasAttribute(Ou)||ge.hasAttribute(Bu))return;const Ce=ge.getAttribute(Mu);if(!Ce||Ce.startsWith(wd))return;const b=Au(Ce,window.location.origin,window.location.pathname);if(!b.isSpaRoute)return;ye.preventDefault();const M=`${b.route}${b.hash}`;window.history.pushState(null,"",M),g(b.route),requestAnimationFrame(()=>bd(b.hash))};return window.addEventListener(yd,ae),document.addEventListener(fd,fe),()=>{window.removeEventListener(yd,ae),document.removeEventListener(fd,fe)}},[]);let z,H=null;return D===Lu?(z=A.jsx(mu,{}),H=A.jsx(hu,{})):D===Iu?z=A.jsx(fu,{}):D===Nu?z=A.jsx(Su,{}):D===Pu?z=A.jsx(Cu,{}):D===Ru?z=A.jsx(_u,{}):z=A.jsx(Eu,{routePath:D}),A.jsx(du,{currentRoute:D,afterFooter:H,children:z})},Hu="root",Gu="/",go=document.getElementById(Hu);if(go){const S=window.location.pathname,D=zi(S),g=A.jsx(ju,{initialPath:S});go.hasChildNodes()&&D===Gu?md.hydrateRoot(go,g):(go.innerHTML="",md.createRoot(go).render(g))}
