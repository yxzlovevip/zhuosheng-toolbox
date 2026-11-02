import {useCallback,useRef,useState} from 'react';
import {invoke} from '../bridge';
const empty={checking:false,report:null,error:''};
export function useToolFileCheck(currentSession){
 const [state,setState]=useState(empty),flight=useRef(null);
 const reset=useCallback(()=>{flight.current=null;setState(empty);},[]);
 const check=useCallback(async()=>{
  const origin=currentSession.current;if(!origin||flight.current)return;
  const operation={};flight.current=operation;setState({checking:true,report:null,error:''});
  try{const report=await invoke('checkToolFiles');if(currentSession.current===origin)setState({checking:false,report,error:''});}
  catch(error){if(currentSession.current===origin)setState({checking:false,report:null,error:error.message});}
  finally{if(flight.current===operation)flight.current=null;}
 },[currentSession]);
 return {state,check,reset};
}
